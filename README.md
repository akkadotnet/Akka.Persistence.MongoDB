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
	plugin = "akka.persistence.journal.mongodb"
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

			# For users with legacy data, who want to keep writing data to MongoDb using the original BSON format
			# and not the standard binary format introduced in v1.4.0 (see https://github.com/akkadotnet/Akka.Persistence.MongoDB/issues/72)
			# enable this setting via `legacy-serialization = on`.
			#
			# NOTE: this will likely break features such as Akka.Cluster.Sharding, IActorRef serialization, AtLeastOnceDelivery, and more.
			legacy-serialization = off
		}
	}

	snapshot-store {
	plugin = "akka.persistence.snapshot-store.mongodb"
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

			# For users with legacy data, who want to keep writing data to MongoDb using the original BSON format
			# and not the standard binary format introduced in v1.4.0 (see https://github.com/akkadotnet/Akka.Persistence.MongoDB/issues/72)
			# enable this setting via `legacy-serialization = on`.
			#
			# NOTE: this will likely break features such as Akka.Cluster.Sharding, IActorRef serialization, AtLeastOnceDelivery, and more.
			legacy-serialization = off
		}
	}
}
```

### Serialization
[Going from v1.4.0 onwards, all events and snapshots are saved as byte arrays using the standard Akka.Persistence format](https://github.com/akkadotnet/Akka.Persistence.MongoDB/issues/72).

However, in the event that you have one of the following use cases:

1. Legacy data all stored in the original BSON / "object" format;
2. A use case where BSON is preferable, i.e. so it can be queried directly via MongoDb queries rather than Akka.Persistence.Query; or
3. A requirement to keep all data in human-readable form.

Then you can disable binary serialization (enabled by default) via the following HOCON:

```
akka.persistence.mongodb{
   journal{
    legacy-serialization = off
  }

  snapshot-store{
   legacy-serialization = off
 }
}
```

Setting `legacy-serialization = on` will allow you to save objects in a BSON format.

**WARNING**: However, `legacy-serialization = on` will break Akka.NET serialization. `IActorRef`s, Akka.Cluster.Sharding, `AtLeastOnceDelivery` actors, and other built-in Akka.NET use cases can't be properly supported using this format. Use it at your own risk.

### Notice
- The MongoDB operator to limit the number of documents in a query only accepts an integer while akka provides a long as maximum for the loading of events during the replay. Internally the long value is cast to an integer and if the value is higher then Int32.MaxValue, Int32.MaxValue is used. So if you have stored more then 2,147,483,647 events for a single PersistenceId, you may have a problem :wink:
