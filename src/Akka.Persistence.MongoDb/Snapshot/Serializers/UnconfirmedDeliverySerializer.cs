using Akka.Actor;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Akka.Persistence.MongoDb.Snapshot.Serializers
{
    public class UnconfirmedDeliverySerializer : SerializerBase<UnconfirmedDelivery>
    {
        public override UnconfirmedDelivery Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var currentBsonType = context.Reader.GetCurrentBsonType();

            if (currentBsonType == MongoDB.Bson.BsonType.Null)
            {
                context.Reader.ReadNull();
                return null;
            }

            context.Reader.ReadStartDocument();

            string object_type = context.Reader.ReadString();

            long deliveryId = context.Reader.ReadInt64();
            ActorPath destination = ActorPath.Parse(context.Reader.ReadString());
            object message = null;

            var name = context.Reader.ReadName(MongoDB.Bson.IO.Utf8NameDecoder.Instance);
            if (name == "Message")
            {
                message = BsonSerializer.Deserialize(context.Reader, typeof(object));
            }

            context.Reader.ReadEndDocument();

            UnconfirmedDelivery result = new UnconfirmedDelivery(deliveryId, destination, message);
            return result;
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, UnconfirmedDelivery value)
        {
            context.Writer.WriteStartDocument();
            context.Writer.WriteName("_t");
            context.Writer.WriteString(value.GetType().Name);

            context.Writer.WriteName("DeliveryId");
            context.Writer.WriteInt64(value.DeliveryId);

            context.Writer.WriteName("Destination");
            context.Writer.WriteString(value.Destination.ToSerializationFormat());

            context.Writer.WriteName("Message");
            BsonSerializer.Serialize(context.Writer, value.Message);

            context.Writer.WriteEndDocument();
        }
    }
}
