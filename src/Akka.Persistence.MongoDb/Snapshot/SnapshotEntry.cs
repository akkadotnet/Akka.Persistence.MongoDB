namespace Akka.Persistence.MongoDb.Snapshot
{
    /// <summary>
    /// Class used for storing a Snapshot as BsonDocument
    /// </summary>
    public class SnapshotEntry
    {
        public string Id { get; set; }

        public string PersistenceId { get; set; }

        public long SequenceNr { get; set; }

        public long Timestamp { get; set; }

        public byte[] Snapshot { get; set; }
    }
}