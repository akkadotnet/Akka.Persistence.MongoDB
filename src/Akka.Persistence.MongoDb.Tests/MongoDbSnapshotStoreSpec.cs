using System;
using System.Configuration;
using Akka.Persistence.TestKit.Snapshot;
using Mongo2Go;
using MongoDB.Driver;

namespace Akka.Persistence.MongoDb.Tests
{
    public class MongoDbSnapshotStoreSpec : SnapshotStoreSpec
    {
        private static readonly MongoDbRunner Runner = MongoDbRunner.Start(ConfigurationManager.AppSettings[0]);

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
            journal {
                plugin = ""akka.persistence.journal.mongodb""
                mongodb {
                    class = ""Akka.Persistence.MongoDb.Journal.MongoDbJournal, Akka.Persistence.MongoDb""
                    connection-string = ""<ConnectionString>""
                    collection = ""EventJournal""
                }
            }
        }";


        public MongoDbSnapshotStoreSpec() : base(CreateSpecConfig(), "MongoDbSnapshotStoreSpec")
        {
            AppDomain.CurrentDomain.DomainUnload += (_, __) =>
            {
                try
                {
                    Runner.Dispose();
                }
                catch { }
            };


            Initialize();
        }

        private static string CreateSpecConfig()
        {
            return SpecConfig.Replace("<ConnectionString>", Runner.ConnectionString + "akkanet");
        }

        protected override void Dispose(bool disposing)
        {
            new MongoClient(Runner.ConnectionString)
                .GetDatabase("akkanet")
                .DropCollectionAsync("SnapshotStore").Wait();

            base.Dispose(disposing);
        }
    }
}
