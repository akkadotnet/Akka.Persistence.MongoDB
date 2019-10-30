//-----------------------------------------------------------------------
// <copyright file="MongoDbSnapshotStore.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Persistence.Snapshot;
using Akka.Serialization;
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

        private Lazy<IMongoCollection<SnapshotEntry>> _snapshotCollection;


        private readonly Akka.Serialization.Serialization _serialization;

        public MongoDbSnapshotStore()
        {
            _settings = MongoDbPersistence.Get(Context.System).SnapshotStoreSettings;

            _serialization = Context.System.Serialization;
        }

        protected override void PreStart()
        {
            base.PreStart();

            _snapshotCollection = new Lazy<IMongoCollection<SnapshotEntry>>(() =>
            {
                var connectionString = new MongoUrl(_settings.ConnectionString);
                var client = new MongoClient(connectionString);

                var snapshot = client.GetDatabase(connectionString.DatabaseName);

                var collection = snapshot.GetCollection<SnapshotEntry>(_settings.Collection);
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

            await _snapshotCollection.Value.ReplaceOneAsync(
                CreateSnapshotIdFilter(snapshotEntry.Id),
                snapshotEntry,
                new UpdateOptions { IsUpsert = true });
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
            var snapshotRep = new Akka.Persistence.Serialization.Snapshot(snapshot);
            var serializer = _serialization.FindSerializerFor(snapshotRep);
            var binary = serializer.ToBinary(snapshotRep);

            var manifest = "";
            if (serializer is SerializerWithStringManifest stringManifest)
                manifest = stringManifest.Manifest(snapshotRep);
            else
                manifest = snapshotRep.GetType().TypeQualifiedName();

            return new SnapshotEntry
            {
                Id = metadata.PersistenceId + "_" + metadata.SequenceNr,
                PersistenceId = metadata.PersistenceId,
                SequenceNr = metadata.SequenceNr,
                Snapshot = binary,
                Timestamp = metadata.Timestamp.Ticks,
                Manifest = manifest,
                SerializerId = serializer?.Identifier
            };
        }

        private SelectedSnapshot ToSelectedSnapshot(SnapshotEntry entry)
        {


            if (entry.Snapshot is byte[] bytes)
            {
                Type type = null;

                if (string.IsNullOrEmpty(entry.Manifest))
                    type = Type.GetType(entry.Manifest, throwOnError: true);

                object dSnapshot;
                if (entry.SerializerId.HasValue)
                {
                    dSnapshot = type == null ? _serialization.Deserialize(bytes, entry.SerializerId.Value, entry.Manifest)
                        : _serialization.Deserialize(bytes, entry.SerializerId.Value, type);
                }
                else
                {
                    var deserializer = _serialization.FindSerializerForType(type);
                    dSnapshot = deserializer.FromBinary(bytes, type);
                }

                if (dSnapshot is Serialization.Snapshot snap)
                    return new SelectedSnapshot(
                        new SnapshotMetadata(entry.PersistenceId, entry.SequenceNr, new DateTime(entry.Timestamp)), snap.Data);

                return new SelectedSnapshot(
                    new SnapshotMetadata(entry.PersistenceId, entry.SequenceNr, new DateTime(entry.Timestamp)), dSnapshot);
            }

            // backwards compat - loaded an old snapshot using BSON serialization. No need to deserialize via Akka.NET
            return new SelectedSnapshot(
                new SnapshotMetadata(entry.PersistenceId, entry.SequenceNr, new DateTime(entry.Timestamp)), entry.Snapshot);
        }
    }
}
