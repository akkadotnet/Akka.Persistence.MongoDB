using Akka.Configuration;
using Akka.Persistence.TCK.Snapshot;
using Xunit;

namespace Akka.Persistence.MongoDb.Tests;

[Collection("MongoDbSpec")]
public class MongoDbSnapshotStoreSaveSnapshotSpec: SnapshotStoreSaveSnapshotSpec, IClassFixture<DatabaseFixture>
{
    public MongoDbSnapshotStoreSaveSnapshotSpec(DatabaseFixture databaseFixture) 
        : base(CreateSpecConfig(databaseFixture), "MongoDbSnapshotStoreSpec")
    {
    }

    private static Config CreateSpecConfig(DatabaseFixture databaseFixture)
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
                                      use-write-transaction = on
                                      auto-initialize = on
                                      collection = "SnapshotStore"
                                  }
                              }
                           }
                           """;

        return ConfigurationFactory.ParseString(specString);
    }
}