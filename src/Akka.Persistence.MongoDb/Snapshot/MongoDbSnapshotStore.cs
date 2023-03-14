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
using Akka.Persistence.MongoDb.Journal;
using Akka.Persistence.Serialization;
using Akka.Persistence.Snapshot;
using Akka.Util;
using MongoDB.Driver;

namespace Akka.Persistence.MongoDb.Snapshot
{
    /// <summary>
    /// A SnapshotStore implementation for writing snapshots to MongoDB.
    /// </summary>
    public class MongoDbSnapshotStore : SnapshotStore
    {
        private readonly MongoDbSnapshotSettings _settings;
        private Lazy<IMongoDatabase> _mongoDatabase;
        private Lazy<IMongoCollection<SnapshotEntry>> _snapshotCollection;
        
        private readonly Akka.Serialization.Serialization _serialization;

        public MongoDbSnapshotStore() : this(MongoDbPersistence.Get(Context.System).SnapshotStoreSettings)
        { }

        public MongoDbSnapshotStore(Config config) : this(new MongoDbSnapshotSettings(config))
        { }

        public MongoDbSnapshotStore(MongoDbSnapshotSettings settings)
        {
            _settings = settings;
            _serialization = Context.System.Serialization;
        }

        protected override void PreStart()
        {
            base.PreStart();
            _mongoDatabase = new Lazy<IMongoDatabase>(() =>
            {
               MongoClient client;
                var setupOption = Context.System.Settings.Setup.Get<MongoDbPersistenceSetup>();
                if (!setupOption.HasValue || setupOption.Value.SnapshotConnectionSettings == null)
                {
                    var connectionString = new MongoUrl(_settings.ConnectionString);
                    client = new MongoClient(connectionString);
                    return client.GetDatabase(connectionString.DatabaseName);
                }
                else
                {
                    client = new MongoClient(setupOption.Value.SnapshotConnectionSettings);
                    return client.GetDatabase(setupOption.Value.SnapshotDatabaseName);
                }
                
            });
            _snapshotCollection = new Lazy<IMongoCollection<SnapshotEntry>>(() =>
            {
                var collection = _mongoDatabase.Value.GetCollection<SnapshotEntry>(_settings.Collection);
                if (_settings.AutoInitialize)
                {
                    var modelWithAscendingPersistenceIdAndDescendingSequenceNr = new CreateIndexModel<SnapshotEntry>(Builders<SnapshotEntry>.IndexKeys
                        .Ascending(entry => entry.PersistenceId)
                        .Descending(entry => entry.SequenceNr));

                    collection.Indexes
                        .CreateOneAsync(modelWithAscendingPersistenceIdAndDescendingSequenceNr, cancellationToken: CancellationToken.None)
                        .Wait();
                }

                return collection;
            });
        }

        protected override Task<SelectedSnapshot> LoadAsync(string persistenceId, SnapshotSelectionCriteria criteria)
        {
            var filter = CreateRangeFilter(persistenceId, criteria);

            return
                _snapshotCollection.Value
                    .Find(filter)
                    .SortByDescending(x => x.SequenceNr)
                    .Limit(1)
                    .Project(x => ToSelectedSnapshot(x))
                    .FirstOrDefaultAsync();
        }

        protected override async Task SaveAsync(SnapshotMetadata metadata, object snapshot)
        {
            var snapshotEntry = ToSnapshotEntry(metadata, snapshot);
            if (_settings.Transaction)
            {
                var client = _mongoDatabase.Value.Client;
                var sessionOptions = new ClientSessionOptions { };
                
                using (var session = await client.StartSessionAsync(sessionOptions/*, cancellationToken*/))
                {
                    // Begin transaction
                    session.StartTransaction();
                    try
                    {
                        await _snapshotCollection.Value.ReplaceOneAsync(
                            session,
                        CreateSnapshotIdFilter(snapshotEntry.Id),
                        snapshotEntry,
                        new ReplaceOptions { IsUpsert = true });
                    }
                    catch (Exception ex)
                    {
                        await session.AbortTransactionAsync();
                        throw ex;
                    }

                    await session.CommitTransactionAsync();
                }
            }
            else
                await _snapshotCollection.Value.ReplaceOneAsync(
                CreateSnapshotIdFilter(snapshotEntry.Id),
                snapshotEntry,
                new ReplaceOptions { IsUpsert = true });
        }

        protected override Task DeleteAsync(SnapshotMetadata metadata)
        {
            var builder = Builders<SnapshotEntry>.Filter;
            var filter = builder.Eq(x => x.PersistenceId, metadata.PersistenceId);

            if (metadata.SequenceNr > 0 && metadata.SequenceNr < long.MaxValue)
                filter &= builder.Eq(x => x.SequenceNr, metadata.SequenceNr);

            if (metadata.Timestamp != DateTime.MinValue && metadata.Timestamp != DateTime.MaxValue)
                filter &= builder.Eq(x => x.Timestamp, metadata.Timestamp.Ticks);

            return _snapshotCollection.Value.FindOneAndDeleteAsync(filter);
        }

        protected override Task DeleteAsync(string persistenceId, SnapshotSelectionCriteria criteria)
        {
            var filter = CreateRangeFilter(persistenceId, criteria);

            return _snapshotCollection.Value.DeleteManyAsync(filter);
        }

        private static FilterDefinition<SnapshotEntry> CreateSnapshotIdFilter(string snapshotId)
        {
            var builder = Builders<SnapshotEntry>.Filter;

            var filter = builder.Eq(x => x.Id, snapshotId);

            return filter;
        }

        private static FilterDefinition<SnapshotEntry> CreateRangeFilter(string persistenceId, SnapshotSelectionCriteria criteria)
        {
            var builder = Builders<SnapshotEntry>.Filter;
            var filter = builder.Eq(x => x.PersistenceId, persistenceId);

            if (criteria.MaxSequenceNr > 0 && criteria.MaxSequenceNr < long.MaxValue)
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
            Type type = null;

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
                        new SnapshotMetadata(entry.PersistenceId, entry.SequenceNr, new DateTime(entry.Timestamp)), snap.Data);

                return new SelectedSnapshot(
                    new SnapshotMetadata(entry.PersistenceId, entry.SequenceNr, new DateTime(entry.Timestamp)), deserialized);
            }

            // backwards compat - loaded an old snapshot using BSON serialization. No need to deserialize via Akka.NET
            return new SelectedSnapshot(
                new SnapshotMetadata(entry.PersistenceId, entry.SequenceNr, new DateTime(entry.Timestamp)), entry.Snapshot);
        }
    }
}
