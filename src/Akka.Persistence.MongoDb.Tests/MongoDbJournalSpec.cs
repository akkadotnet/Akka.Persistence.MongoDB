using Akka.Configuration;
using Akka.Persistence.TestKit.Journal;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Akka.Persistence.MongoDb.Tests
{
    public class MongoDbJournalSpec : JournalSpec
    {
        private static readonly Config SpecConfig = ConfigurationFactory.ParseString(@"
        akka.persistence {
            publish-plugin-commands = on
            journal {
                plugin = ""akka.persistence.journal.mongodb""
                mongodb {
                    class = ""Akka.Persistence.MongoDb.Journal.MongoDbJournal, Akka.Persistence.MongoDb""
                    connection-string = ""mongodb://localhost/akkanet""
                    collection = ""EventJournal""
                }
            }
        }");

        public MongoDbJournalSpec() : base(SpecConfig, "MongoDbJournalSpec")
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
            db.GetCollection<BsonDocument>("EventJournal").DeleteManyAsync(x => true).Wait();
        }
    }
}
