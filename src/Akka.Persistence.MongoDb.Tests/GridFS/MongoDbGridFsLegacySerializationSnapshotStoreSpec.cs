//-----------------------------------------------------------------------
// <copyright file="MongoDbSnapshotStoreSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Event;
using Akka.Persistence.TCK.Snapshot;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

#nullable enable
namespace Akka.Persistence.MongoDb.Tests.GridFS;

[Collection("MongoDbSpec")]
public class MongoDbGridFsLegacySerializationSnapshotStoreSpec : SnapshotStoreSpec, IClassFixture<DatabaseFixture>
{
    protected override bool SupportsSerialization => false;

    public MongoDbGridFsLegacySerializationSnapshotStoreSpec(DatabaseFixture databaseFixture, ITestOutputHelper output) 
        : base(CreateSpecConfig(databaseFixture), nameof(MongoDbGridFsLegacySerializationSnapshotStoreSpec), output)
    {
        Initialize();
    }

    protected override int SnapshotByteSizeLimit => 128 * 1024 * 1024;

    private static Config CreateSpecConfig(DatabaseFixture databaseFixture)
    {
        var specString = @"
                akka.test.single-expect-default = 3s
                akka.persistence {
                    publish-plugin-commands = on
                    snapshot-store {
                        plugin = ""akka.persistence.snapshot-store.mongodb""
                        mongodb {
                            class = ""Akka.Persistence.MongoDb.Snapshot.MongoDbGridFsSnapshotStore, Akka.Persistence.MongoDb""
                            connection-string = """ + databaseFixture.ConnectionString + @"""
                            auto-initialize = on
                            collection = ""SnapshotStore""
                            legacy-serialization = on
                        }
                    }
                }";

        return ConfigurationFactory.ParseString(specString)
            .WithFallback(MongoDbPersistence.DefaultConfiguration());
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

        var stopwatch = Stopwatch.StartNew();
        SnapshotStore.Tell(
            new LoadSnapshot(Pid, new SnapshotSelectionCriteria(saved.Metadata.SequenceNr), long.MaxValue), 
            senderProbe.Ref);
        var loaded = await senderProbe.ExpectMsgAsync<LoadSnapshotResult>();
        stopwatch.Stop();
        Log.Info($"{SnapshotByteSizeLimit} bytes snapshot loaded in {stopwatch.Elapsed.Milliseconds} milliseconds");
        
        MD5.Create().ComputeHash((byte[])loaded.Snapshot.Snapshot).Should()
            .BeEquivalentTo(MD5.Create().ComputeHash(bigSnapshot));
    }
}