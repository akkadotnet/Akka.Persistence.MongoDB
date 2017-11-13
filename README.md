# Akka.Persistence.MongoDB

Akka Persistence journal and snapshot store backed by MongoDB database.

**WARNING: Akka.Persistence.MongoDB plugin is still in beta and it's mechanics described bellow may be still subject to change**.

### Setup

To activate the journal plugin, add the following lines to actor system configuration file:

```
akka.persistence.journal.plugin = "akka.persistence.journal.mongodb"
akka.persistence.journal.mongodb.connection-string = "<database connection string>"
akka.persistence.journal.mongodb.collection = "<journal collection>"
```

Similar configuration may be used to setup a MongoDB snapshot store:

```
akka.persistence.snapshot-store.plugin = "akka.persistence.snapshot-store.mongodb"
akka.persistence.snapshot-store.mongodb.connection-string = "<database connection string>"
akka.persistence.snapshot-store.mongodb.collection = "<snapshot-store collection>"
```

Remember that connection string must be provided separately to Journal and Snapshot Store. To finish setup simply initialize plugin using: `MongoDbPersistence.Get(actorSystem);`

### Configuration

Both journal and snapshot store share the same configuration keys (however they resides in separate scopes, so they are definied distinctly for either journal or snapshot store):

```hocon
akka.persistence {
	journal {
		mongodb {
			# qualified type name of the MongoDb persistence journal actor
			class = "Akka.Persistence.MongoDb.Journal.MongoDbJournal, Akka.Persistence.MongoDb"

			# connection string used for database access
			connection-string = ""

			# should corresponding journal table's indexes be initialized automatically
			auto-initialize = off

			# dispatcher used to drive journal actor
			plugin-dispatcher = "akka.actor.default-dispatcher"

			# MongoDb collection corresponding with persistent journal
			collection = "EventJournal"

			# metadata collection
			metadata-collection = "Metadata"
		}
	}

	snapshot-store {
		mongodb {
			# qualified type name of the MongoDB persistence snapshot actor
			class = "Akka.Persistence.MongoDb.Snapshot.MongoDbSnapshotStore, Akka.Persistence.MongoDb"

			# connection string used for database access
			connection-string = ""

			# should corresponding snapshot's indexes be initialized automatically
			auto-initialize = off

			# dispatcher used to drive snapshot storage actor
			plugin-dispatcher = "akka.actor.default-dispatcher"

			# MongoDb collection corresponding with persistent snapshot store
			collection = "SnapshotStore"
		}
	}
}
```

### Serialization
The events and snapshots are stored as BsonDocument, so you need to register you types with the BsonClassMap before you can use your persistence actor.  
Otherwise the recovery will fail and you receive a RecoveryFailure with the message:  
>An error occurred while deserializing the Payload property of class \<Journal or Snapshot class>: Unknown discriminator value '\<your type>'

### Migration
#### From 1.05 to 1.1.0
As of 1.1.0 the highest SequenceNr for each PersistenceId in the EventJournal collection is kept in a separate collection, Metadata.
This script creates the Metadata collection, then finds the highest SequenceNr for each persistence id from the EventJournal and adds it to the Metadata collection.

To run, save this to a JavaScript file, update {your_database_address} e.g. 127.0.0.1:27017/events. Run: mongo {file_name}.js
```javascript
try {
	var db = connect('{your_database_address}');

	var persistenceIds = db.EventJournal.distinct('PersistenceId');
	
	persistenceIds.forEach(persistenceId => {
		print('Finding highest SequenceNr for PersistenceId: ' + persistenceId);
		var highestSequenceNr = db.EventJournal
			.find({ PersistenceId: persistenceId }, { SequenceNr: true })
			.sort({ SequenceNr: -1 })
			.limit(1)
			.next()
			.SequenceNr;
		
		print('Highest SequenceNr found ' + highestSequenceNr + ', inserting into Metadata table...');
		db.Metadata.insertOne(
			{
				_id: persistenceId,
				PersistenceId: persistenceId,
				SequenceNr: highestSequenceNr
			}
		);
		
		print('Inserted successfully');
	});
} catch(e) {
	print(e);
}
```

### Notice
- The MongoDB operator to limit the number of documents in a query only accepts an integer while akka provides a long as maximum for the loading of events during the replay. Internally the long value is cast to an integer and if the value is higher then Int32.MaxValue, Int32.MaxValue is used. So if you have stored more then 2,147,483,647 events for a single PersistenceId, you may have a problem :wink:
