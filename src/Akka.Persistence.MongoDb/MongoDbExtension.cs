using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence.MongoDb.Journal;
using Akka.Persistence.MongoDb.Snapshot;
using MongoDB.Driver;

namespace Akka.Persistence.MongoDb
{
    /// <summary>
    /// An actor system extension initializing support for MongoDB persistence layer.
    /// </summary>
    public class MongoDbExtension : IExtension
    {
        /// <summary>
        /// The settings for the MongoDb journal.
        /// </summary>
        public MongoDbJournalSettings JournalSettings { get; private set; }

        /// <summary>
        /// The collection for the MongoDb journal
        /// </summary>
        public IMongoCollection<JournalEntry> JournalCollection { get; private set; }

        /// <summary>
        /// The settings for the MongoDB snapshot store.
        /// </summary>
        public MongoDbSnapshotSettings SnapshotStoreSettings { get; private set; }

        /// <summary>
        /// The collection for the MongoDb snapshot store
        /// </summary>
        public IMongoCollection<SnapshotEntry> SnapshotCollection { get; private set; }


        public MongoDbExtension(ExtendedActorSystem system)
        {
            if (system == null)
                throw new ArgumentNullException("system");

            // Initialize fallback configuration defaults
            system.Settings.InjectTopLevelFallback(MongoDbPersistence.DefaultConfig());

            // Read config
            var journalConfig = system.Settings.Config.GetConfig("akka.persistence.journal.mongodb");
            JournalSettings = new MongoDbJournalSettings(journalConfig);

            var snapshotConfig = system.Settings.Config.GetConfig("akka.persistence.snapshot-store.mongodb");
            SnapshotStoreSettings = new MongoDbSnapshotSettings(snapshotConfig);

            // Create collections
            var connectionString = new MongoUrl(JournalSettings.ConnectionString);
            var client = new MongoClient(connectionString);
            var journalDatabase = client.GetDatabase(connectionString.DatabaseName);

            JournalCollection = journalDatabase.GetCollection<JournalEntry>(JournalSettings.Collection);
            JournalCollection.Indexes.CreateOneAsync(
                Builders<JournalEntry>.IndexKeys.Ascending(entry => entry.PersistenceId)
                    .Descending(entry => entry.SequenceNr)).Wait();

            var snapshotDatabase = journalDatabase;
            // We only need another client if the connection strings aren't equal, 
            // because the MongoClient uses the same connection pool for all instances which have the same connection string
            if (!JournalSettings.ConnectionString.Equals(SnapshotStoreSettings.ConnectionString))
            {
                connectionString = new MongoUrl(SnapshotStoreSettings.ConnectionString);
                client = new MongoClient(connectionString);
                snapshotDatabase = client.GetDatabase(connectionString.DatabaseName);
            }

            SnapshotCollection = snapshotDatabase.GetCollection<SnapshotEntry>(SnapshotStoreSettings.Collection);
            SnapshotCollection.Indexes.CreateOneAsync(
                Builders<SnapshotEntry>.IndexKeys.Ascending(entry => entry.PersistenceId)
                    .Descending(entry => entry.SequenceNr)).Wait();
        }
    }
}
