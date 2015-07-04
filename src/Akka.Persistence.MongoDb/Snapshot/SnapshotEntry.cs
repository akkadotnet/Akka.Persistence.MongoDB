using MongoDB.Bson.Serialization.Attributes;

namespace Akka.Persistence.MongoDb.Snapshot
{
    /// <summary>
    /// Class used for storing a Snapshot as BsonDocument
    /// </summary>
    public class SnapshotEntry
    {
        [BsonId]
        public string Id { get; set; }

        [BsonElement("PersistenceId")]
        public string PersistenceId { get; set; }

        [BsonElement("SequenceNr")]
        public long SequenceNr { get; set; }

        [BsonElement("Timestamp")]
        public long Timestamp { get; set; }

        [BsonElement("Snapshot")]
        public object Snapshot { get; set; }
    }
}