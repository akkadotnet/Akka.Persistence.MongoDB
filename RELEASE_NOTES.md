#### 1.4.12 November 26 2020 ####

* Bump Akka version to 1.4.12
* Corrected `CurrentPersistentIds` query and `AllPersistentIds` queries to be more memory efficient and query entity ID data directly from Mongo
* Introduced `AllEvents` and `CurrentEvents` query to read the entire MongoDb journal
* Deprecated previous `GetMaxSeqNo` behavior - we no longer query the max sequence number directly from the journal AND the metadata collection. We only get that data directly from the metadata collection itself, which should make this query an O(1) operation rather than O(n)
