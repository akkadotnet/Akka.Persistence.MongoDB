using System;
using System.Collections.Generic;
using System.Text;
using Akka.Actor.Setup;
using Akka.Persistence.MongoDb.Journal;
using Akka.Persistence.MongoDb.Snapshot;
using MongoDB.Driver;

namespace Akka.Persistence.MongoDb
{
    /// <summary>
    /// A setup class to allow for programmatic configuration of the <see cref="MongoDbSnapshotStore"/> and <see cref="MongoDbJournal"/>
    /// </summary>
    public class MongoDbPersistenceSetup : Setup
    {
        /// <summary>
        /// Constructor to instantiate a new instance of <see cref="MongoDbPersistenceSetup"/>
        /// </summary>
        /// <param name="snapshotDatabaseName">
        /// The database name to be used by <see cref="MongoDbSnapshotStore"/> when it connects the server
        /// </param>
        /// <param name="snapshotConnectionSettings">
        /// The <see cref="MongoClientSettings"/> to be used by <see cref="MongoDbSnapshotStore"/> when it connects the server.
        /// </param>
        /// <param name="journalDatabaseName">
        /// The database name to be used by <see cref="MongoDbJournal"/> when it connects the server
        /// </param>
        /// <param name="journalConnectionSettings">
        /// The <see cref="MongoClientSettings"/> to be used by <see cref="MongoDbJournal"/> when it connects the server
        /// </param>
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

        /// <summary>
        /// The database name to be used by <see cref="MongoDbSnapshotStore"/> when it connects the server
        /// </summary>
        public string SnapshotDatabaseName { get; }
        /// <summary>
        /// The <see cref="MongoClientSettings"/> to be used by <see cref="MongoDbSnapshotStore"/> when it connects the server.
        /// Setting this property will override 'akka.persistence.snapshot-store.mongodb.connection-string' in the HOCON configuration.
        /// Set this to <code>null</code> if you're not overriding this connection string.
        /// </summary>
        public MongoClientSettings SnapshotConnectionSettings { get; }
        /// <summary>
        /// The database name to be used by <see cref="MongoDbJournal"/> when it connects the server
        /// </summary>
        public string JournalDatabaseName { get; }
        /// <summary>
        /// The <see cref="MongoClientSettings"/> to be used by <see cref="MongoDbJournal"/> when it connects the server
        /// Setting this property will override 'akka.persistence.journal.mongodb.connection-string' in the HOCON configuration.
        /// Set this to <code>null</code> if you're not overriding this connection string.
        /// </summary>
        public MongoClientSettings JournalConnectionSettings { get; }
    }
}
