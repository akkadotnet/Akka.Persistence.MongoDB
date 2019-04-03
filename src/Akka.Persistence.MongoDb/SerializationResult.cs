using Akka.Serialization;

namespace Akka.Persistence.MongoDb
{
    internal class SerializationResult
    {
        public SerializationResult(object payload, Serializer serializer)
        {
            Payload = payload;
            Serializer = serializer;
        }

        public object Payload { get; }
        public Serializer Serializer { get; }
    }
}