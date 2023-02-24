﻿using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Configuration;
using Akka.Persistence.TCK.Journal;
using Akka.Persistence.TCK.Snapshot;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.MongoDb.Tests
{
    [Collection("MongoDbSpec")]
    public class MongoDbSnapshotStoreSetupSpec : SnapshotStoreSpec, IClassFixture<DatabaseFixture>
    {
        // TEST: MongoDb snapshot plugin set using Setup should behave exactly like when it is
        // set up using connection string.
        public MongoDbSnapshotStoreSetupSpec(
            DatabaseFixture databaseFixture,
            ITestOutputHelper output)
            : base(CreateBootstrapSetup(databaseFixture), nameof(MongoDbSnapshotStoreSetupSpec), output)
        {
            Initialize();
        }

        private static ActorSystemSetup CreateBootstrapSetup(DatabaseFixture fixture)
        {
            var s = fixture.ConnectionString.Split('?');
            var con = s[0] + $"testdb?" + s[1];
            var connectionString = new MongoUrl(con);
            var client = new MongoClient(connectionString);
            var databaseName = connectionString.DatabaseName;
            var settings = client.Settings;

            return BootstrapSetup.Create()
                .WithConfig(CreateSpecConfig())
                .And(new MongoDbPersistenceSetup(databaseName, settings, null, null));
        }

        private static Config CreateSpecConfig()
        {
            var specString = @"
                akka.test.single-expect-default = 3s
                akka.persistence {
                    publish-plugin-commands = on
                    snapshot-store {
                        plugin = ""akka.persistence.snapshot-store.mongodb""
                        mongodb {
                            class = ""Akka.Persistence.MongoDb.Snapshot.MongoDbSnapshotStore, Akka.Persistence.MongoDb""
                            auto-initialize = on
                            collection = ""SnapshotStore""
                        }
                    }
                }";

            return ConfigurationFactory.ParseString(specString);
        }
    }
}
