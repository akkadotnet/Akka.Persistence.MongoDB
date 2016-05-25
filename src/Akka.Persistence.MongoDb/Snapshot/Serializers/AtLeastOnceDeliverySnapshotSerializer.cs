using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Akka.Persistence.MongoDb.Snapshot.Serializers
{
    public class AtLeastOnceDeliverySnapshotSerializer : SerializerBase<AtLeastOnceDeliverySnapshot>
    {
        public override Akka.Persistence.AtLeastOnceDeliverySnapshot Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var currentBsonType = context.Reader.GetCurrentBsonType();

            if (currentBsonType == MongoDB.Bson.BsonType.Null)
            {
                context.Reader.ReadNull();
                return null;
            }

            context.Reader.ReadStartDocument();

            string object_type = context.Reader.ReadString();

            long currentDeliveryId = context.Reader.ReadInt64();
            UnconfirmedDelivery[] unconfirmedDeliveries = null;

            var name = context.Reader.ReadName(MongoDB.Bson.IO.Utf8NameDecoder.Instance);
            if (name == "UnconfirmedDeliveries")
            {
                unconfirmedDeliveries = BsonSerializer.Deserialize<UnconfirmedDelivery[]>(context.Reader);
            }

            context.Reader.ReadEndDocument();

            var result = new AtLeastOnceDeliverySnapshot(currentDeliveryId, unconfirmedDeliveries);
            return result;
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, AtLeastOnceDeliverySnapshot value)
        {
            context.Writer.WriteStartDocument();

            context.Writer.WriteName("_t");
            context.Writer.WriteString(value.GetType().Name);

            context.Writer.WriteName("CurrentDeliveryId");
            context.Writer.WriteInt64(value.CurrentDeliveryId);

            context.Writer.WriteName("UnconfirmedDeliveries");
            BsonSerializer.Serialize<UnconfirmedDelivery[]>(context.Writer, value.UnconfirmedDeliveries);

            context.Writer.WriteEndDocument();
        }
    }
}
