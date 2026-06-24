using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Hosting;
using Akka.Persistence.Hosting;
using Akka.Persistence.Journal;
using Akka.Persistence.MongoDb.Hosting;
using Akka.Persistence.MongoDb.Query;
using Akka.Persistence.Query;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Akka.Persistence.MongoDb.Tests.Hosting;

/// <summary>
/// Regression test for https://github.com/akkadotnet/Akka.Persistence.Sql/issues/552
/// Verifies that event adapters configured via the NEW callback API are actually invoked at runtime
/// by checking that events are tagged and appear in EventsByTag queries.
/// </summary>
public class EventAdapterRuntimeInvocationSpecs : Akka.Hosting.TestKit.TestKit, IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;
    private readonly ITestOutputHelper _output;

    public EventAdapterRuntimeInvocationSpecs(ITestOutputHelper output, DatabaseFixture fixture)
    {
        _output = output;
        _fixture = fixture;
    }

    #region Test Events and Actors

    public sealed class TestEvent
    {
        public TestEvent(string data) { Data = data; }
        public string Data { get; }
    }

    /// <summary>
    /// Event adapter that tags TestEvent instances
    /// </summary>
    public sealed class TestEventTagger : IWriteEventAdapter
    {
        public TestEventTagger(ExtendedActorSystem system) { }

        public string Manifest(object evt) => string.Empty;

        public object ToJournal(object evt)
        {
            return evt switch
            {
                TestEvent => new Tagged(evt, new[] { "test-tag" }),
                _ => evt
            };
        }
    }

    public sealed class TestPersistentActor : ReceivePersistentActor
    {
        private readonly List<string> _events = new();

        public sealed class SaveEvent
        {
            public SaveEvent(string data) { Data = data; }
            public string Data { get; }
        }

        public sealed class GetEvents
        {
            public static readonly GetEvents Instance = new();
            private GetEvents() { }
        }

        public TestPersistentActor(string persistenceId)
        {
            PersistenceId = persistenceId;

            Command<SaveEvent>(cmd =>
            {
                var evt = new TestEvent(cmd.Data);
                Persist(evt, _ =>
                {
                    _events.Add(cmd.Data);
                    Sender.Tell("OK");
                });
            });

            Command<GetEvents>(_ =>
            {
                Sender.Tell(_events.ToArray());
            });

            Recover<TestEvent>(evt =>
            {
                _events.Add(evt.Data);
            });
        }

        public override string PersistenceId { get; }
    }

    #endregion

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.WithMongoDbPersistence(
            connectionString: _fixture.ConnectionString,
            journalBuilder: journal =>
            {
                journal.AddWriteEventAdapter<TestEventTagger>(
                    "test-tagger",
                    new[] { typeof(TestEvent) });
                journal.WithHealthCheck(HealthStatus.Degraded);
            },
            snapshotBuilder: snapshot =>
            {
                snapshot.WithHealthCheck(HealthStatus.Degraded);
            });
    }

    [Fact]
    public async Task EventAdapter_Should_Tag_Events_And_Appear_In_EventsByTag_Query()
    {
        // Verify adapter is in HOCON configuration
        var config = Sys.Settings.Config;
        var journalConfig = config.GetConfig("akka.persistence.journal.mongodb");

        _output.WriteLine("=== HOCON Configuration ===");
        _output.WriteLine(journalConfig.ToString());

        Assert.True(journalConfig.HasPath("event-adapters"));
        Assert.True(journalConfig.HasPath("event-adapter-bindings"));

        // Create persistent actor
        var actor = Sys.ActorOf(Props.Create(() => new TestPersistentActor("test-1")));

        // Persist 3 events
        await actor.Ask<string>(new TestPersistentActor.SaveEvent("event-1"), TimeSpan.FromSeconds(3));
        await actor.Ask<string>(new TestPersistentActor.SaveEvent("event-2"), TimeSpan.FromSeconds(3));
        await actor.Ask<string>(new TestPersistentActor.SaveEvent("event-3"), TimeSpan.FromSeconds(3));

        // CRITICAL: Use Persistence Query to verify events were tagged
        var queries = Sys.ReadJournalFor<MongoDbReadJournal>(MongoDbReadJournal.Identifier);
        var materializer = Sys.Materializer();

        await AwaitAssertAsync(async () =>
        {
            var taggedEvents = await queries
                .CurrentEventsByTag("test-tag", Offset.NoOffset())
                .RunWith(Sink.Seq<EventEnvelope>(), materializer);

            _output.WriteLine($"Found {taggedEvents.Count()} events with tag 'test-tag'");

            Assert.Equal(3, taggedEvents.Count());

            // Verify the events are the correct type
            Assert.True(taggedEvents.All(e => e.Event is TestEvent));
        });
    }
}
