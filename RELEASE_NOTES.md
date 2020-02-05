#### 1.4.0-beta3 February 04 2020 ####

Introduced legacy serialization modes.

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