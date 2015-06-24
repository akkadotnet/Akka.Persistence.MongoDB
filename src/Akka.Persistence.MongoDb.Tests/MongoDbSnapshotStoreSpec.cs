using Akka.Configuration;
using Akka.Persistence.TestKit.Snapshot;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Akka.Persistence.MongoDb.Tests
{
    public class MongoDbSnapshotStoreSpec : SnapshotStoreSpec
    {
        private static readonly Config SpecConfig = ConfigurationFactory.ParseString(@"
        akka.persistence {
            publish-plugin-commands = on
            snapshot-store {
                plugin = ""akka.persistence.snapshot-store.mongodb""
                mongodb {
                    class = ""Akka.Persistence.MongoDb.Snapshot.MongoDbSnapshotStore, Akka.Persistence.MongoDb""
                    connection-string = ""mongodb://localhost/akkanet""
                    collection = ""SnapshotStore""
                }
            }
        }");

        public MongoDbSnapshotStoreSpec() : base(SpecConfig, "MongoDbSnapshotStoreSpec")
        {
            ClearDb();
            Initialize();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            ClearDb();
        }

        private void ClearDb()
        {
            var db = new MongoClient("mongodb://localhost").GetDatabase("akkanet");
            db.GetCollection<BsonDocument>("SnapshotStore").DeleteManyAsync(x => true).Wait();
        }
    }
}
