#### 1.5.70 July 29th 2026 ####

* [Bump Akka.NET to 1.5.70](https://github.com/akkadotnet/akka.net/releases/tag/1.5.70)
* [Bump Akka.Hosting to 1.5.70](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.70)
* Add support for `Offset.FromEnd` to current and live `EventsByTag` and `AllEvents` queries

#### 1.5.67 April 28th 2026 ####

* [Bump Akka.NET to 1.5.67](https://github.com/akkadotnet/akka.net/releases/tag/1.5.67)
* [Bump Akka.Hosting to 1.5.67](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.67)

#### 1.5.59 January 26th 2026 ####

* [Bump Akka.NET to 1.5.59](https://github.com/akkadotnet/akka.net/releases/tag/1.5.59)
* [Bump Akka.Hosting to 1.5.59](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.59)
* [Fix lock contention and high CPU usage during concurrent event persistence](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/428)

**Performance Fix: Reduced BSON Serializer Lock Contention**

Fixed a critical performance issue where concurrent persistence operations would cause high CPU usage due to lock contention in the BSON serializer. Previously, `FullTypeNameObjectSerializer` would repeatedly register discriminators for types that were already registered, acquiring write locks unnecessarily.

Changes:
- Added tracking of already-registered types to skip redundant registration
- Made `FullTypeNameObjectSerializer` public with a static `RegisterNewTypesToDiscriminator()` method for pre-registration at startup

#### 1.5.55.1 October 29th 2025 ####

**Improved API**

This release introduces the simplified Akka.Hosting 1.5.55.1 API for connectivity health checks, eliminating redundant parameter passing:

**New Simplified API (Recommended):**
```csharp
journalBuilder: journal =>
{
    journal.WithConnectivityCheck(); // Options automatically accessed from builder
}
```

**Previous API (Still Supported):**
```csharp
journalBuilder: journal =>
{
    journal.WithConnectivityCheck(journalOptions); // Explicit parameter passing
}
```

The new API automatically accesses options from `builder.Options`, making the code cleaner and less error-prone. The previous API is marked as `[Obsolete]` but remains functional for backward compatibility.

* Update `WithConnectivityCheck()` extension methods to use simplified Akka.Hosting 1.5.55.1 API pattern
* Upgraded to [Akka.Hosting 1.5.55.1](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.55.1)

#### 1.5.55 October 26th 2025 ####

* [Bump Akka.NET to 1.5.55](https://github.com/akkadotnet/akka.net/releases/tag/1.5.55)
* [Bump Akka.Hosting to 1.5.55](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.55)
* [Add MongoDB connectivity health checks](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/423)

Adds new `WithConnectivityCheck()` methods for proactive MongoDB connectivity verification with customizable tags.

#### 1.5.53 October 16th 2025 ####

* [Bump Akka.NET to 1.5.53](https://github.com/akkadotnet/akka.net/releases/tag/1.5.53)
* [Bump Akka.Persistence.Hosting to 1.5.53](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.53)
* [Implement hosting healthcheck](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/421)

**New Feature: Health Check Support for Akka.Persistence.MongoDb.Hosting**

This release adds built-in health check support for MongoDB journal and snapshot stores, integrated with [Microsoft.Extensions.Diagnostics.HealthChecks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks). This enables monitoring and observability for your MongoDB persistence plugins.

Key capabilities:
* Automatic health reporting for MongoDB journal and snapshot stores
* Integration with ASP.NET Core health check endpoints
* Configurable health status reporting (Healthy, Degraded, Unhealthy)
* Tagged health checks for easy filtering (`akka`, `persistence`, `mongodb`)

Example usage:
```csharp
services.AddHealthChecks(); // Add health check service

services.AddAkka("MyActorSystem", (builder, provider) =>
{
    builder
        .WithMongoDbPersistence(
            connectionString: "mongodb://localhost:27017/akka",
            journalBuilder: journal => journal.WithHealthCheck(HealthStatus.Degraded),
            snapshotBuilder: snapshot => snapshot.WithHealthCheck(HealthStatus.Degraded));
});
```

For complete documentation, see the [Akka.Persistence.MongoDb.Hosting README](src/Akka.Persistence.MongoDb.Hosting/README.md) and the main [README](README.md#health-check-support).

#### 1.5.42 May 22nd 2025 ####

* [Bump Akka.NET to 1.5.42](https://github.com/akkadotnet/akka.net/releases/tag/1.5.42)
* [Bump Akka.Persistence.Hosting to 1.5.42](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.42)
* [Use the new Akka.Persistence cancellation token API](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/418)
* [Optimize write transaction usage](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/410)

We're optimizing how write transaction is being done. Following the [MongoDb documentation](https://www.mongodb.com/docs/manual/core/write-operations-atomicity/#atomicity-and-transactions) that all single document writes are atomic, we're not using transaction for single document writes anymore.

#### 1.5.41-beta1 April 4th 2025 ####

* [Optimize write transaction usage](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/410)

We're optimizing how write transaction is being done. Following the [MongoDb documentation](https://www.mongodb.com/docs/manual/core/write-operations-atomicity/#atomicity-and-transactions) that all single document writes are atomic, we're not using transaction for single document writes anymore.

#### 1.5.40 March 27th 2025 ####

* [Bump Akka.NET to 1.5.40](https://github.com/akkadotnet/akka.net/releases/tag/1.5.40)
* [Bump Akka.Persistence.Hosting to 1.5.40](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.40)
* [Fix snapshot store recreating MongoDb collection on each DB operation](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/402)
* [Add tags support to AllEvents query](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/407)
* [Separate transaction feature flag to enable separate read and write transaction](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/408)
* [Refactor bulk journal reads during actor recovery and event query to use the more memory efficient `AsCursorAsync()` MongoDb API](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/409)

#### 1.5.37 January 24th 2025 ####

* [Bump Akka.NET to 1.5.37](https://github.com/akkadotnet/akka.net/releases/tag/1.5.37)
* [Bump Akka.Persistence.Hosting to 1.5.37](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.37)

#### 1.5.32 December 23rd 2024 ####

* [Bump Akka.NET to 1.5.32](https://github.com/akkadotnet/akka.net/releases/tag/1.5.32)
* [Bump Akka.Persistence.Hosting to 1.5.32](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.32)
* [Bump MongoDB.Driver to 3.0.0](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/395)

**Breaking Change Notice**

Due to breaking changes in MongoDb.Driver v3.0.0, from this point forward, Akka.Persistence.MongoDb releases **WILL NOT** support:
* MongoDb server v3.6 and earlier
* Projects that targets .NET Core 2.x and lower
* Projects that targets .NET Framework 2.7.1 and lower
* LINQ2 provider
* TLS 1.0 and 1.1

**Driver Behavior Change And Workaround**

There is a behavior change in `MongoDb.Driver` v3.0.0 where it would use the DNS resolved server host address when it actually tries to connect to the server. This is problematic if your application lives inside a container and hard code your MongoDb server address inside the connection string.

Example:
* Server address used in connection string: "host.docker.internal:27017"
* Actual server address being used in MongoDb connection: "127.0.0.1:27017"

The workaround is to add "directConnection=true" in your connection string.

**`MongoDb.Driver` Upgrade Advisories**

`MongoDb.Driver` 3.0.0 release note: https://github.com/mongodb/mongo-csharp-driver/releases/tag/v3.0.0

`MongoDb.Driver` 3.0.0 Upgrade advisory: https://www.mongodb.com/docs/drivers/csharp/v3.0/upgrade/v3/

#### 1.5.32-beta1 December 5th 2024 ####

* [Bump Akka.NET to 1.5.32](https://github.com/akkadotnet/akka.net/releases/tag/1.5.32)
* [Bump Akka.Persistence.Hosting to 1.5.32](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.32)
* [Bump MongoDB.Driver to 3.0.0](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/395)

**Breaking Change Notice** 

Due to breaking changes in MongoDb.Driver v3.0.0, from this point forward, Akka.Persistence.MongoDb releases **WILL NOT** support:
* MongoDb server v3.6 and earlier
* Projects that targets .NET Core 2.x and lower
* Projects that targets .NET Framework 2.7.1 and lower
* LINQ2 provider
* TLS 1.0 and 1.1

MongoDb.Driver 3.0.0 release note: https://github.com/mongodb/mongo-csharp-driver/releases/tag/v3.0.0
 
MongoDb.Driver 3.0.0 Upgrade advisory: https://www.mongodb.com/docs/drivers/csharp/v3.0/upgrade/v3/ 

#### 1.5.31 December 4th 2024 ####

* [Bump Akka.NET to 1.5.31](https://github.com/akkadotnet/akka.net/releases/tag/1.5.31)
* [Bump Akka.Persistence.Hosting to 1.5.31.1](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.31.1)
* [Bump MongoDB.Driver to 2.30.0](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/392)

**Version Notice**

Due to breaking changes in MongoDb.Driver v3.0.0, This release will be the last Akka.Persistence.MongoDb that will support:
* MongoDb server v3.6 and earlier
* Projects that targets .NET Core 2.x and lower
* Projects that targets .NET Framework 2.7.1 and lower
* LINQ2 provider
* TLS 1.0 and 1.1

#### 1.5.30 October 3rd 2024 ####

* [Bump Akka.NET to 1.5.30](https://github.com/akkadotnet/akka.net/releases/tag/1.5.30)
* [Bump Akka.Persistence.Hosting to 1.5.30](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.30)
* [Bump MongoDB.Driver to 2.28.0](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/388)

**Breaking Change Notice**

The MongoDb driver 2.28.0 is [now strongly-named](https://github.com/mongodb/mongo-csharp-driver/pull/1393) which may affect your project(s). You can read more about this decision [here](https://www.mongodb.com/community/forums/t/net-c-driver-strong-naming/291649).

#### 1.5.29 October 1st 2024 ####

> [!NOTE]
> 
> **Deprecated**
> 
> Deprecated due to Akka.NET 1.5.29 deprecation. Please use 1.5.30 instead.

* [Bump Akka.NET to 1.5.29](https://github.com/akkadotnet/akka.net/releases/tag/1.5.29)
* [Bump Akka.Persistence.Hosting to 1.5.29](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.29)
* [Bump MongoDB.Driver to 2.28.0](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/388)

**Breaking Change Notice**

The MongoDb driver 2.28.0 is [now strongly-named](https://github.com/mongodb/mongo-csharp-driver/pull/1393) which may affect your project(s). You can read more about this decision [here](https://www.mongodb.com/community/forums/t/net-c-driver-strong-naming/291649).

#### 1.5.28 September 11th 2024 ####

* [Bump Akka.NET to 1.5.28](https://github.com/akkadotnet/akka.net/releases/tag/1.5.28)
* [Bump Akka.Persistence.Hosting to 1.5.28](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.28)
* [Bump MongoDB.Driver to 2.27.0](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/380)
* [Add large snapshot support](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/383)

**Support For Large (Greater Than 16 Megabytes) Snapshot Store**

> [!NOTE]
> 
> GridFS is considered as an advanced feature, it will not be supported by Akka.Hosting.

We added a new SnapshotStore that supports GridFS. To use it, you will need to set it through manual HOCON setting.

```text
akka.persistence.snapshot-store.mongodb.class = "Akka.Persistence.MongoDb.Snapshot.MongoDbGridFsSnapshotStore, Akka.Persistence.MongoDb"
```

#### 1.5.26 July 15th 2024 ####

* [Bump Akka.NET to 1.5.26](https://github.com/akkadotnet/akka.net/releases/tag/1.5.26)
* [Bump Akka.Persistence.Hosting to 1.5.25](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.25)
* [Fix failure in CurrentEventByTag when there are no events](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/374)
* [Fix CurrentEventByTag never completes](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/379)

#### 1.5.12.1 September 15 2023 ####

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

#### 1.5.12 August 10 2023 ####

* [Bump Akka.Persistence.Hosting from 1.5.8.1 to 1.5.12](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/337)
* [Bump AkkaVersion from 1.5.11 to 1.5.12](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/336)
* [Separate Akka.Hosting and core Akka version](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/335)
* [Bump XunitVersion from 2.4.2 to 2.5.0](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/332)
* [Move to using Build Props file and central package management.](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/333)
* [Bump MongoDB.Driver from 2.19.1 to 2.20.0](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/326)
* [Adding Hosting Extensions for Akka.Persistence.MongoDB](https://github.com/akkadotnet/Akka.Persistence.MongoDB/pull/331)
 
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
