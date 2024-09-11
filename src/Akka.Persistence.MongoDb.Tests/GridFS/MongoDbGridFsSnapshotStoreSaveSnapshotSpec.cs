using Akka.Configuration;
using Akka.Persistence.TCK.Snapshot;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.MongoDb.Tests.GridFS;

[Collection("MongoDbSpec")]
public class MongoDbGridFsSnapshotStoreSaveSnapshotSpec: SnapshotStoreSaveSnapshotSpec, IClassFixture<DatabaseFixture>
{
    public MongoDbGridFsSnapshotStoreSaveSnapshotSpec(DatabaseFixture databaseFixture, ITestOutputHelper output) 
        : base(CreateSpecConfig(databaseFixture), nameof(MongoDbGridFsSnapshotStoreSaveSnapshotSpec), output)
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
                                      class = "Akka.Persistence.MongoDb.Snapshot.MongoDbGridFsSnapshotStore, Akka.Persistence.MongoDb"
                                      connection-string = "{{databaseFixture.ConnectionString}}"
                                      use-write-transaction = off
                                      auto-initialize = on
                                      collection = "SnapshotStore"
                                  }
                              }
                           }
                           """;

        return ConfigurationFactory.ParseString(specString);
    }

}