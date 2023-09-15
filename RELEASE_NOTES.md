#### 1.5.0.1 September 15 2023 ####

* [Bump Akka.Persistence.Hosting to 1.5.12](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/337)
* [Bump AkkaVersion to 1.5.12](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/336)
* [Separate Akka.Hosting and core Akka version](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/335)
* [Bump XunitVersion to 2.5.0](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/332)
* [Move to using Build Props file and central package management.](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/333)
* [Adding Hosting Extensions for Akka.Persistence.MongoDB](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/331)
* [Add indexed tags support](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/318)
* [Add CancellationToken suppport to all driver calls](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/328)
* [All writes are now performed via MongoDb transactions](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/301)
* [Bump Akka.Persistence.Hosting to 1.5.12.1](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.12.1)
* [Bump MongoDB.Driver to 2.21.0](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/339)
* [Remove byte rot code that might have caused issue #313](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/347)
* [Implement transaction on both read and write operation](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/347)
* [Make transaction defaults to enabled](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/348)

**Breaking Behavior Change**

In this version, we're turning transaction on by default. If you're more concerned with database write and read performance compared to data consistency and correctness, you can move back to the old behavior by setting this flag in the HOCON configuration:

```hocon
akka.persistence.journal.mongodb.use-write-transaction = off
akka.persistence.snapshot-store.mongodb.use-write-transaction = off
```

Or by setting them inside the hosting options:

```csharp
var journalOptions = new MongoDbJournalOptions(true) 
    {
        UseWriteTransaction = false
    };
var snapshotOptions = new MongoDbSnapshotOptions(true)
    {
        UseWriteTransaction = false
    };
```

#### 1.5.0 March 03 2023 ####
* [Bump Akka.NET to 1.5.0](https://github.com/akkadotnet/akka.net/releases/tag/1.5.0)

#### 1.4.48 January 24 2023 ####
* [Bump Akka.NET to 1.4.48](https://github.com/akkadotnet/akka.net/releases/tag/1.4.48)
* [Bump MongoDb.Driver to 2.17.0](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/284)

#### 1.4.40 July 27 2022 ####
* [Bump Akka.NET to 1.4.40](https://github.com/akkadotnet/akka.net/releases/tag/1.4.40)
* [Fix HighestSequenceNr query not returning proper value](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/267)
* [Bump MongoDb.Driver to 2.17.0](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/273)

#### 1.4.40-RC1 July 1 2022 ####
* [Fix HighestSequenceNr query not returning proper value](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/267)
* [Bump MongoDb.Driver to 2.16.1](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/266)

#### 1.4.39 June 6 2022 ####
* [Bump Akka.NET version to 1.4.39](https://github.com/akkadotnet/akka.net/releases/tag/1.4.39)
* [Fix compatibility with Akka.Cluster.Sharding in persistence mode](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/259)
* [Bump MongoDb.Driver to 2.15.1](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/255)
* [Fix BsonTimestamp causes NRE to be thrown](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/249)

#### 1.4.38-beta2 May 27 2022 ####

* [Fix compatibility with Akka.Cluster.Sharding in persistence mode](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/259)
* [Bump Akka.NET version to 1.4.38](https://github.com/akkadotnet/akka.net/releases/tag/1.4.38)
* [Bump MongoDb.Driver to 2.15.1](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/255)

#### 1.4.38-beta1 April 15 2022 ####

* [Fix BsonTimestamp causes NRE to be thrown](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/249)

#### 1.4.37 April 15 2022 ####

* [Bump Akka.NET version to 1.4.37](https://github.com/akkadotnet/akka.net/releases/tag/1.4.37)
* [Bump MongoDb.Driver to 2.15.0](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/245)

#### 1.4.31 December 21 2021 ####

* [Bump Akka.NET version to 1.4.31](https://github.com/akkadotnet/akka.net/releases/tag/1.4.31)
* [Bump MongoDb.Driver to 2.14.1](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/234)
* [Fix MongoDB InsertManyAsync to support ordering](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/224)

#### 1.4.25 September 9 2021 ####

* [Bump Akka.NET version to 1.4.25](https://github.com/akkadotnet/akka.net/releases/tag/1.4.25)
* [Bump MongoDb.Driver to 2.13.1](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/216)

#### 1.4.21 July 07 2021 ####

* [Bump Akka.NET version to 1.4.21](https://github.com/akkadotnet/akka.net/releases/tag/1.4.21)
* [Bump MongoDb.Driver to 2.12.4](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/209)
* [Change table auto-initialize default value to true](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/212)

#### 1.4.19 May 04 2021 ####

* [Bump MongoDb.Driver to 2.12.2](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/197)
* [Bump Akka.NET version to 1.4.19](https://github.com/akkadotnet/akka.net/releases/tag/1.4.19)
* [Add programmatic setup support](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/199)

Please [read the Akka.Persistence.MongoDb README.md on how to use the new `MongoDbPersistenceSetup` feature to programmatically configure your `MongoDbClient`](https://github.com/akkadotnet/Akka.Persistence.MongoDB#programmatic-configuration).
