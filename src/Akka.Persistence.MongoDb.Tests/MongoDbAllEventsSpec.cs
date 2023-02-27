//-----------------------------------------------------------------------
// <copyright file="MongoDbAllEventsSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2017 Akka.NET Contrib <https://github.com/AkkaNetContrib/Akka.Persistence.MongoDB>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Configuration;
using Akka.Persistence.MongoDb.Query;
using Akka.Persistence.Query;
using Akka.Persistence.TCK.Query;
using Akka.Util.Internal;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.MongoDb.Tests
{
    [Collection("MongoDbSpec")]
    public class MongoDbAllEventsSpec: AllEventsSpec, IClassFixture<DatabaseFixture>
    {
        private static Config CreateSpecConfig(DatabaseFixture databaseFixture, int id)
        {
            var s = databaseFixture.ConnectionString.Split('?');
            var connectionString = s[0] + $"{id}?" + s[1];
            // akka.test.single-expect-default = 10s
            var specString = @"
                akka.test.single-expect-default = 10s
                akka.persistence {
                    publish-plugin-commands = on
                    journal {
                        plugin = ""akka.persistence.journal.mongodb""
                        mongodb {
                            class = ""Akka.Persistence.MongoDb.Journal.MongoDbJournal, Akka.Persistence.MongoDb""
                            connection-string = """ + ConnectionString(databaseFixture, id) + @"""
                            auto-initialize = on
                            collection = ""EventJournal""
                        }
                    }
                    snapshot-store {
                        plugin = ""akka.persistence.snapshot-store.mongodb""
                        mongodb {
                            class = ""Akka.Persistence.MongoDb.Snapshot.MongoDbSnapshotStore, Akka.Persistence.MongoDb""
                            connection-string = """ + ConnectionString(databaseFixture, id) + @"""
                        }
                    }
                    query {
                        mongodb {
                            class = ""Akka.Persistence.MongoDb.Query.MongoDbReadJournalProvider, Akka.Persistence.MongoDb""
                            refresh-interval = 1s
                        }
                    }
                }";

            return ConfigurationFactory.ParseString(specString);
        }
        private static string ConnectionString(DatabaseFixture databaseFixture, int id)
        {
            var s = databaseFixture.ConnectionString.Split('?');
            var connectionString = s[0] + $"{id}?" + s[1];
            return connectionString;
        }
        public static readonly AtomicCounter Counter = new AtomicCounter(0);

        public MongoDbAllEventsSpec(ITestOutputHelper output, DatabaseFixture databaseFixture) : base(CreateSpecConfig(databaseFixture, Counter.GetAndIncrement()), "MongoDbAllEventsSpec", output)
        {
            ReadJournal = Sys.ReadJournalFor<MongoDbReadJournal>(MongoDbReadJournal.Identifier);
        }
    }
}
