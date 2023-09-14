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
using FluentAssertions;
using Xunit.Sdk;

namespace Akka.Persistence.MongoDb.Tests
{
    [Collection("MongoDbSpec")]
    public class MongoDbCurrentEventsByTagSpec : Akka.Persistence.TCK.Query.CurrentEventsByTagSpec, IClassFixture<DatabaseFixture>
    {
        public static readonly AtomicCounter Counter = new AtomicCounter(0);
        private readonly ITestOutputHelper _output;

        public MongoDbCurrentEventsByTagSpec(ITestOutputHelper output, DatabaseFixture databaseFixture)
            : base(CreateSpecConfig(databaseFixture, Counter.GetAndIncrement()), "MongoDbCurrentEventsByTagSpec", output)
        {
           
            _output = output;
            output.WriteLine(databaseFixture.MongoDbConnectionString(Counter.Current));
            ReadJournal = Sys.ReadJournalFor<MongoDbReadJournal>(MongoDbReadJournal.Identifier);

            var x = Sys.ActorOf(TestActor.Props("x"));
            x.Tell("warm-up");
            ExpectMsg("warm-up-done", TimeSpan.FromSeconds(10));
        }

        protected override bool SupportsTagsInEventEnvelope => true;

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
                            connection-string = """ + databaseFixture.MongoDbConnectionString(id) + @"""
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
                    snapshot-store {
                        plugin = ""akka.persistence.snapshot-store.mongodb""
                        mongodb {
                            class = ""Akka.Persistence.MongoDb.Snapshot.MongoDbSnapshotStore, Akka.Persistence.MongoDb""
                            connection-string = """ + databaseFixture.MongoDbConnectionString(id) + @"""
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

        // All of the unit tests below are copies of the original TCK methods
        // There was an incompatibility with FluentAssertion and the only way to make these unit tests work is by copying them over
        [Fact]
        public override void ReadJournal_query_CurrentEventsByTag_should_find_events_from_offset_exclusive()
        {
            if (ReadJournal is not ICurrentEventsByTagQuery queries)
                throw IsTypeException.ForMismatchedType(nameof(ICurrentEventsByTagQuery), ReadJournal?.GetType().Name ?? "null");

            var a = Sys.ActorOf(TestActor.Props("a"));
            var b = Sys.ActorOf(TestActor.Props("b"));
            var c = Sys.ActorOf(TestActor.Props("c"));

            a.Tell("hello");
            ExpectMsg("hello-done");
            a.Tell("a green apple");
            ExpectMsg("a green apple-done");
            b.Tell("a black car");
            ExpectMsg("a black car-done");
            a.Tell("something else");
            ExpectMsg("something else-done");
            a.Tell("a green banana");
            ExpectMsg("a green banana-done");
            b.Tell("a green leaf");
            ExpectMsg("a green leaf-done");
            c.Tell("a green cucumber");
            ExpectMsg("a green cucumber-done");

            var greenSrc1 = queries.CurrentEventsByTag("green", offset: Offset.NoOffset());
            var probe1 = greenSrc1.RunWith(this.SinkProbe<EventEnvelope>(), Materializer);
            probe1.Request(2);
            ExpectEnvelope(probe1, "a", 2L, "a green apple", "green");
            var offs = ExpectEnvelope(probe1, "a", 4L, "a green banana", "green").Offset;
            probe1.Cancel();

            var greenSrc = queries.CurrentEventsByTag("green", offset: offs);
            var probe2 = greenSrc.RunWith(this.SinkProbe<EventEnvelope>(), Materializer);
            probe2.Request(10);
            // note that banana is not included, since exclusive offset
            ExpectEnvelope(probe2, "b", 2L, "a green leaf", "green");
            probe2.Cancel();
        }

        [Fact]
        public override void ReadJournal_query_CurrentEventsByTag_should_find_existing_events()
        {
            if (ReadJournal is not ICurrentEventsByTagQuery queries)
                throw IsTypeException.ForMismatchedType(nameof(ICurrentEventsByTagQuery), ReadJournal?.GetType().Name ?? "null");

            var a = Sys.ActorOf(TestActor.Props("a"));
            var b = Sys.ActorOf(TestActor.Props("b"));

            a.Tell("hello");
            ExpectMsg("hello-done");
            a.Tell("a green apple");
            ExpectMsg("a green apple-done");
            b.Tell("a black car");
            ExpectMsg("a black car-done");
            a.Tell("something else");
            ExpectMsg("something else-done");
            a.Tell("a green banana");
            ExpectMsg("a green banana-done");
            b.Tell("a green leaf");
            ExpectMsg("a green leaf-done");

            var greenSrc = queries.CurrentEventsByTag("green", offset: Offset.NoOffset());
            var probe = greenSrc.RunWith(this.SinkProbe<EventEnvelope>(), Materializer);
            probe.Request(2);
            ExpectEnvelope(probe, "a", 2L, "a green apple", "green");
            ExpectEnvelope(probe, "a", 4L, "a green banana", "green");
            probe.ExpectNoMsg(TimeSpan.FromMilliseconds(500));
            probe.Request(2);
            ExpectEnvelope(probe, "b", 2L, "a green leaf", "green");
            probe.ExpectComplete();

            var blackSrc = queries.CurrentEventsByTag("black", offset: Offset.NoOffset());
            var probe2 = blackSrc.RunWith(this.SinkProbe<EventEnvelope>(), Materializer);
            probe2.Request(5);
            ExpectEnvelope(probe2, "b", 1L, "a black car", "black");
            probe2.ExpectComplete();

            var appleSrc = queries.CurrentEventsByTag("apple", offset: Offset.NoOffset());
            var probe3 = appleSrc.RunWith(this.SinkProbe<EventEnvelope>(), Materializer);
            probe3.Request(5);
            ExpectEnvelope(probe3, "a", 2L, "a green apple", "apple");
            probe3.ExpectComplete();
        }
        
        [Fact]
        public override void ReadJournal_query_CurrentEventsByTag_should_not_see_new_events_after_complete()
        {
            if (ReadJournal is not ICurrentEventsByTagQuery queries)
                throw IsTypeException.ForMismatchedType(nameof(ICurrentEventsByTagQuery), ReadJournal?.GetType().Name ?? "null");

            var a = Sys.ActorOf(TestActor.Props("a"));
            var b = Sys.ActorOf(TestActor.Props("b"));

            a.Tell("hello");
            ExpectMsg("hello-done");
            a.Tell("a green apple");
            ExpectMsg("a green apple-done");
            b.Tell("a black car");
            ExpectMsg("a black car-done");
            a.Tell("something else");
            ExpectMsg("something else-done");
            a.Tell("a green banana");
            ExpectMsg("a green banana-done");
            b.Tell("a green leaf");
            ExpectMsg("a green leaf-done");

            var c = Sys.ActorOf(TestActor.Props("c"));

            var greenSrc = queries.CurrentEventsByTag("green", offset: Offset.NoOffset());
            var probe = greenSrc.RunWith(this.SinkProbe<EventEnvelope>(), Materializer);
            probe.Request(2);
            ExpectEnvelope(probe, "a", 2L, "a green apple", "green");
            ExpectEnvelope(probe, "a", 4L, "a green banana", "green");
            probe.ExpectNoMsg(TimeSpan.FromMilliseconds(100));

            c.Tell("a green cucumber");
            ExpectMsg("a green cucumber-done");

            probe.ExpectNoMsg(TimeSpan.FromMilliseconds(100));
            probe.Request(5);
            ExpectEnvelope(probe, "b", 2L, "a green leaf", "green");
            probe.ExpectComplete(); // green cucumber not seen
        }
        
        [Fact]
        public override void ReadJournal_query_CurrentEventsByTag_should_see_all_150_events()
        {
            if (ReadJournal is not ICurrentEventsByTagQuery queries)
                throw IsTypeException.ForMismatchedType(nameof(ICurrentEventsByTagQuery), ReadJournal?.GetType().Name ?? "null");

            var a = Sys.ActorOf(TestActor.Props("a"));

            foreach (var _ in Enumerable.Range(1, 150))
            {
                a.Tell("a green apple");
                ExpectMsg("a green apple-done");
            }

            var greenSrc = queries.CurrentEventsByTag("green", offset: Offset.NoOffset());
            var probe = greenSrc.RunWith(this.SinkProbe<EventEnvelope>(), Materializer);
            probe.Request(150);
            foreach (var i in Enumerable.Range(1, 150))
            {
                ExpectEnvelope(probe, "a", i, "a green apple", "green");
            }

            probe.ExpectComplete();
            probe.ExpectNoMsg(TimeSpan.FromMilliseconds(500));
        }
        
        private EventEnvelope ExpectEnvelope(TestSubscriber.Probe<EventEnvelope> probe, string persistenceId, long sequenceNr, string @event, string tag)
        {
            var envelope = probe.ExpectNext<EventEnvelope>(_ => true);
            envelope.PersistenceId.Should().Be(persistenceId);
            envelope.SequenceNr.Should().Be(sequenceNr);
            envelope.Event.Should().Be(@event);
            if (SupportsTagsInEventEnvelope)
            {
                envelope.Tags.Should().NotBeNull();
                envelope.Tags.Should().Contain(tag);
            }
            return envelope;
        }
        
    }
}
