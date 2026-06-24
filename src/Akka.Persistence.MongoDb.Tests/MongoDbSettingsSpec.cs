using System;
using Xunit;

namespace Akka.Persistence.MongoDb.Tests
{
    [Collection("MongoDbSpec")]
    public class MongoDbSettingsSpec : Akka.TestKit.Xunit.TestKit
    {
        [Fact]
        public void Mongo_JournalSettings_must_have_default_values()
        {
            var mongoPersistence = MongoDbPersistence.Get(Sys);

            Assert.Equal(string.Empty, mongoPersistence.JournalSettings.ConnectionString);
            Assert.True(mongoPersistence.JournalSettings.AutoInitialize);
            Assert.Equal("EventJournal", mongoPersistence.JournalSettings.Collection);
            Assert.Equal("Metadata", mongoPersistence.JournalSettings.MetadataCollection);
            Assert.False(mongoPersistence.JournalSettings.LegacySerialization);
            Assert.Equal(TimeSpan.FromSeconds(10), mongoPersistence.JournalSettings.CallTimeout);
        }

        [Fact]
        public void Mongo_SnapshotStoreSettingsSettings_must_have_default_values()
        {
            var mongoPersistence = MongoDbPersistence.Get(Sys);

            Assert.Equal(string.Empty, mongoPersistence.SnapshotStoreSettings.ConnectionString);
            Assert.True(mongoPersistence.SnapshotStoreSettings.AutoInitialize);
            Assert.Equal("SnapshotStore", mongoPersistence.SnapshotStoreSettings.Collection);
            Assert.False(mongoPersistence.SnapshotStoreSettings.LegacySerialization);
            Assert.Equal(TimeSpan.FromSeconds(10), mongoPersistence.SnapshotStoreSettings.CallTimeout);
        }
    }
}
