﻿//-----------------------------------------------------------------------
// <copyright file="MongoDbJournalSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Persistence.TCK.Journal;
using Xunit;
using Akka.Configuration;
using Akka.Persistence.MongoDb.Query;
using Akka.Persistence.Query;
using Xunit.Abstractions;
using Akka.Util.Internal;

namespace Akka.Persistence.MongoDb.Tests
{
    [Collection("MongoDbSpec")]
    public class MongoDbCurrentEventsByPersistenceIdsSpec : TCK.Query.CurrentEventsByPersistenceIdSpec, IClassFixture<DatabaseFixture>
    {
        public static readonly AtomicCounter Counter = new AtomicCounter(0);
        private static MongoDbConnectionString _mongoDb = new MongoDbConnectionString();
        private readonly ITestOutputHelper _output;

        public MongoDbCurrentEventsByPersistenceIdsSpec(ITestOutputHelper output, DatabaseFixture databaseFixture)
            : base(CreateSpecConfig(databaseFixture, Counter.GetAndIncrement()), "MongoDbCurrentEventsByPersistenceIdsSpec", output)
        {
            _output = output;
            output.WriteLine(_mongoDb.ConnectionString(databaseFixture, Counter.Current));
            ReadJournal = Sys.ReadJournalFor<MongoDbReadJournal>(MongoDbReadJournal.Identifier);
        }

        private static Config CreateSpecConfig(DatabaseFixture databaseFixture, int id)
        {
            var specString = @"
                akka.test.single-expect-default = 10s
                akka.persistence {
                    publish-plugin-commands = on
                    journal {
                        plugin = ""akka.persistence.journal.mongodb""
                        mongodb {
                            class = ""Akka.Persistence.MongoDb.Journal.MongoDbJournal, Akka.Persistence.MongoDb""
                            connection-string = """ + _mongoDb.ConnectionString(databaseFixture, id) +  @"""
                            auto-initialize = on
                            collection = ""EventJournal""
                        }
                    }
                    snapshot-store {
                        plugin = ""akka.persistence.snapshot-store.mongodb""
                        mongodb {
                            class = ""Akka.Persistence.MongoDb.Snapshot.MongoDbSnapshotStore, Akka.Persistence.MongoDb""
                            connection-string = """ + _mongoDb.ConnectionString(databaseFixture, id) +  @"""
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
        
    }


}
