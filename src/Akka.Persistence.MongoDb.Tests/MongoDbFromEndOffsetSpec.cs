//-----------------------------------------------------------------------
// <copyright file="MongoDbFromEndOffsetSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2017 Akka.NET Contrib <https://github.com/AkkaNetContrib/Akka.Persistence.MongoDB>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.MongoDb.Query;
using Akka.Persistence.Query;
using Akka.Persistence.TCK.Query;
using Akka.Streams.Dsl;
using Akka.Util.Internal;
using Xunit;

namespace Akka.Persistence.MongoDb.Tests
{
    public class MongoDbReadJournalConfigurationSpec
    {
        [Theory]
        [InlineData("", 50)]
        [InlineData("akka.persistence.journal.explicit", 65)]
        public void FromEnd_Ask_timeout_should_follow_selected_journal_call_timeout(
            string writePlugin,
            int expectedAskTimeoutSeconds)
        {
            var config = ConfigurationFactory.ParseString("""
akka.persistence.journal {
    plugin = "akka.persistence.journal.default"
    default.call-timeout = 45s
    explicit.call-timeout = 60s
}
""");

            var timeout = MongoDbReadJournal.ResolveFromEndAskTimeout(config, writePlugin);

            Assert.Equal(TimeSpan.FromSeconds(expectedAskTimeoutSeconds), timeout);
        }

        [Fact]
        public void FromEnd_Ask_timeout_should_preserve_infinite_journal_call_timeout()
        {
            var config = ConfigurationFactory.ParseString("""
akka.persistence.journal {
    plugin = "akka.persistence.journal.default"
    default.call-timeout = infinite
}
""");

            var timeout = MongoDbReadJournal.ResolveFromEndAskTimeout(config, "");

            Assert.Equal(Timeout.InfiniteTimeSpan, timeout);
        }
    }

    [Collection("MongoDbSpec")]
    public class MongoDbTransactionFromEndOffsetSpec : MongoDbFromEndOffsetSpecBase
    {
        public MongoDbTransactionFromEndOffsetSpec(
            ITestOutputHelper output,
            DatabaseFixture databaseFixture)
            : base(output, databaseFixture, useTransaction: true)
        {
        }
    }

    [Collection("MongoDbSpec")]
    public class MongoDbFromEndOffsetSpec : MongoDbFromEndOffsetSpecBase
    {
        public MongoDbFromEndOffsetSpec(
            ITestOutputHelper output,
            DatabaseFixture databaseFixture)
            : base(output, databaseFixture, useTransaction: false)
        {
        }
    }

    public abstract class MongoDbFromEndOffsetSpecBase : FromEndOffsetSpec, IClassFixture<DatabaseFixture>
    {
        private static readonly AtomicCounter Counter = new AtomicCounter(0);

        protected MongoDbFromEndOffsetSpecBase(
            ITestOutputHelper output,
            DatabaseFixture databaseFixture,
            bool useTransaction)
            : base(
                CreateSpecConfig(databaseFixture, Counter.GetAndIncrement(), useTransaction),
                "MongoDbFromEndOffsetSpec",
                output)
        {
            ReadJournal = Sys.ReadJournalFor<MongoDbReadJournal>(MongoDbReadJournal.Identifier);
        }

        [Fact]
        public async Task ReadJournal_FromEnd_should_resolve_again_for_each_materialization()
        {
            var queries = Assert.IsAssignableFrom<ICurrentAllEventsQuery>(ReadJournal);
            var source = queries.CurrentAllEvents(new FromEnd(2));
            var actor = Sys.ActorOf(JournalTestActor.Props("sequence-offsets"));

            for (var i = 1; i <= 3; i++)
            {
                actor.Tell($"event-{i}");
                await ExpectMsgAsync(
                    $"event-{i}-done",
                    cancellationToken: TestContext.Current.CancellationToken);
            }

            var firstMaterialization = await source
                .RunWith(Sink.Seq<EventEnvelope>(), Materializer);

            Assert.Collection(
                firstMaterialization,
                envelope =>
                {
                    Assert.Equal("event-2", envelope.Event);
                    Assert.IsType<Sequence>(envelope.Offset);
                },
                envelope =>
                {
                    Assert.Equal("event-3", envelope.Event);
                    Assert.IsType<Sequence>(envelope.Offset);
                });

            actor.Tell("event-4");
            await ExpectMsgAsync(
                "event-4-done",
                cancellationToken: TestContext.Current.CancellationToken);

            var resumeOffset = Assert.IsType<Sequence>(firstMaterialization[^1].Offset);
            var resumed = await queries.CurrentAllEvents(resumeOffset)
                .RunWith(Sink.Seq<EventEnvelope>(), Materializer);

            Assert.Collection(
                resumed,
                envelope =>
                {
                    Assert.Equal("event-4", envelope.Event);
                    Assert.IsType<Sequence>(envelope.Offset);
                });

            var secondMaterialization = await source
                .RunWith(Sink.Seq<EventEnvelope>(), Materializer);

            Assert.Collection(
                secondMaterialization,
                envelope =>
                {
                    Assert.Equal("event-3", envelope.Event);
                    Assert.IsType<Sequence>(envelope.Offset);
                },
                envelope =>
                {
                    Assert.Equal("event-4", envelope.Event);
                    Assert.IsType<Sequence>(envelope.Offset);
                });
        }

        private static Config CreateSpecConfig(
            DatabaseFixture databaseFixture,
            int id,
            bool useTransaction)
        {
            var specString = $$"""
akka.test.single-expect-default = 10s
akka.persistence {
    publish-plugin-commands = on
    journal {
        plugin = "akka.persistence.journal.mongodb"
        mongodb {
            class = "Akka.Persistence.MongoDb.Journal.MongoDbJournal, Akka.Persistence.MongoDb"
            connection-string = "{{databaseFixture.MongoDbConnectionString(id)}}"
            use-write-transaction = {{(useTransaction ? "on" : "off")}}
            use-read-transaction = {{(useTransaction ? "on" : "off")}}
            auto-initialize = on
            collection = "EventJournal"
            event-adapters {
                color-tagger = "Akka.Persistence.TCK.Query.ColorFruitTagger, Akka.Persistence.TCK"
            }
            event-adapter-bindings {
                "System.String" = color-tagger
            }
        }
    }
    snapshot-store {
        plugin = "akka.persistence.snapshot-store.mongodb"
        mongodb {
            class = "Akka.Persistence.MongoDb.Snapshot.MongoDbSnapshotStore, Akka.Persistence.MongoDb"
            connection-string = "{{databaseFixture.MongoDbConnectionString(id)}}"
            use-write-transaction = {{(useTransaction ? "on" : "off")}}
            use-read-transaction = {{(useTransaction ? "on" : "off")}}
        }
    }
    query {
        mongodb {
            class = "Akka.Persistence.MongoDb.Query.MongoDbReadJournalProvider, Akka.Persistence.MongoDb"
            refresh-interval = 1s
        }
    }
}
""";
            return ConfigurationFactory.ParseString(specString);
        }
    }
}
