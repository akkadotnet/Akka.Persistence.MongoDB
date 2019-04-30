#### 1.3.12 April 05 2019 ####
Support for Akka.Persistence 1.3.12.
Added support for Akka.Persistence.Query.
Upgraded to MongoDb v2.7.0 driver (2.8.0 doesn't support .NET 4.5)
Added support for configurable, binary serialization via Akka.NET.


#### 1.3.5 March 23 2018 ####
Support for Akka.Persistence 1.3.5.
Support for .NET Standard 1.6
Supports latest version of .NET MongoDB Driver.
You don't neeed to register/map your classes for serialization/deserialization anymore!

#### 1.1.0 July 30 2016 ####
Updated to Akka.Persistence 1.1.1

**Migration from 1.0.5 Up**
As of 1.1.0, the highest SequenceNr for each PersistenceId in the EventJournal collection is kept in a separate collection, Metadata.
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

#### 1.0.5 August 08 2015 ####

#### 1.0.4 August 07 2015 ####
Initial release of Akka.Persistence.MongoDb
