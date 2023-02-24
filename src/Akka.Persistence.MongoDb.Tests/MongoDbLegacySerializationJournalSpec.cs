﻿//-----------------------------------------------------------------------
// <copyright file="MongoDbJournalSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2019 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2019 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Configuration;
using Akka.Persistence.TCK.Journal;
using Xunit;

namespace Akka.Persistence.MongoDb.Tests
{
    [Collection("MongoDbSpec")]
    public class MongoDbLegacySerializationJournalSpec : JournalSpec, IClassFixture<DatabaseFixture>
    {
        protected override bool SupportsRejectingNonSerializableObjects { get; } = false;

        protected override bool SupportsSerialization => false;

        public MongoDbLegacySerializationJournalSpec(DatabaseFixture databaseFixture) : base(CreateSpecConfig(databaseFixture), "MongoDbJournalSpec")
        {
            Initialize();
        }

        private static Config CreateSpecConfig(DatabaseFixture databaseFixture)
        {
            var s = databaseFixture.ConnectionString.Split('?');
            var connectionString = s[0] + $"?" + s[1];
            var specString = @"
                akka.test.single-expect-default = 3s
                akka.persistence {
                    publish-plugin-commands = on
                    journal {
                        plugin = ""akka.persistence.journal.mongodb""
                        mongodb {
                            class = ""Akka.Persistence.MongoDb.Journal.MongoDbJournal, Akka.Persistence.MongoDb""
                            connection-string = """ + connectionString + @"""
                            auto-initialize = on
                            collection = ""EventJournal""
                            legacy-serialization = on
                        }
                    }
                }";

            return ConfigurationFactory.ParseString(specString)
                .WithFallback(MongoDbPersistence.DefaultConfiguration());
        }
    }
}