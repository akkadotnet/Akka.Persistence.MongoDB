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
        }";


        public MongoDbSnapshotStoreSpec() : base(CreateSpecConfig(), "MongoDbSnapshotStoreSpec")
        {
            AppDomain.CurrentDomain.DomainUnload += (_, __) => Runner.Dispose();

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
