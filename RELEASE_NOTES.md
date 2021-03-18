#### 1.4.17 March 17 2021 ####

* Bump [Akka.NET version to 1.4.17](https://github.com/akkadotnet/akka.net/releases/tag/1.4.17)
* [Resolve MongoDb write atomicity issues on write by not updating metadata collection](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/186) - this is an important change that makes all writes atomic for an individual persistentId in MongoDb. We don't update the meta-data collection on write anymore - it's only done when the most recent items in the journal are deleted, and thus we store the highest recorded sequence number in the meta-data collection during deletes. All of the Akka.Persistence.Sql plugins operate this way as well.
