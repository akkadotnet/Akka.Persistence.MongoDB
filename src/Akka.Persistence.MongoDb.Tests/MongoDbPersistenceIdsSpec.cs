//-----------------------------------------------------------------------
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
using System;
using Akka.Actor;
using Akka.Streams.TestKit;
using System.Linq;
using System.Diagnostics;

namespace Akka.Persistence.MongoDb.Tests
{
    [Collection("MongoDbSpec")]
    public class MongoDbPersistenceIdsSpec : Akka.Persistence.TCK.Query.PersistenceIdsSpec, IClassFixture<DatabaseFixture>
    {
        public static readonly AtomicCounter Counter = new AtomicCounter(0);
        private readonly ITestOutputHelper _output;

        public MongoDbPersistenceIdsSpec(ITestOutputHelper output, DatabaseFixture databaseFixture) 
            : base(CreateSpecConfig(databaseFixture, Counter.GetAndIncrement()), "MongoDbPersistenceIdsSpec", output)
        {
            _output = output;
            output.WriteLine(databaseFixture.ConnectionString + Counter.Current);
            ReadJournal = Sys.ReadJournalFor<MongoDbReadJournal>(MongoDbReadJournal.Identifier);
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
                            connection-string = """ + databaseFixture.ConnectionString + id + @"""
                            auto-initialize = on
                            collection = ""EventJournal""
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

        [Fact]
        public void ReadJournal_ConcurrentMessaging_should_work()
        {
            Enumerable.Range(1, 100).AsParallel().ForEach(_ => {
                Setup(Guid.NewGuid().ToString(), 1);
                Setup(Guid.NewGuid().ToString(), 1);
            });
        }

        private IActorRef Setup(string persistenceId, int n)
        {
            var sw = Stopwatch.StartNew();
            var pref = Sys.ActorOf(JournalTestActor.Props(persistenceId));
            for (int i = 1; i <= n; i++) {
                pref.Tell($"{persistenceId}-{i}");
                ExpectMsg($"{persistenceId}-{i}-done", TimeSpan.FromSeconds(10), $"{persistenceId}-{i}-done");
            }
            _output.WriteLine(sw.ElapsedMilliseconds.ToString());
            return pref;
        }

    }

    
}
