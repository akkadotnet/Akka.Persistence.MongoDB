//-----------------------------------------------------------------------
// <copyright file="MongoDbSnapshotStoreSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Configuration;
using Akka.Persistence.TCK.Snapshot;
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

    protected override int SnapshotByteSizeLimit => 20 * 1024 * 1024;

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
}