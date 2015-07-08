using System.Configuration;
using Akka.Persistence.TestKit.Journal;
using Mongo2Go;

namespace Akka.Persistence.MongoDb.Tests
{
    public class MongoDbJournalSpec : JournalSpec
    {
        private static readonly string SpecConfig = @"
        akka.persistence {
            publish-plugin-commands = on
            journal {
                plugin = ""akka.persistence.journal.mongodb""
                mongodb {
                    class = ""Akka.Persistence.MongoDb.Journal.MongoDbJournal, Akka.Persistence.MongoDb""
                    connection-string = ""<ConnectionString>""
                    collection = ""EventJournal""
                }
            }
        }";

        private static MongoDbRunner _runner;

        public MongoDbJournalSpec() : base(CreateSpecConfig(), "MongoDbJournalSpec")
        {
            Initialize();
        }

        private static string CreateSpecConfig()
        {
            _runner = MongoDbRunner.Start(ConfigurationManager.AppSettings[0]);
            return SpecConfig.Replace("<ConnectionString>", _runner.ConnectionString + "akkanet");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            try
            {
                _runner.Dispose();
            }
            catch { }
            _runner = null;
        }
    }
}
