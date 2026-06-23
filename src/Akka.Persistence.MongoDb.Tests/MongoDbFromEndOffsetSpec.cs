//-----------------------------------------------------------------------
// <copyright file="MongoDbFromEndOffsetSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2017 Akka.NET Contrib <https://github.com/AkkaNetContrib/Akka.Persistence.MongoDB>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Configuration;
using Akka.Persistence.MongoDb.Query;
using Akka.Persistence.Query;
using Akka.Persistence.TCK.Query;
using Akka.Util.Internal;
using Xunit;

namespace Akka.Persistence.MongoDb.Tests
{
    [Collection("MongoDbSpec")]
    public class MongoDbTransactionFromEndOffsetSpec : MongoDbFromEndOffsetSpecBase
    {
        public MongoDbTransactionFromEndOffsetSpec(ITestOutputHelper output, DatabaseFixture databaseFixture)
            : base(output, databaseFixture, useTransaction: true) { }
    }

    [Collection("MongoDbSpec")]
    public class MongoDbFromEndOffsetSpec : MongoDbFromEndOffsetSpecBase
    {
        public MongoDbFromEndOffsetSpec(ITestOutputHelper output, DatabaseFixture databaseFixture)
            : base(output, databaseFixture, useTransaction: false) { }
    }

    public abstract class MongoDbFromEndOffsetSpecBase : FromEndOffsetSpec, IClassFixture<DatabaseFixture>
    {
        private static readonly AtomicCounter Counter = new AtomicCounter(0);

        protected MongoDbFromEndOffsetSpecBase(ITestOutputHelper output, DatabaseFixture databaseFixture, bool useTransaction)
            : base(CreateSpecConfig(databaseFixture, Counter.GetAndIncrement(), useTransaction), "MongoDbFromEndOffsetSpec", output)
        {
            ReadJournal = Sys.ReadJournalFor<MongoDbReadJournal>(MongoDbReadJournal.Identifier);
        }

        private static Config CreateSpecConfig(DatabaseFixture databaseFixture, int id, bool useTransaction)
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
