using Akka.Configuration;
using Akka.Persistence.TestKit.Performance;
using Akka.Util.Internal;
using System;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.MongoDb.Tests
{
    [Collection("MongoDbSpec")]
    public class MongoDbJournalPerfSpec: JournalPerfSpec, IClassFixture<DatabaseFixture>
    {
        private static Config CreateSpecConfig(DatabaseFixture databaseFixture, int id)
        {
            var s = databaseFixture.ConnectionString.Split('?');
            var connectionString = s[0] + $"{id}?" + s[1];
            // akka.test.single-expect-default = 10s
            var specString = @"
                akka.persistence {
                    publish-plugin-commands = on
                    journal {
                        plugin = ""akka.persistence.journal.mongodb""
                        mongodb {
                            class = ""Akka.Persistence.MongoDb.Journal.MongoDbJournal, Akka.Persistence.MongoDb""
                            connection-string = """ + connectionString + @"""
                            auto-initialize = on
                            collection = ""EventJournal""
                        }
                    }
                }";

            return ConfigurationFactory.ParseString(specString)
                .WithFallback(MongoDbPersistence.DefaultConfiguration());
        }
        public static readonly AtomicCounter Counter = new AtomicCounter(0);

        public MongoDbJournalPerfSpec(ITestOutputHelper output, DatabaseFixture databaseFixture) : base(CreateSpecConfig(databaseFixture, Counter.GetAndIncrement()), "MongoDbJournalPerfSpec", output)
        {
            EventsCount = 1000;
            ExpectDuration = TimeSpan.FromMinutes(10);
            MeasurementIterations = 1;
        }
    }
}
