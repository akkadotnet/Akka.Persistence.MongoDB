﻿using Akka.Configuration;
using Akka.Persistence.TCK.Serialization;
using Akka.Util.Internal;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.MongoDb.Tests.Serialization
{
    [Collection("MongoDbSpec")]
    public class MongoDbJournalBinarySerializationSpec : JournalSerializationSpec, IClassFixture<DatabaseFixture>
    {
        public static readonly AtomicCounter Counter = new AtomicCounter(0);
        private readonly ITestOutputHelper _output;

        public MongoDbJournalBinarySerializationSpec(ITestOutputHelper output, DatabaseFixture databaseFixture)
            : base(CreateSpecConfig(databaseFixture, Counter.GetAndIncrement()), nameof(MongoDbJournalBinarySerializationSpec), output)
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
                            stored-as = binary
                            event-adapters {
                                custom-adapter = ""Akka.Persistence.TCK.Serialization.TestJournal+MyWriteAdapter, Akka.Persistence.TCK""
                            }
                            event-adapter-bindings = {
                                ""Akka.Persistence.TCK.Serialization.TestJournal+MyPayload3, Akka.Persistence.TCK"" = custom-adapter
                            }
                        }
                    }
                }";

            return ConfigurationFactory.ParseString(specString);
        }
    }
}
