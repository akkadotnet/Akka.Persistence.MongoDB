using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.MongoDb.Query;
using Akka.Persistence.Query;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util.Internal;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.MongoDb.Tests
{
    [Collection("MongoDbSpec")]
    public class BugFix76Spec : Akka.TestKit.Xunit2.TestKit, IClassFixture<DatabaseFixture>
    {
        public IReadJournal ReadJournal { get; }
        public ActorMaterializer Materializer => Sys.Materializer();
        public static readonly AtomicCounter Counter = new AtomicCounter(0);
        private readonly ITestOutputHelper _helper;
        public BugFix76Spec(ITestOutputHelper helper, DatabaseFixture fixture)
            : base(CreateSpecConfig(fixture, Counter.Current), output: helper)
        {
            ReadJournal = Sys.ReadJournalFor<MongoDbReadJournal>(MongoDbReadJournal.Identifier);
            _helper = helper;
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
                    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(numberOfPersistentActors / 2));
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
        [InlineData(500,50)]
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

                    foreach (var r in results)
                    {
                        var id = r.First().PersistenceId;
                        r.Count.Should().Be(eventsPerActor, $"Expected persistent entity [{id}] to recover [{eventsPerActor}] events");
                    }

                    Sys.Log.Info("Processed {0} events across {1} entities", results.Sum(x => x.Count), results.Length);
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
