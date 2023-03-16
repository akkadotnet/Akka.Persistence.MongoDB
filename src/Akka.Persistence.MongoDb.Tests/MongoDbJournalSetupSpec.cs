using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Configuration;
using Akka.Persistence.TCK;
using Akka.Persistence.TCK.Journal;
using Akka.Persistence.TCK.Serialization;
using Akka.TestKit;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.MongoDb.Tests
{
    [Collection("MongoDbSpec")]
    public class MongoDbJournalSetupSpec : JournalSpec, IClassFixture<DatabaseFixture>
    {
        // TEST: MongoDb journal plugin set using Setup should behave exactly like when it is
        // set up using connection string.
        public MongoDbJournalSetupSpec(
            DatabaseFixture databaseFixture,
            ITestOutputHelper output)
            : base(CreateBootstrapSetup(databaseFixture), nameof(MongoDbSnapshotStoreSetupSpec), output)
        {
            Initialize();
        }

        private static ActorSystemSetup CreateBootstrapSetup(DatabaseFixture fixture)
        {
            //Default LinqProvider has been changed to LINQ3.LinqProvider can be changed back to LINQ2 in the following way:
            var connectionString = new MongoUrl(fixture.ConnectionString);
            var clientSettings = MongoClientSettings.FromUrl(connectionString);
            clientSettings.LinqProvider = LinqProvider.V2;
            var client = new MongoClient(clientSettings);
            var databaseName = connectionString.DatabaseName;
            var settings = client.Settings;

            return BootstrapSetup.Create()
                .WithConfig(CreateSpecConfig())
                .And(new MongoDbPersistenceSetup(null, null, databaseName, settings));
        }

        private static Config CreateSpecConfig()
        {
            var specString = @"
                akka.test.single-expect-default = 3s
                akka.persistence {
                    publish-plugin-commands = on
                    journal {
                        plugin = ""akka.persistence.journal.mongodb""
                        mongodb {
                            class = ""Akka.Persistence.MongoDb.Journal.MongoDbJournal, Akka.Persistence.MongoDb""
                            auto-initialize = on
                            collection = ""EventJournal""
                        }
                    }
                }";

            return ConfigurationFactory.ParseString(specString)
                .WithFallback(MongoDbPersistence.DefaultConfiguration());
        }
    }
}
