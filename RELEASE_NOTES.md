#### 1.5.8 June 30 2023 ####

* [Bump Akka.NET to 1.5.8](https://github.com/akkadotnet/akka.net/releases/tag/1.5.8)
* [Add indexed tags support](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/318)
* [Add CancellationToken suppport to all driver calls](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/328)

#### 1.5.7 May 31 2023 ####

* [Bump Akka.NET to 1.5.7](https://github.com/akkadotnet/akka.net/releases/tag/1.5.7)
* [Bump MongoDb.Driver to 2.19.1](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/311)

#### 1.5.1.1 March 24 2023 ####

* [fixed ObjectSerializer initialization for backward compatibility](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/310) - this solves a compatibility problem in older Akka.Persistence.MongoDb applications that was introduced by updating MongoDb.Driver to 2.19.0.

#### 1.5.1 March 21 2023 ####
* [All writes are now performed via MongoDb transactions](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/301)
* [Bump MongoDb.Driver to 2.19.0](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/308)
* [Bump Akka.NET to 1.5.1](https://github.com/akkadotnet/akka.net/releases/tag/1.5.1)

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
