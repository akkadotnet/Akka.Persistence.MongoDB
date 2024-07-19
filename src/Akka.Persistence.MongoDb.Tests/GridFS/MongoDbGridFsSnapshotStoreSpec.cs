//-----------------------------------------------------------------------
// <copyright file="MongoDbSnapshotStoreSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.TCK.Snapshot;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

#nullable enable
namespace Akka.Persistence.MongoDb.Tests.GridFS;

[Collection("MongoDbSpec")]
public class MongoDbGridFsTransactionSnapshotStoreSpec : MongoDbGridFsSnapshotStoreSpecBase
{
    public MongoDbGridFsTransactionSnapshotStoreSpec(DatabaseFixture databaseFixture, ITestOutputHelper output) : base(databaseFixture, true, output)
    {
    }
}

[Collection("MongoDbSpec")]
public class MongoDbGridFsSnapshotStoreSpec : MongoDbGridFsSnapshotStoreSpecBase
{
    public MongoDbGridFsSnapshotStoreSpec(DatabaseFixture databaseFixture, ITestOutputHelper output) : base(databaseFixture, false, output)
    {
    }
}

public abstract class MongoDbGridFsSnapshotStoreSpecBase : SnapshotStoreSpec, IClassFixture<DatabaseFixture>
{
    protected MongoDbGridFsSnapshotStoreSpecBase(DatabaseFixture databaseFixture, bool transaction, ITestOutputHelper output) 
        : base(CreateSpecConfig(databaseFixture, transaction), nameof(MongoDbGridFsSnapshotStoreSpecBase), output)
    {
        Initialize();
    }

    protected override int SnapshotByteSizeLimit => 18 * 1024 * 1024;

    private static Config CreateSpecConfig(DatabaseFixture databaseFixture, bool transaction)
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
                                      use-write-transaction = {{(transaction ? "on" : "off")}}
                                      auto-initialize = on
                                      collection = "SnapshotStore"
                                  }
                              }
                           }
                           """;

        return ConfigurationFactory.ParseString(specString);
    }
    
    [Fact]
    public async Task SnapshotStore_should_save_bigger_size_snapshot_consistently()
    {
        var metadata = new SnapshotMetadata(Pid, 100);
        var bigSnapshot = new byte[SnapshotByteSizeLimit];
        new Random().NextBytes(bigSnapshot);
        var senderProbe = CreateTestProbe();
        SnapshotStore.Tell(new SaveSnapshot(metadata, bigSnapshot), senderProbe.Ref);
        var saved = await senderProbe.ExpectMsgAsync<SaveSnapshotSuccess>();

        SnapshotStore.Tell(
            new LoadSnapshot(Pid, new SnapshotSelectionCriteria(saved.Metadata.SequenceNr), long.MaxValue), 
            senderProbe.Ref);
        var loaded = await senderProbe.ExpectMsgAsync<LoadSnapshotResult>();
        ((byte[])loaded.Snapshot.Snapshot).Should().BeEquivalentTo(bigSnapshot, opt => opt.WithStrictOrdering());
    }
}