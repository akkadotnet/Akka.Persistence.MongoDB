## Akka.Persistence.MongoDB

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

Remember that connection string must be provided separately to Journal and Snapshot Store. To finish setup simply initialize plugin using: `MongoDbPersistence.Instance.Apply(actorSystem);`

### Configuration

Both journal and snapshot store share the same configuration keys (however they resides in separate scopes, so they are definied distinctly for either journal or snapshot store):

- `class` (string with fully qualified type name) - determines class to be used as a persistent journal. Default: *Akka.Persistence.MongoDb.Journal.MongoDbJournal, Akka.Persistence.MongoDb* (for journal) and *Akka.Persistence.MongoDb.Snapshot.MongoDbSnapshotStore, Akka.Persistence.MongoDb* (for snapshot store).
- `connection-string` - connection string used to access MongoDB. Default: *mongodb://localhost/akkanet* (for journal and snapshot store).
- `collection` - collection used to store the events or snapshots. Default:
*EventJournal* (for journal) and *SnapshotStore* (for snapshot store).

### Serialization
The events and snapshots are stored as BsonDocument, so you need to register you types with the BsonClassMap before you can use your persistence actor.  
Otherwise the recovery will fail and you receive a RecoveryFailure with the message:  
>An error occurred while deserializing the Payload property of class \<Journal or Snapshot class>: Unknown discriminator value '\<your type>'

### Notice
- The MongoDB operator to limit the number of documents in a query only accepts an integer while akka provides a long as maximum for the loading of events during the replay. Internally the long value is cast to an integer and if the value is higher then Int32.MaxValue, Int32.MaxValue is used. So if you have stored more then 2,147,483,647 events for a singel PersistenceId, you may have a problem :wink:
- If you call SaveSnapshot in your PersistanceActor twice, without persisting any events to the journal between the first and second call, the second call will not override the exising snapshot but rather send a SaveSnapshotFailure message back. Even if storing snapshots without persisting events in the meantime make no sense at all.
