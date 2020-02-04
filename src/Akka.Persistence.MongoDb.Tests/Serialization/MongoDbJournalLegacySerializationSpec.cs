// -----------------------------------------------------------------------
// <copyright file="MongoDbJournalLegacySerializationSpec.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2020 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using Akka.Configuration;
using Akka.Persistence.TCK.Serialization;
using Akka.Util.Internal;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.MongoDb.Tests.Serialization
{
    [Collection("MongoDbSpec")]
    public class MongoDbJournalLegacySerializationSpec : JournalSerializationSpec, IClassFixture<DatabaseFixture>
    {
        public static readonly AtomicCounter Counter = new AtomicCounter(0);
        private readonly ITestOutputHelper _output;

        public MongoDbJournalLegacySerializationSpec(ITestOutputHelper output, DatabaseFixture databaseFixture)
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
                            legacy-serialization = on
                        }
                    }
                }";

            return ConfigurationFactory.ParseString(specString);
        }


        [Fact(Skip = "Waiting on better error messages")]
        public override void Journal_should_serialize_Persistent_with_EventAdapter_manifest()
        {
            base.Journal_should_serialize_Persistent_with_EventAdapter_manifest();
        }
    }
}