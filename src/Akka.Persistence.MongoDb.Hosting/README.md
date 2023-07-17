# Akka.Persistence.MongoDb.Hosting

Akka.Hosting extension methods to add Akka.Persistence.MongoDb to an ActorSystem

# Akka.Persistence.MongoDb Extension Methods

## WithMongoDbPersistence() Method

```csharp
public static AkkaConfigurationBuilder WithMongoDbPersistence(
    this AkkaConfigurationBuilder builder,
    string connectionString,
    PersistenceMode mode = PersistenceMode.Both,
    bool autoInitialize = true,
    Action<AkkaPersistenceJournalBuilder>? journalBuilder = null,
    string pluginIdentifier = "mongodb",
    bool isDefaultPlugin = true);
```

```csharp
public static AkkaConfigurationBuilder WithMongoDbPersistence(
    this AkkaConfigurationBuilder builder,
    Action<MongoDbJournalOptions>? journalOptionConfigurator = null,
    Action<MongoDbSnapshotOptions>? snapshotOptionConfigurator = null,
    bool isDefaultPlugin = true)
```

```csharp
public static AkkaConfigurationBuilder WithMongoDbPersistence(
    this AkkaConfigurationBuilder builder,
    MongoDbJournalOptions? journalOptions = null,
    MongoDbSnapshotOptions? snapshotOptions = null)
```

### Parameters

* `connectionString` __string__

  Connection string used for database access.

* `mode` __PersistenceMode__

  Determines which settings should be added by this method call. __Default__: `PersistenceMode.Both`

    * `PersistenceMode.Journal`: Only add the journal settings
    * `PersistenceMode.SnapshotStore`: Only add the snapshot store settings
    * `PersistenceMode.Both`: Add both journal and snapshot store settings

* `autoInitialize` __bool__

  Should the Mongo Db store collection be initialized automatically. __Default__: `false`

* `journalBuilder` __Action\<AkkaPersistenceJournalBuilder\>__

  An Action delegate used to configure an `AkkaPersistenceJournalBuilder` instance. Used to configure [Event Adapters](https://getakka.net/articles/persistence/event-adapters.html)

* `journalConfigurator` __Action\<MongoDbJournalOptions\>__

  An Action delegate to configure a `MongoDbJournalOptions` instance.

* `snapshotConfigurator` __Action\<MongoDbSnapshotOptions\>__

  An Action delegate to configure a `MongoDbSnapshotOptions` instance.

* `journalOptions` __MongoDbJournalOptions__

  An `MongoDbJournalOptions` instance to configure the SqlServer journal.

* `snapshotOptions` __MongoDbSnapshotOptions__

  An `MongoDbSnapshotOptions` instance to configure the SqlServer snapshot store.

## Example

```csharp
using var host = new HostBuilder()
    .ConfigureServices((context, services) =>
    {
        services.AddAkka("mongoDbDemo", (builder, provider) =>
        {
            builder
                .WithMongoDbPersistence("your-mongodb-connection-string");
        });
    }).Build();

await host.RunAsync();
```