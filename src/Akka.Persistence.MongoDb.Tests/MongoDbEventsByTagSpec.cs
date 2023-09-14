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
using FluentAssertions;
using Xunit.Sdk;

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
                akka.test.single-expect-default = 10s
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
        public override void ReadJournal_live_query_EventsByTag_should_find_events_from_offset_exclusive()
        {
            if (ReadJournal is not IEventsByTagQuery queries)
                throw IsTypeException.ForMismatchedType(nameof(IEventsByTagQuery), ReadJournal?.GetType().Name ?? "null");

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

            var greenSrc1 = queries.EventsByTag("green", offset: Offset.NoOffset());
            var probe1 = greenSrc1.RunWith(this.SinkProbe<EventEnvelope>(), Materializer);
            probe1.Request(2);
            ExpectEnvelope(probe1, "a", 2L, "a green apple", "green");
            var offs = ExpectEnvelope(probe1, "a", 4L, "a green banana", "green").Offset;
            probe1.Cancel();

            var greenSrc2 = queries.EventsByTag("green", offset: offs);
            var probe2 = greenSrc2.RunWith(this.SinkProbe<EventEnvelope>(), Materializer);
            probe2.Request(10);
            ExpectEnvelope(probe2, "b", 2L, "a green leaf", "green");
            ExpectEnvelope(probe2, "c", 1L, "a green cucumber", "green");
            probe2.ExpectNoMsg(TimeSpan.FromMilliseconds(100));
            probe2.Cancel();
        }

        [Fact]
        public override void ReadJournal_live_query_EventsByTag_should_find_new_events()
        {
            if (ReadJournal is not IEventsByTagQuery queries)
                throw IsTypeException.ForMismatchedType(nameof(IEventsByTagQuery), ReadJournal?.GetType().Name ?? "null");

            var b = Sys.ActorOf(TestActor.Props("b"));
            var d = Sys.ActorOf(TestActor.Props("d"));

            b.Tell("a black car");
            ExpectMsg("a black car-done");

            var blackSrc = queries.EventsByTag("black", offset: Offset.NoOffset());
            var probe = blackSrc.RunWith(this.SinkProbe<EventEnvelope>(), Materializer);
            probe.Request(2);
            ExpectEnvelope(probe, "b", 1L, "a black car", "black");
            probe.ExpectNoMsg(TimeSpan.FromMilliseconds(100));

            d.Tell("a black dog");
            ExpectMsg("a black dog-done");
            d.Tell("a black night");
            ExpectMsg("a black night-done");

            ExpectEnvelope(probe, "d", 1L, "a black dog", "black");
            probe.ExpectNoMsg(TimeSpan.FromMilliseconds(100));
            probe.Request(10);
            ExpectEnvelope(probe, "d", 2L, "a black night", "black");
            probe.Cancel();
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
