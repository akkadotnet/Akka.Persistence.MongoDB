//-----------------------------------------------------------------------
// <copyright file="MongoDbJournalSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Persistence.TCK.Journal;
using Xunit;
using Akka.Configuration;

namespace Akka.Persistence.MongoDb.Tests
{
    [Collection("MongoDbSpec")]
    public class MongoDbTransactionJournalSpec : MongoDbJournalSpecBase
    {
        public MongoDbTransactionJournalSpec(DatabaseFixture databaseFixture) : base(databaseFixture, true)
        {
        }
    }
    
    [Collection("MongoDbSpec")]
    public class MongoDbJournalSpec : MongoDbJournalSpecBase
    {
        public MongoDbJournalSpec(DatabaseFixture databaseFixture) : base(databaseFixture, false)
        {
        }
    }
    
    public abstract class MongoDbJournalSpecBase : JournalSpec, IClassFixture<DatabaseFixture>
    {
        protected override bool SupportsRejectingNonSerializableObjects { get; } = false;

        protected MongoDbJournalSpecBase(DatabaseFixture databaseFixture, bool transaction) 
            : base(CreateSpecConfig(databaseFixture, transaction), "MongoDbJournalSpec")
        {
            Initialize();
        }

        private static Config CreateSpecConfig(DatabaseFixture databaseFixture, bool transaction)
        {
            var specString = $$"""
akka.test.single-expect-default = 3s
akka.persistence {
   publish-plugin-commands = on
   journal {
       plugin = "akka.persistence.journal.mongodb"
       mongodb {
           class = "Akka.Persistence.MongoDb.Journal.MongoDbJournal, Akka.Persistence.MongoDb"
           connection-string = "{{databaseFixture.ConnectionString}}"
           use-write-transaction = {{(transaction ? "on" : "off")}}
           auto-initialize = on
           collection = "EventJournal"
       }
   }
}
""";

            return ConfigurationFactory.ParseString(specString)
                .WithFallback(MongoDbPersistence.DefaultConfiguration());
        }
    }
}
