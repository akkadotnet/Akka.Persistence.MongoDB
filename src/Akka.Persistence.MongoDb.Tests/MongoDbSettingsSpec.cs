//-----------------------------------------------------------------------
// <copyright file="MongoDbSettingsSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using FluentAssertions;
using Xunit;

namespace Akka.Persistence.MongoDb.Tests
{
    [Collection("MongoDbSpec")]
    public class MongoDbSettingsSpec : Akka.TestKit.Xunit2.TestKit
    {
        [Fact]
        public void Mongo_JournalSettings_must_have_default_values()
        {
            var mongoPersistence = MongoDbPersistence.Get(Sys);

            mongoPersistence.JournalSettings.ConnectionString.Should().Be(string.Empty);
            mongoPersistence.JournalSettings.AutoInitialize.Should().BeFalse();
            mongoPersistence.JournalSettings.Collection.Should().Be("EventJournal");
            mongoPersistence.JournalSettings.MetadataCollection.Should().Be("Metadata");
        }

        [Fact]
        public void Mongo_SnapshotStoreSettingsSettings_must_have_default_values()
        {
            var mongoPersistence = MongoDbPersistence.Get(Sys);

            mongoPersistence.SnapshotStoreSettings.ConnectionString.Should().Be(string.Empty);
            mongoPersistence.SnapshotStoreSettings.AutoInitialize.Should().BeFalse();
            mongoPersistence.SnapshotStoreSettings.Collection.Should().Be("SnapshotStore");
        }
    }
}
