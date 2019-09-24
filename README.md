# Akka.Persistence.MongoDB

Akka Persistence journal and snapshot store backed by MongoDB database.

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
The events and snapshots are stored as BsonDocument. On the previous version of this driver you needed to register your types with BsonClassMap before you could use your persistence actor, otherwise the recovery would fail and you'd receive a RecoveryFailure with the message:  
>An error occurred while deserializing the Payload property of class \<Journal or Snapshot class>: Unknown discriminator value '\<your type>'

#### **Since now, all types are registered automatically for you so you don't need to use BsonClassMap to serialize/deserialize your types!**

### Notice
- The MongoDB operator to limit the number of documents in a query only accepts an integer while akka provides a long as maximum for the loading of events during the replay. Internally the long value is cast to an integer and if the value is higher then Int32.MaxValue, Int32.MaxValue is used. So if you have stored more then 2,147,483,647 events for a single PersistenceId, you may have a problem :wink:
