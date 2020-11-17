using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Akka.Persistence.MongoDb.Auto
{
    public class AutoSequence
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        [BsonElement("_id")]
        public string Id { get; set; }

        public string SequenceName { get; set; }

        public int SequenceValue { get; set; }
    }
}
