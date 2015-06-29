using System;
using System.Threading.Tasks;
using Akka.Persistence.Snapshot;
using Akka.Serialization;
using MongoDB.Driver;

namespace Akka.Persistence.MongoDb.Snapshot
{
    /// <summary>
    /// A SnapshotStore implementation for writing snapshots to MongoDB.
    /// </summary>
    public class MongoDbSnapshotStore : SnapshotStore
    {
        private static readonly Type SnapshotType = typeof (Serialization.Snapshot);
        private readonly Serializer _serializer;
        private readonly IMongoCollection<SnapshotEntry> _collection;

        public MongoDbSnapshotStore()
        {
            var mongoDbExtension = MongoDbPersistence.Instance.Apply(Context.System);

            _collection = mongoDbExtension.SnapshotCollection;
            _serializer = Context.System.Serialization.FindSerializerForType(SnapshotType);
        }

        protected override Task<SelectedSnapshot> LoadAsync(string persistenceId, SnapshotSelectionCriteria criteria)
        {
            var filter = CreateRangeFilter(persistenceId, criteria);

            return
                _collection
                    .Find(filter)
                    .Project(x => ToSelectedSnapshot(x))
                    .FirstOrDefaultAsync();
        }

        protected override Task SaveAsync(SnapshotMetadata metadata, object snapshot)
        {
            var snapshotEntry = new SnapshotEntry
            {
                Id = metadata.PersistenceId + "_" + metadata.SequenceNr,
                PersistenceId = metadata.PersistenceId,
                SequenceNr = metadata.SequenceNr,
                Snapshot = Serialize(snapshot),
                Timestamp = metadata.Timestamp.Ticks
            };

            // throws a MongoWriteException if s snapshot with the same PersistenceId and SequenceNr 
            // is inserted the second time. As @Horusiath pointed out, that's fine, because a second snapshot
            // without any events in the meantime doesn't make any sense.
            return _collection.InsertOneAsync(snapshotEntry);
        }

        protected override void Saved(SnapshotMetadata metadata) { }

        protected override void Delete(SnapshotMetadata metadata)
        {
            var builder = Builders<SnapshotEntry>.Filter;
            var filter = builder.Eq(x => x.PersistenceId, metadata.PersistenceId);
            
            if (metadata.SequenceNr > 0 && metadata.SequenceNr < long.MaxValue)
                filter &= builder.Eq(x => x.SequenceNr, metadata.SequenceNr);

            if (metadata.Timestamp != DateTime.MinValue && metadata.Timestamp != DateTime.MaxValue)
                filter &= builder.Eq(x => x.Timestamp, metadata.Timestamp.Ticks);

            _collection.FindOneAndDeleteAsync(filter).Wait();
        }

        protected override void Delete(string persistenceId, SnapshotSelectionCriteria criteria)
        {
            var filter = CreateRangeFilter(persistenceId, criteria);

            _collection.DeleteManyAsync(filter).Wait();
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
        
        private SelectedSnapshot ToSelectedSnapshot(SnapshotEntry entry)
        {
            return new SelectedSnapshot(
                new SnapshotMetadata(entry.PersistenceId, entry.SequenceNr, new DateTime(entry.Timestamp)),
                Deserialize(entry.Snapshot));
        }

        private object Deserialize(byte[] bytes)
        {
            return ((Serialization.Snapshot)_serializer.FromBinary(bytes, SnapshotType)).Data;
        }

        private byte[] Serialize(object snapshotData)
        {
            return _serializer.ToBinary(new Serialization.Snapshot(snapshotData));
        }
    }
}
