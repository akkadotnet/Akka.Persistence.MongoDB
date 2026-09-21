//-----------------------------------------------------------------------
// <copyright file="MongoDbSnapshotStoreSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Akka.Persistence.TCK.Snapshot;
using Akka.Serialization;
using Xunit;

#nullable enable
namespace Akka.Persistence.MongoDb.Tests.GridFS;

[Collection("MongoDbSpec")]
public class MongoDbGridFsSnapshotStoreSpec : SnapshotStoreSpec, IClassFixture<DatabaseFixture>
{
    public MongoDbGridFsSnapshotStoreSpec(DatabaseFixture databaseFixture, ITestOutputHelper output) 
        : base(CreateSpecConfig(databaseFixture), nameof(MongoDbGridFsSnapshotStoreSpec), output)
    {
        Initialize();
    }

    protected override int SnapshotByteSizeLimit => 128 * 1024 * 1024;

    private static Config CreateSpecConfig(DatabaseFixture databaseFixture)
    {
        var specString = $$"""
                           akka.test.single-expect-default = 60s
                           akka.actor {
                              serializers {
                                 blocking-snapshot = "Akka.Persistence.MongoDb.Tests.GridFS.BlockingSnapshotSerializer, Akka.Persistence.MongoDb.Tests"
                              }
                              serialization-bindings {
                                 "Akka.Persistence.MongoDb.Tests.GridFS.BlockingSnapshotPayload, Akka.Persistence.MongoDb.Tests" = blocking-snapshot
                              }
                           }
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
                                      # 128 MB GridFS writes are slow on Windows CI; override plugin and
                                      # circuit-breaker timeouts so they don't fire before the operation completes
                                      call-timeout = 60s
                                      circuit-breaker {
                                          max-failures = 5
                                          call-timeout = 60s
                                          reset-timeout = 60s
                                      }
                                  }
                              }
                           }
                           """;

        return ConfigurationFactory.ParseString(specString);
    }
    
    [Fact]
    public async Task SnapshotStore_should_save_bigger_size_snapshot_consistently()
    {
        var metadata = new SnapshotMetadata(Pid, 100, DateTime.MinValue);
        var bigSnapshot = new byte[SnapshotByteSizeLimit];
        Random.Shared.NextBytes(bigSnapshot);
        var senderProbe = CreateTestProbe();
        SnapshotStore.Tell(new SaveSnapshot(metadata, bigSnapshot), senderProbe.Ref);
        var saved = await senderProbe.ExpectMsgAsync<SaveSnapshotSuccess>(TimeSpan.FromSeconds(60));

        var stopwatch = Stopwatch.StartNew();
        SnapshotStore.Tell(
            new LoadSnapshot(Pid, new SnapshotSelectionCriteria(saved.Metadata.SequenceNr), long.MaxValue),
            senderProbe.Ref);
        var loaded = await senderProbe.ExpectMsgAsync<LoadSnapshotResult>(TimeSpan.FromSeconds(60));
        stopwatch.Stop();
        Log.Info($"{SnapshotByteSizeLimit} bytes snapshot loaded in {stopwatch.Elapsed.Milliseconds} milliseconds");

        Assert.Equal(MD5.Create().ComputeHash(bigSnapshot), MD5.Create().ComputeHash((byte[])loaded.Snapshot.Snapshot));
    }

    [Fact]
    public async Task SnapshotStore_should_load_another_snapshot_while_a_snapshot_is_being_serialized()
    {
        BlockingSnapshotSerializer.Reset();

        var saveProbe = CreateTestProbe();
        var loadProbe = CreateTestProbe();
        var saveMetadata = new SnapshotMetadata($"blocking-save-{Guid.NewGuid():N}", 1, DateTime.MinValue);

        SnapshotStore.Tell(new SaveSnapshot(saveMetadata, new BlockingSnapshotPayload()), saveProbe.Ref);

        var serializationStarted = await Task.Run(() =>
            BlockingSnapshotSerializer.SerializationStarted.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(serializationStarted, "the snapshot serializer should have started");

        try
        {
            SnapshotStore.Tell(
                new LoadSnapshot(
                    $"concurrent-load-{Guid.NewGuid():N}",
                    SnapshotSelectionCriteria.Latest,
                    long.MaxValue),
                loadProbe.Ref);

            var loaded = await loadProbe.ExpectMsgAsync<LoadSnapshotResult>(TimeSpan.FromSeconds(5));
            Assert.Null(loaded.Snapshot);
        }
        finally
        {
            BlockingSnapshotSerializer.AllowSerializationToComplete.Set();
        }

        await saveProbe.ExpectMsgAsync<SaveSnapshotSuccess>(TimeSpan.FromSeconds(10));
    }
}

public sealed class BlockingSnapshotPayload
{
}

public sealed class BlockingSnapshotSerializer : Serializer
{
    public static readonly ManualResetEventSlim SerializationStarted = new(false);
    public static readonly ManualResetEventSlim AllowSerializationToComplete = new(false);

    public BlockingSnapshotSerializer(ExtendedActorSystem system) : base(system)
    {
    }

    public override int Identifier => 771001;

    public override bool IncludeManifest => false;

    public static void Reset()
    {
        SerializationStarted.Reset();
        AllowSerializationToComplete.Reset();
    }

    public override byte[] ToBinary(object obj)
    {
        SerializationStarted.Set();
        if (!AllowSerializationToComplete.Wait(TimeSpan.FromSeconds(30)))
            throw new TimeoutException("Timed out waiting for the test to release snapshot serialization.");

        return Array.Empty<byte>();
    }

    public override object FromBinary(byte[] bytes, Type type)
    {
        return new BlockingSnapshotPayload();
    }
}