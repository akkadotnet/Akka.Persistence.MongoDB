//-----------------------------------------------------------------------
// <copyright file="MongoDbSnapshotStore.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Snapshot;
using Akka.Util;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

#nullable enable
namespace Akka.Persistence.MongoDb.Snapshot
{
    /// <summary>
    /// A SnapshotStore implementation for writing snapshots to MongoDB.
    /// </summary>
    public class MongoDbSnapshotStore : SnapshotStore
    {
        private static readonly ClientSessionOptions EmptySessionOptions = new();
        
        private readonly MongoDbSnapshotSettings _settings;
        // ReSharper disable InconsistentNaming
        private IMongoDatabase? _mongoDatabase_DoNotUseDirectly;
        private IMongoCollection<SnapshotEntry>? _snapshotCollection_DoNotUseDirectly;
        // ReSharper enable InconsistentNaming

        /// <summary>
        /// Used to cancel all outstanding commands when the actor is stopped.
        /// </summary>
        private readonly CancellationTokenSource _pendingCommandsCancellation = new();

        private readonly Akka.Serialization.Serialization _serialization;

        public MongoDbSnapshotStore() : this(MongoDbPersistence.Get(Context.System).SnapshotStoreSettings)
        {
        }

        public MongoDbSnapshotStore(Config config) : this(new MongoDbSnapshotSettings(config))
        {
        }

        public MongoDbSnapshotStore(MongoDbSnapshotSettings settings)
        {
            _settings = settings;
            _serialization = Context.System.Serialization;
        }

        private CancellationTokenSource CreatePerCallCts()
        {
            var unitedCts =
                CancellationTokenSource.CreateLinkedTokenSource(_pendingCommandsCancellation.Token);
            unitedCts.CancelAfter(_settings.CallTimeout);
            return unitedCts;
        }

        private async Task MaybeWithTransaction(Func<IClientSessionHandle?, CancellationToken, Task> act, CancellationToken token)
        {
            if (!_settings.Transaction)
            {
                await act(null, token);
                return;
            }
            
            using var session = await GetMongoDb().Client.StartSessionAsync(EmptySessionOptions, token);
            await session.WithTransactionAsync(
                async (s, ct) =>
                {
                    await act(s, ct);
                    return Task.FromResult(NotUsed.Instance);
                }, cancellationToken:token);
        }
        
        private async Task<T> MaybeWithTransaction<T>(Func<IClientSessionHandle?, CancellationToken, Task<T>> act, CancellationToken token)
        {
            if (!_settings.Transaction) 
                return await act(null, token);
            
            using var session = await GetMongoDb().Client.StartSessionAsync(EmptySessionOptions, token);
            return await session.WithTransactionAsync(act, cancellationToken:token);
        }
        
        private IMongoDatabase GetMongoDb()
        {
            if (_mongoDatabase_DoNotUseDirectly is not null)
                return _mongoDatabase_DoNotUseDirectly;
            
            MongoClient client;
            var setupOption = Context.System.Settings.Setup.Get<MongoDbPersistenceSetup>();
            if (!setupOption.HasValue || setupOption.Value.SnapshotConnectionSettings == null)
            {
                //Default LinqProvider has been changed to LINQ3.LinqProvider can be changed back to LINQ2 in the following way:
                var connectionString = new MongoUrl(_settings.ConnectionString);
                var clientSettings = MongoClientSettings.FromUrl(connectionString);
                clientSettings.LinqProvider = LinqProvider.V2;
                client = new MongoClient(clientSettings);
                _mongoDatabase_DoNotUseDirectly = client.GetDatabase(connectionString.DatabaseName);
                return _mongoDatabase_DoNotUseDirectly;
            }

            client = new MongoClient(setupOption.Value.SnapshotConnectionSettings);
            _mongoDatabase_DoNotUseDirectly = client.GetDatabase(setupOption.Value.SnapshotDatabaseName);
            return _mongoDatabase_DoNotUseDirectly;
        }

