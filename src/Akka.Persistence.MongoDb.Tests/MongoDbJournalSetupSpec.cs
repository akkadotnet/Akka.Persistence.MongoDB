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
            var s = fixture.ConnectionString.Split('?');
            var con = s[0] + $"?" + s[1];
            var connectionString = new MongoUrl(con);
            var client = new MongoClient(connectionString);
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
