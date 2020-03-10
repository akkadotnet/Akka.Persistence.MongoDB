using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Journal;
using Akka.Persistence.MongoDb.Query;
using Akka.Persistence.Query;
using Akka.Streams;
using Akka.Util.Internal;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.MongoDb.Tests
{
    [Collection("MongoDbSpec")]
    public class Bug61FixSpec : Akka.TestKit.Xunit2.TestKit, IClassFixture<DatabaseFixture>
    {
        public static readonly AtomicCounter Counter = new AtomicCounter(0);
        private readonly ITestOutputHelper _output;

        protected MongoDbReadJournal ReadJournal { get; }

        protected IMaterializer Materializer { get; }

        public class RealMsg
        {
            public RealMsg(string msg)
            {
                Msg = msg;
            }
            public string Msg { get; }
        }

        public const int MessageCount = 20;

        public Bug61FixSpec(ITestOutputHelper output, DatabaseFixture databaseFixture)
            : base(CreateSpecConfig(databaseFixture, Counter.GetAndIncrement()), "MongoDbCurrentEventsByTagSpec", output)
        {
            _output = output;
            output.WriteLine(databaseFixture.ConnectionString + Counter.Current);
            Materializer = Sys.Materializer();
            ReadJournal = Sys.ReadJournalFor<MongoDbReadJournal>(MongoDbReadJournal.Identifier);
        }

        /// <summary>
        /// Reproduction spec for https://github.com/akkadotnet/Akka.Persistence.MongoDB/issues/61
        /// </summary>
        [Fact]
        public async Task Bug61_Events_Recovered_By_Id_Should_Match_Tag()
        {
            var actor = Sys.ActorOf(TagActor.Props("x"));

            actor.Tell(MessageCount);
            ExpectMsg($"{MessageCount}-done", TimeSpan.FromSeconds(20));

            var eventsById = await ReadJournal.CurrentEventsByPersistenceId("x", 0L, long.MaxValue)
                .RunAggregate(ImmutableHashSet<EventEnvelope>.Empty, (agg, e) => agg.Add(e), Materializer);

            eventsById.Count.Should().Be(MessageCount);

            var eventsByTag = await ReadJournal.CurrentEventsByTag(typeof(RealMsg).Name)
                .RunAggregate(ImmutableHashSet<EventEnvelope>.Empty, (agg, e) => agg.Add(e), Materializer);

            eventsByTag.Count.Should().Be(MessageCount);

            eventsById.All(x => x.Event is RealMsg).Should().BeTrue("Expected all events by id to be RealMsg");
            eventsByTag.All(x => x.Event is RealMsg).Should().BeTrue("Expected all events by tag to be RealMsg");
        }

        /// <summary>
        /// Reproduction spec for https://github.com/akkadotnet/Akka.Persistence.MongoDB/issues/80
        /// </summary>
        [Fact]
        public void Bug80_CurrentEventsByTag_should_Recover_until_end()
        {
            var actor = Sys.ActorOf(TagActor.Props("y"));
            var msgCount = 1200;
            actor.Tell(msgCount);
            ExpectMsg($"{msgCount}-done", TimeSpan.FromSeconds(20));

            var eventsByTag = ReadJournal.CurrentEventsByTag(typeof(RealMsg).Name)
                .RunForeach(e => TestActor.Tell(e), Materializer);

            ReceiveN(msgCount);
        }

        /// <summary>
        /// Making sure EventsByTag didn't break during implementation of https://github.com/akkadotnet/Akka.Persistence.MongoDB/issues/80
        /// </summary>
        [Fact]
        public void Bug80_AllEventsByTag_should_Recover_all_messages()
        {
            var actor = Sys.ActorOf(TagActor.Props("y"));
            var msgCount = 1200;
            actor.Tell(msgCount);
            ExpectMsg($"{msgCount}-done", TimeSpan.FromSeconds(20));

            var eventsByTag = ReadJournal.EventsByTag(typeof(RealMsg).Name)
                .RunForeach(e => TestActor.Tell(e), Materializer);

            // can't do this because Offset isn't IComparable
            // ReceiveN(msgCount).Cast<EventEnvelope>().Select(x => x.Offset).Should().BeInAscendingOrder();
            ReceiveN(msgCount);

            // should receive more messages after the fact
            actor.Tell(msgCount);
            ExpectMsg($"{msgCount}-done", TimeSpan.FromSeconds(20));
            ReceiveN(msgCount);
        }
        
        private class TagActor : ReceivePersistentActor
        {
            public static Props Props(string id)
            {
                return Akka.Actor.Props.Create(() => new TagActor(id));
            }

            public TagActor(string id)
            {
                PersistenceId = id;

                Command<int>(i =>
                {
                    var msgs = new List<RealMsg>();
                    foreach (var n in Enumerable.Range(0, i))
                    {
                        msgs.Add(new RealMsg(i.ToString()));
                    }
                    PersistAll(msgs, m =>
                    {
                        if (LastSequenceNr >= i)
                        {
                            Sender.Tell($"{i}-done");
                        }
                    });
                });

                Command<RealMsg>(r =>
                {
                    Persist(r, e =>
                    {
                        Sender.Tell($"{e.Msg}-done");
                    });
                });
            }

            public override string PersistenceId { get; }
        }

        private class EventTagger : IWriteEventAdapter
        {
            public string DefaultTag { get; }

            public EventTagger()
            {
                DefaultTag = "accounts";
            }

            public string Manifest(object evt)
            {
                return string.Empty;
            }

            public object ToJournal(object evt)
            {
                return new Tagged(evt, ImmutableHashSet<string>.Empty.Add(DefaultTag).Add(evt.GetType().Name));
            }
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
                            event-adapters {
                                tagger  = """ + typeof(EventTagger).AssemblyQualifiedName + @"""
                            }
                            event-adapter-bindings = {
                                """ + typeof(RealMsg).AssemblyQualifiedName + @""" = tagger
                            }
                            stored-as = binary
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