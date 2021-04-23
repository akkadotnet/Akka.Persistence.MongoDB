using System;
using System.Collections.Generic;
using System.Text;
using Akka.Actor.Setup;
using MongoDB.Driver;

namespace Akka.Persistence.MongoDb
{
    public class MongoDbPersistenceSetup : Setup
    {
        public MongoDbPersistenceSetup(
            string snapshotDatabaseName,
            MongoClientSettings snapshotConnectionSettings, 
            string journalDatabaseName,
            MongoClientSettings journalConnectionSettings)
        {
            SnapshotConnectionSettings = snapshotConnectionSettings;
            JournalConnectionSettings = journalConnectionSettings;
            SnapshotDatabaseName = snapshotDatabaseName;
            JournalDatabaseName = journalDatabaseName;
        }

        public string SnapshotDatabaseName { get; }
        public MongoClientSettings SnapshotConnectionSettings { get; }
        public string JournalDatabaseName { get; }
        public MongoClientSettings JournalConnectionSettings { get; }
    }
}
