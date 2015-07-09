using System;
using System.Configuration;
using Akka.Persistence.TestKit.Journal;
using Mongo2Go;
using MongoDB.Driver;

namespace Akka.Persistence.MongoDb.Tests
{
    public class MongoDbJournalSpec : JournalSpec
    {
        private static readonly MongoDbRunner Runner = MongoDbRunner.Start(ConfigurationManager.AppSettings[0]);

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

        public MongoDbJournalSpec() : base(CreateSpecConfig(), "MongoDbJournalSpec")
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
                .DropCollectionAsync("EventJournal").Wait();
            
            base.Dispose(disposing);
        }
    }
}