        private async Task<IMongoCollection<SnapshotEntry>> GetSnapshotCollection(CancellationToken token)
        {
            _snapshotCollection_DoNotUseDirectly = GetMongoDb().GetCollection<SnapshotEntry>(_settings.Collection);

            if (!_settings.AutoInitialize) 
                return _snapshotCollection_DoNotUseDirectly;
            
            var modelWithAscendingPersistenceIdAndDescendingSequenceNr =
                new CreateIndexModel<SnapshotEntry>(Builders<SnapshotEntry>.IndexKeys
                    .Ascending(entry => entry.PersistenceId)
                    .Descending(entry => entry.SequenceNr));

            await _snapshotCollection_DoNotUseDirectly.Indexes
                .CreateOneAsync(modelWithAscendingPersistenceIdAndDescendingSequenceNr,
                    cancellationToken: token);

            return _snapshotCollection_DoNotUseDirectly;
        }
        
        protected override void PostStop()
        {
            // cancel any pending database commands during shutdown
            _pendingCommandsCancellation.Cancel();
            _pendingCommandsCancellation.Dispose();
            base.PostStop();
        }

        protected override async Task<SelectedSnapshot> LoadAsync(string persistenceId, SnapshotSelectionCriteria criteria)
        {
            using var unitedCts = CreatePerCallCts();
            var snapshotCollection = await GetSnapshotCollection(unitedCts.Token);

            return await MaybeWithTransaction(async (session, token) =>
            {
                var filter = CreateRangeFilter(persistenceId, criteria);
                return await (session is not null ? snapshotCollection.Find(session, filter) : snapshotCollection.Find(filter)) 
                    .SortByDescending(x => x.SequenceNr)
                    .Limit(1)
                    .Project(x => ToSelectedSnapshot(x))
                    .FirstOrDefaultAsync(token);
            }, unitedCts.Token);
        }

        protected override async Task SaveAsync(SnapshotMetadata metadata, object snapshot)
        {
            using var unitedCts = CreatePerCallCts();
            var snapshotCollection = await GetSnapshotCollection(unitedCts.Token);
            
            var snapshotEntry = ToSnapshotEntry(metadata, snapshot);
            await MaybeWithTransaction(async (session, token) =>
            {
                if (session is not null)
                {
                    await snapshotCollection.ReplaceOneAsync(
                        session: session,
                        filter: CreateSnapshotIdFilter(snapshotEntry.Id),
                        replacement: snapshotEntry,
                        options: new ReplaceOptions { IsUpsert = true }, 
                        cancellationToken: token);
                }
                else
                {
                    await snapshotCollection.ReplaceOneAsync(
                        filter: CreateSnapshotIdFilter(snapshotEntry.Id),
                        replacement: snapshotEntry,
                        options: new ReplaceOptions { IsUpsert = true }, 
                        cancellationToken: token);
                }
            }, unitedCts.Token);
        }

        protected override async Task DeleteAsync(SnapshotMetadata metadata)
        {
            using var unitedCts = CreatePerCallCts();
            var snapshotCollection = await GetSnapshotCollection(unitedCts.Token);

            await MaybeWithTransaction(async (session, token) =>
            {
                var builder = Builders<SnapshotEntry>.Filter;
                var filter = builder.Eq(x => x.PersistenceId, metadata.PersistenceId);

                if (metadata.SequenceNr is > 0 and < long.MaxValue)
                    filter &= builder.Eq(x => x.SequenceNr, metadata.SequenceNr);

                if (metadata.Timestamp != DateTime.MinValue && metadata.Timestamp != DateTime.MaxValue)
                    filter &= builder.Eq(x => x.Timestamp, metadata.Timestamp.Ticks);

                if(session is not null)
                    await snapshotCollection.FindOneAndDeleteAsync(session, filter, cancellationToken: token);
                else
                    await snapshotCollection.FindOneAndDeleteAsync(filter, cancellationToken: token);
            }, unitedCts.Token);
        }

        protected override async Task DeleteAsync(string persistenceId, SnapshotSelectionCriteria criteria)
        {
            using var unitedCts = CreatePerCallCts();
            var snapshotCollection = await GetSnapshotCollection(unitedCts.Token);

            await MaybeWithTransaction(async (session, token) =>
            {
                var filter = CreateRangeFilter(persistenceId, criteria);
                if(session is not null)
                    await snapshotCollection.DeleteManyAsync(session, filter, cancellationToken: token);
                else
                    await snapshotCollection.DeleteManyAsync(filter, token);
            }, unitedCts.Token);
        }

        private static FilterDefinition<SnapshotEntry> CreateSnapshotIdFilter(string snapshotId)
        {
            var builder = Builders<SnapshotEntry>.Filter;

            var filter = builder.Eq(x => x.Id, snapshotId);

            return filter;
        }

