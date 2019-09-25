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
using System.Threading.Tasks;
using Akka.Streams.Dsl;
using System.Collections.Generic;
using System.Threading;
using Akka.Streams;
using FluentAssertions;

namespace Akka.Persistence.MongoDb.Tests
{
    /// <summary>
    /// Copied from https://github.com/akkadotnet/akka.net/blob/4f984bceffec20bc92beaf6f138f5a8f153c794b/src/core/Akka.Persistence.TCK/Query/TestActor.cs#L15
    /// since it's marked as `internal` at the moment
    /// </summary>
    internal class QueryTestActor : UntypedPersistentActor, IWithUnboundedStash
    {
        public static Props Props(string persistenceId) => Actor.Props.Create(() => new QueryTestActor(persistenceId));

        public sealed class DeleteCommand
        {
            public DeleteCommand(long toSequenceNr)
            {
                ToSequenceNr = toSequenceNr;
            }

            public long ToSequenceNr { get; }
        }

        public QueryTestActor(string persistenceId)
        {
            PersistenceId = persistenceId;
        }

        public override string PersistenceId { get; }

        protected override void OnRecover(object message)
        {
        }

        protected override void OnCommand(object message)
        {
            switch (message)
            {
                case DeleteCommand delete:
                    DeleteMessages(delete.ToSequenceNr);
                    Become(WhileDeleting(Sender)); // need to wait for delete ACK to return
                    break;
                case string cmd:
                    var sender = Sender;
                    Persist(cmd, e => sender.Tell($"{e}-done"));
                    break;
            }
        }

        protected Receive WhileDeleting(IActorRef originalSender)
        {
            return message =>
            {
                switch (message)
                {
                    case DeleteMessagesSuccess success:
                        originalSender.Tell($"{success.ToSequenceNr}-deleted");
                        Become(OnCommand);
                        Stash.UnstashAll();
                        break;
                    case DeleteMessagesFailure failure:
                        originalSender.Tell($"{failure.ToSequenceNr}-deleted-failed");
                        Become(OnCommand);
                        Stash.UnstashAll();
                        break;
                    default:
                        Stash.Stash();
                        break;
                }

                return true;
            };
        }

        [Collection("MongoDbSpec")]
        public class MongoDbEventsByPersistenceIdSpec : Akka.Persistence.TCK.Query.EventsByPersistenceIdSpec, IClassFixture<DatabaseFixture>
        {
            public static readonly AtomicCounter Counter = new AtomicCounter(0);
            private readonly ITestOutputHelper _output;

            public MongoDbEventsByPersistenceIdSpec(ITestOutputHelper output, DatabaseFixture databaseFixture)
                : base(CreateSpecConfig(databaseFixture, Counter.GetAndIncrement()), "MongoDbEventsByPersistenceIdSpec", output)
            {
                _output = output;
                output.WriteLine(databaseFixture.ConnectionString + Counter.Current);
                ReadJournal = Sys.ReadJournalFor<MongoDbReadJournal>(MongoDbReadJournal.Identifier);
            }

            private async Task<IReadOnlyList<(string persistentId, IActorRef actor)>> BigSetup(int numberOfPersistentActors, int numberOfEvents)
            {
                return await Source.From(Enumerable.Range(0, numberOfPersistentActors))
                    .Select(x => (x.ToString(), Sys.ActorOf(QueryTestActor.Props(x.ToString()))))
                    .SelectAsync(10, async x =>
                    {
                        var id = x.Item1;
                        var actor = x.Item2;
                        var tasks = new List<Task<string>>();
                        var expectedResponses = new HashSet<string>();
                        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                        foreach (var i in Enumerable.Range(0, numberOfEvents))
                        {
                            tasks.Add(actor.Ask<string>(id + $"-{i}", cts.Token));
                            expectedResponses.Add(id + $"-{i}-done");
                        }

                        var results = await Task.WhenAll(tasks);
                        Assert.True(expectedResponses.SetEquals(results));
                        return (id, actor);
                    })
                    .RunWith(Sink.Seq<(string persistentId, IActorRef actor)>(), Materializer);
            }

            [Theory]
            [InlineData(10, 50)]
            public async Task Bugfix76_EventsByPersistenceIdPublisher_stuck_during_replay(int persistentActors, int eventsPerActor)
            {
                var queries = ReadJournal.AsInstanceOf<ICurrentEventsByPersistenceIdQuery>();
                var pref = await BigSetup(persistentActors, eventsPerActor); // 50 actors, 50 events each

                Within(TimeSpan.FromSeconds(persistentActors / 2), () =>
                {
                    // launch all queries simultaneously
                    EventFilter.Debug(contains: "replay failed for persistenceId").Expect(0, async () =>
                    {
                        // start all queries at the same time
                        var qs = pref.Select(x =>
                        {
                            var q = queries.CurrentEventsByPersistenceId(x.persistentId, 0L, long.MaxValue)
                                .RunWith(Sink.Seq<EventEnvelope>(), Materializer);

                            return (x.persistentId, q);
                        });

                        var results = await Task.WhenAll(qs.Select(x => x.q));

                        foreach(var r in results)
                        {
                            var id = r.First().PersistenceId;
                            r.Count.Should().Be(eventsPerActor, $"Expected persistent entity [{id}] to recover [{eventsPerActor}] events");
                        }
                    });
                });
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

        }
    }
}
