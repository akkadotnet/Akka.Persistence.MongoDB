using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Akka.Persistence.MongoDb.Snapshot;

public class GridFsPayloadEnvelope
{
    [BsonElement("_v")]
    public object Payload { get; set; }
}