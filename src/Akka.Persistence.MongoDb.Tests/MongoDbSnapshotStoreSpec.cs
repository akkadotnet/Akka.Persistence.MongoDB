using System.Configuration;
using Akka.Persistence.TestKit.Snapshot;
using Mongo2Go;

namespace Akka.Persistence.MongoDb.Tests
{
    public class MongoDbSnapshotStoreSpec : SnapshotStoreSpec
    {
        private static readonly string SpecConfig = @"
        akka.persistence {
            publish-plugin-commands = on
            snapshot-store {
                plugin = ""akka.persistence.snapshot-store.mongodb""
                mongodb {
                    class = ""Akka.Persistence.MongoDb.Snapshot.MongoDbSnapshotStore, Akka.Persistence.MongoDb""
                    connection-string = ""<ConnectionString>""
                    collection = ""SnapshotStore""
                }
            }
        }";
        private static MongoDbRunner _runner;

        public MongoDbSnapshotStoreSpec() : base(CreateSpecConfig(), "MongoDbSnapshotStoreSpec")
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
