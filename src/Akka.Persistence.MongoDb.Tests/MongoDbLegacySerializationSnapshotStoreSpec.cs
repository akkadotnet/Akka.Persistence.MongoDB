//-----------------------------------------------------------------------
// <copyright file="MongoDbSnapshotStoreSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Configuration;
using Akka.Persistence.TCK.Snapshot;
using Xunit;

namespace Akka.Persistence.MongoDb.Tests
{
    // TODO: enable this spec once https://github.com/akkadotnet/akka.net/pull/4190 is available via Akka.NET v1.4.0-beta5 or higher
    //[Collection("MongoDbSpec")]
    //public class MongoDbLegacySerializationSnapshotStoreSpec : SnapshotStoreSpec, IClassFixture<DatabaseFixture>
    //{
    //    protected override bool SupportsSerialization => false;

    //    public MongoDbLegacySerializationSnapshotStoreSpec(DatabaseFixture databaseFixture) : base(CreateSpecConfig(databaseFixture), "MongoDbSnapshotStoreSpec")
    //    {
    //        Initialize();
    //    }

    //    private static Config CreateSpecConfig(DatabaseFixture databaseFixture)
    //    {
    //        var specString = @"
    //            akka.test.single-expect-default = 3s
    //            akka.persistence {
    //                publish-plugin-commands = on
    //                snapshot-store {
    //                    plugin = ""akka.persistence.snapshot-store.mongodb""
    //                    mongodb {
    //                        class = ""Akka.Persistence.MongoDb.Snapshot.MongoDbSnapshotStore, Akka.Persistence.MongoDb""
    //                        connection-string = """ + databaseFixture.ConnectionString + @"""
    //                        auto-initialize = on
    //                        collection = ""SnapshotStore""
    //                        legacy-serialization = on
    //                    }
    //                }
    //            }";

    //        return ConfigurationFactory.ParseString(specString);
    //    }
    //}
}