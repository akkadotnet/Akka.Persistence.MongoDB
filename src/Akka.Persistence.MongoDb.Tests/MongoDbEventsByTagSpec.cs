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
    public class MongoDbEventsByTagSpec : Akka.Persistence.TCK.Query.EventsByTagSpec, IClassFixture<DatabaseFixture>
    {
        public static readonly AtomicCounter Counter = new AtomicCounter(0);
        private readonly ITestOutputHelper _output;

        public MongoDbEventsByTagSpec(ITestOutputHelper output, DatabaseFixture databaseFixture)
            : base(CreateSpecConfig(databaseFixture, Counter.GetAndIncrement()), "MongoDbCurrentEventsByTagSpec", output)
        {
            _output = output;
            output.WriteLine(databaseFixture.ConnectionString + Counter.Current);
            ReadJournal = Sys.ReadJournalFor<MongoDbReadJournal>(MongoDbReadJournal.Identifier);

            var x = Sys.ActorOf(TestActor.Props("x"));
            x.Tell("warm-up");
            ExpectMsg("warm-up-done", TimeSpan.FromSeconds(10));
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
                            connection-string = """ + databaseFixture.ConnectionString + id + @"""
                            auto-initialize = on
                            collection = ""EventJournal""
                            event-adapters {
                                color-tagger  = ""Akka.Persistence.TCK.Query.ColorFruitTagger, Akka.Persistence.TCK""
                            }
                            event-adapter-bindings = {
                                ""System.String"" = color-tagger
                            }
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



        internal class TestActor : UntypedPersistentActor
        {
            public static Props Props(string persistenceId) => Actor.Props.Create(() => new TestActor(persistenceId));

            public sealed class DeleteCommand
            {
                public DeleteCommand(long toSequenceNr)
                {
                    ToSequenceNr = toSequenceNr;
                }

                public long ToSequenceNr { get; }
            }

            public TestActor(string persistenceId)
            {
                PersistenceId = persistenceId;
            }

            public override string PersistenceId { get; }

            protected override void OnRecover(object message)
            {
            }

            protected override void OnCommand(object message)
            {
                switch (message) {
                    case DeleteCommand delete:
                        DeleteMessages(delete.ToSequenceNr);
                        Sender.Tell($"{delete.ToSequenceNr}-deleted");
                        break;
                    case string cmd:
                        var sender = Sender;
                        Persist(cmd, e => sender.Tell($"{e}-done"));
                        break;
                }
            }
        }
    }


}