        private static FilterDefinition<SnapshotEntry> CreateRangeFilter(string persistenceId,
            SnapshotSelectionCriteria criteria)
        {
            var builder = Builders<SnapshotEntry>.Filter;
            var filter = builder.Eq(x => x.PersistenceId, persistenceId);

            if (criteria.MaxSequenceNr is > 0 and < long.MaxValue)
                filter &= builder.Lte(x => x.SequenceNr, criteria.MaxSequenceNr);

            if (criteria.MaxTimeStamp != DateTime.MinValue && criteria.MaxTimeStamp != DateTime.MaxValue)
                filter &= builder.Lte(x => x.Timestamp, criteria.MaxTimeStamp.Ticks);

            return filter;
        }

        private SnapshotEntry ToSnapshotEntry(SnapshotMetadata metadata, object snapshot)
        {
            if (_settings.LegacySerialization)
            {
                var manifest = snapshot.GetType().TypeQualifiedName();

                return new SnapshotEntry
                {
                    Id = metadata.PersistenceId + "_" + metadata.SequenceNr,
                    PersistenceId = metadata.PersistenceId,
                    SequenceNr = metadata.SequenceNr,
                    Snapshot = snapshot,
                    Timestamp = metadata.Timestamp.Ticks,
                    Manifest = manifest,
                    SerializerId = null
                };
            }

            var snapshotRep = new Serialization.Snapshot(snapshot);
            var serializer = _serialization.FindSerializerFor(snapshotRep);
            var binary = serializer.ToBinary(snapshotRep);
            var binaryManifest = Akka.Serialization.Serialization.ManifestFor(serializer, snapshotRep);

            return new SnapshotEntry
            {
                Id = metadata.PersistenceId + "_" + metadata.SequenceNr,
                PersistenceId = metadata.PersistenceId,
                SequenceNr = metadata.SequenceNr,
                Snapshot = binary,
                Timestamp = metadata.Timestamp.Ticks,
                Manifest = binaryManifest,
                SerializerId = serializer?.Identifier
            };
        }

        private SelectedSnapshot ToSelectedSnapshot(SnapshotEntry entry)
        {
            if (_settings.LegacySerialization)
            {
                return new SelectedSnapshot(
                    new SnapshotMetadata(
                        entry.PersistenceId,
                        entry.SequenceNr,
                        new DateTime(entry.Timestamp)),
                    entry.Snapshot);
            }

            var legacy = entry.SerializerId.HasValue || !string.IsNullOrEmpty(entry.Manifest);

            if (!legacy)
            {
                var ser = _serialization.FindSerializerForType(typeof(Serialization.Snapshot));
                var snapshot = ser.FromBinary<Serialization.Snapshot>((byte[])entry.Snapshot);
                return new SelectedSnapshot(new SnapshotMetadata(entry.PersistenceId, entry.SequenceNr), snapshot.Data);
            }

            int? serializerId = null;
            Type? type = null;

            // legacy serialization
            if (!entry.SerializerId.HasValue && !string.IsNullOrEmpty(entry.Manifest))
                type = Type.GetType(entry.Manifest, true);
            else
                serializerId = entry.SerializerId;

            if (entry.Snapshot is byte[] bytes)
            {
                object deserialized;

                if (serializerId.HasValue)
                {
                    deserialized = _serialization.Deserialize(bytes, serializerId.Value, entry.Manifest);
                }
                else
                {
                    var deserializer = _serialization.FindSerializerForType(type);
                    deserialized = deserializer.FromBinary(bytes, type);
                }

                if (deserialized is Serialization.Snapshot snap)
                    return new SelectedSnapshot(
                        new SnapshotMetadata(entry.PersistenceId, entry.SequenceNr, new DateTime(entry.Timestamp)),
                        snap.Data);

                return new SelectedSnapshot(
                    new SnapshotMetadata(entry.PersistenceId, entry.SequenceNr, new DateTime(entry.Timestamp)),
                    deserialized);
            }

            // backwards compat - loaded an old snapshot using BSON serialization. No need to deserialize via Akka.NET
            return new SelectedSnapshot(
                new SnapshotMetadata(entry.PersistenceId, entry.SequenceNr, new DateTime(entry.Timestamp)),
                entry.Snapshot);
        }
    }
}