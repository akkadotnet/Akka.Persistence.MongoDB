using System.Collections.Generic;
using Akka.Configuration;
using Akka.Persistence.TCK.Serialization;
using Akka.Util.Internal;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.MongoDb.Tests.Serialization
{
    [Collection("MongoDbSpec")]
    public class MongoDbJournalSerializationSpec : JournalSerializationSpec, IClassFixture<DatabaseFixture>
    {
        public static readonly AtomicCounter Counter = new AtomicCounter(0);
        private readonly ITestOutputHelper _output;

        public MongoDbJournalSerializationSpec(ITestOutputHelper output, DatabaseFixture databaseFixture)
            : base(CreateSpecConfig(databaseFixture, Counter.GetAndIncrement()), nameof(MongoDbJournalSerializationSpec), output)
        {
            _output = output;
            output.WriteLine(databaseFixture.ConnectionString + Counter.Current);
        }

        private static Config CreateSpecConfig(DatabaseFixture databaseFixture, int id)
        {
            var specString = @"
                akka.test.single-expect-default = 3s
                akka.persistence {
                    publish-plugin-commands = on
                    journal {
                        plugin = ""akka.persistence.journal.mongodb""
                        mongodb {
                            class = ""Akka.Persistence.MongoDb.Journal.MongoDbJournal, Akka.Persistence.MongoDb""
                            connection-string = """ + databaseFixture.ConnectionString + @"""
                            auto-initialize = on
                            collection = ""EventJournal""
                        }
                    }
                }";

            return ConfigurationFactory.ParseString(specString)
                .WithFallback(MongoDbPersistence.DefaultConfiguration());
        }


        [Fact(Skip = "Waiting on better error messages")]
        public override void Journal_should_serialize_Persistent_with_EventAdapter_manifest()
        {
            base.Journal_should_serialize_Persistent_with_EventAdapter_manifest();
        }
    }
}
