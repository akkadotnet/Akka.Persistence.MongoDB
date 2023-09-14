//-----------------------------------------------------------------------
// <copyright file="MongoDbSnapshotStoreSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Xunit;
using Akka.Persistence.TCK.Snapshot;
using Akka.Configuration;

namespace Akka.Persistence.MongoDb.Tests
{
    [Collection("MongoDbSpec")]
    public class MongoDbTransactionSnapshotStoreSpec : MongoDbSnapshotStoreSpecBase
    {
        public MongoDbTransactionSnapshotStoreSpec(DatabaseFixture databaseFixture) : base(databaseFixture, true)
        {
        }
    }
    
    [Collection("MongoDbSpec")]
    public class MongoDbSnapshotStoreSpec : MongoDbSnapshotStoreSpecBase
    {
        public MongoDbSnapshotStoreSpec(DatabaseFixture databaseFixture) : base(databaseFixture, false)
        {
        }
    }
    
    public abstract class MongoDbSnapshotStoreSpecBase : SnapshotStoreSpec, IClassFixture<DatabaseFixture>
    {
        protected MongoDbSnapshotStoreSpecBase(DatabaseFixture databaseFixture, bool transaction) 
            : base(CreateSpecConfig(databaseFixture, transaction), "MongoDbSnapshotStoreSpec")
        {
            Initialize();
        }

        private static Config CreateSpecConfig(DatabaseFixture databaseFixture, bool transaction)
        {
            var specString = $$"""
akka.test.single-expect-default = 3s
akka.persistence {
   publish-plugin-commands = on
   snapshot-store {
       plugin = "akka.persistence.snapshot-store.mongodb"
       mongodb {
           class = "Akka.Persistence.MongoDb.Snapshot.MongoDbSnapshotStore, Akka.Persistence.MongoDb"
           connection-string = "{{databaseFixture.ConnectionString}}"
           use-write-transaction = {{(transaction ? "on" : "off")}}
           auto-initialize = on
           collection = "SnapshotStore"
       }
   }
}
""";

            return ConfigurationFactory.ParseString(specString);
        }
    }
}
