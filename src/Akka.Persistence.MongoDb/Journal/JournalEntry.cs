namespace Akka.Persistence.MongoDb.Journal
{
    /// <summary>
    /// Class used for storing intermediate result of the <see cref="IPersistentRepresentation"/>
    /// as BsonDocument into the MongoDB-Collection
    /// </summary>
    public class JournalEntry 
    {
        public string Id { get; set; }

        public string PersistenceId { get; set; }

        public long SequenceNr { get; set; }

        public bool IsDeleted { get; set; }

        public byte[] Payload { get; set; }
    }
}
