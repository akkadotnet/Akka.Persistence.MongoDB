using System.Linq;
using Akka.Persistence.MongoDb.Snapshot.Serializers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;

namespace Akka.Persistence.MongoDb.Tests.Serialization
{
    public class AtLeastOnceDeliverySnapshotSerializerSpecs
    {
        class TestMessage
        {
            public TestMessage(string text)
            {
                this.Text = text;
            }
            public string Text { get; private set; }
        }

        static AtLeastOnceDeliverySnapshotSerializerSpecs()
        {
            BsonSerializer.RegisterSerializer<AtLeastOnceDeliverySnapshot>(new AtLeastOnceDeliverySnapshotSerializer());
            BsonSerializer.RegisterSerializer<UnconfirmedDelivery>(new UnconfirmedDeliverySerializer());
        }

        [Fact]
        public void TestEmptySnapshot()
        {
            var snapshot = new AtLeastOnceDeliverySnapshot(1, new UnconfirmedDelivery[0]);

            var json = snapshot.ToJson();
            var expected = "{ '_t' : 'AtLeastOnceDeliverySnapshot', 'CurrentDeliveryId' : NumberLong(1), 'UnconfirmedDeliveries' : { '_t' : 'UnconfirmedDelivery[]', '_v' : [] } }"
                .Replace("'", "\"");
            Assert.Equal(expected, json);

            var bson = snapshot.ToBson();
            var rehydrated = BsonSerializer.Deserialize<AtLeastOnceDeliverySnapshot>(bson);
            Assert.True(bson.SequenceEqual(rehydrated.ToBson()));
        }

        [Fact]
        public void TestSnapshotWithUnconfirmedDelivery()
        {
            var snapshot = new AtLeastOnceDeliverySnapshot(1, new[] { new UnconfirmedDelivery(1, Actor.ActorPath.Parse("akka://MySystem/user/test"), new TestMessage("Test message")) });

            var json = snapshot.ToJson();
            var expected = "{ '_t' : 'AtLeastOnceDeliverySnapshot', 'CurrentDeliveryId' : NumberLong(1), 'UnconfirmedDeliveries' : { '_t' : 'UnconfirmedDelivery[]', '_v' : [{ '_t' : 'UnconfirmedDelivery', 'DeliveryId' : NumberLong(1), 'Destination' : 'akka://MySystem/user/test', 'Message' : { '_t' : 'TestMessage', 'Text' : 'Test message' } }] } }"
                .Replace("'", "\"");
            Assert.Equal(expected, json);

            var bson = snapshot.ToBson();
            var rehydrated = BsonSerializer.Deserialize<AtLeastOnceDeliverySnapshot>(bson);
            Assert.True(bson.SequenceEqual(rehydrated.ToBson()));
        }
    }
}