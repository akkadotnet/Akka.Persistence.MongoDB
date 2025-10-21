using System;
using Akka.Actor;
using Akka.Hosting;
using Akka.Persistence.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;

#nullable enable
namespace Akka.Persistence.MongoDb.Hosting;

public static class AkkaPersistenceMongoDbHostingExtensions
{
    /// <summary>
    ///     Adds Akka.Persistence.MongoDb support to this <see cref="ActorSystem"/> with optional support
    ///     for health checks on both journal and snapshot store.
    /// </summary>
    /// <param name="builder">
    ///     The builder instance being configured.
    /// </param>
    /// <param name="connectionString">
    ///     Connection string used for database access.
    /// </param>
    /// <param name="mode">
    ///     <para>
    ///         Determines which settings should be added by this method call.
    ///     </para>
    ///     <i>Default</i>: <see cref="PersistenceMode.Both"/>
    /// </param>
    /// <param name="autoInitialize">
    ///     <para>
    ///         Should the SQL store table be initialized automatically.
    ///     </para>
    ///     <i>Default</i>: <c>false</c>
    /// </param>
    /// <param name="journalBuilder">
    ///     <para>
    ///         An <see cref="Action{T}"/> used to configure an <see cref="AkkaPersistenceJournalBuilder"/> instance.
    ///     </para>
    ///     <i>Default</i>: <c>null</c>
    /// </param>
    /// <param name="snapshotBuilder">
    ///     <para>
    ///         An <see cref="Action{T}"/> used to configure an <see cref="AkkaPersistenceSnapshotBuilder"/> instance.
    ///     </para>
    ///     <i>Default</i>: <c>null</c>
    /// </param>
    /// <param name="pluginIdentifier">
    ///     <para>
    ///         The configuration identifier for the plugins
    ///     </para>
    ///     <i>Default</i>: <c>"sql-server"</c>
    /// </param>
    /// <param name="isDefaultPlugin">
    ///     <para>
    ///         A <c>bool</c> flag to set the plugin as the default persistence plugin for the <see cref="ActorSystem"/>
    ///     </para>
    ///     <b>Default</b>: <c>true</c>
    /// </param>
    /// <returns>
    ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <see cref="journalBuilder"/> is set and <see cref="mode"/> is set to
    ///     <see cref="PersistenceMode.SnapshotStore"/>
    /// </exception>
    /// <example>
    /// <code>
    /// builder.WithMongoDbPersistence(
    ///     connectionString: "...",
    ///     journalBuilder: journal => journal
    ///         .AddEventAdapter&lt;MyAdapter&gt;("adapter", new[] { typeof(MyEvent) })
    ///         .WithHealthCheck(HealthStatus.Degraded),
    ///     snapshotBuilder: snapshot => snapshot
    ///         .WithHealthCheck(HealthStatus.Degraded));
    /// </code>
    /// </example>
    public static AkkaConfigurationBuilder WithMongoDbPersistence(
        this AkkaConfigurationBuilder builder,
        string connectionString,
        PersistenceMode mode = PersistenceMode.Both,
        bool autoInitialize = true,
        Action<AkkaPersistenceJournalBuilder>? journalBuilder = null,
        Action<AkkaPersistenceSnapshotBuilder>? snapshotBuilder = null,
        string pluginIdentifier = "mongodb",
        bool isDefaultPlugin = true)
    {
        if (mode == PersistenceMode.SnapshotStore && journalBuilder is { })
            throw new Exception(
                $"{nameof(journalBuilder)} can only be set when {nameof(mode)} is set to either {PersistenceMode.Both} or {PersistenceMode.Journal}");

        if (mode == PersistenceMode.Journal && snapshotBuilder is not null)
            throw new Exception($"{nameof(snapshotBuilder)} can only be set when {nameof(mode)} is set to either {PersistenceMode.Both} or {PersistenceMode.SnapshotStore}");
        
        var journalOpt = new MongoDbJournalOptions(isDefaultPlugin, pluginIdentifier)
        {
            ConnectionString = connectionString,
            AutoInitialize = autoInitialize,
        };

        var snapshotOpt = new MongoDbSnapshotOptions(isDefaultPlugin, pluginIdentifier)
        {
            ConnectionString = connectionString,
            AutoInitialize = autoInitialize,
        };

        return mode switch
        {
            PersistenceMode.Journal => builder.WithMongoDbPersistence(journalOpt, null, journalBuilder, snapshotBuilder),
            PersistenceMode.SnapshotStore => builder.WithMongoDbPersistence(null, snapshotOpt, journalBuilder, snapshotBuilder),
            PersistenceMode.Both => builder.WithMongoDbPersistence(journalOpt, snapshotOpt, journalBuilder, snapshotBuilder),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Invalid PersistenceMode defined.")
        };
    }

    /// <summary>
    ///     Adds Akka.Persistence.MongoDb support to this <see cref="ActorSystem"/>. At least one of the
    ///     configurator delegate needs to be populated else this method will throw an exception.
    /// </summary>
    /// <param name="builder">
    ///     The builder instance being configured.
    /// </param>
    /// <param name="journalOptionConfigurator">
    ///     <para>
    ///         An <see cref="Action{T}"/> that modifies an instance of <see cref="MongoDbJournalOptions"/>,
    ///         used to configure the journal plugin
    ///     </para>
    ///     <i>Default</i>: <c>null</c>
    /// </param>
    /// <param name="snapshotOptionConfigurator">
    ///     <para>
    ///         An <see cref="Action{T}"/> that modifies an instance of <see cref="MongoDbSnapshotOptions"/>,
    ///         used to configure the snapshot store plugin
    ///     </para>
    ///     <i>Default</i>: <c>null</c>
    /// </param>
    /// <param name="isDefaultPlugin">
    ///     <para>
    ///         A <c>bool</c> flag to set the plugin as the default persistence plugin for the <see cref="ActorSystem"/>
    ///     </para>
    ///     <b>Default</b>: <c>true</c>
    /// </param>
    /// <returns>
    ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when both <paramref name="journalOptionConfigurator"/> and <paramref name="snapshotOptionConfigurator"/> are null.
    /// </exception>
    public static AkkaConfigurationBuilder WithMongoDbPersistence(
        this AkkaConfigurationBuilder builder,
        Action<MongoDbJournalOptions>? journalOptionConfigurator = null,
        Action<MongoDbSnapshotOptions>? snapshotOptionConfigurator = null,
        bool isDefaultPlugin = true)
    {
        if (journalOptionConfigurator is null && snapshotOptionConfigurator is null)
            throw new ArgumentException(
                $"{nameof(journalOptionConfigurator)} and {nameof(snapshotOptionConfigurator)} could not both be null");

        MongoDbJournalOptions? journalOptions = null;
        if (journalOptionConfigurator is { })
        {
            journalOptions = new MongoDbJournalOptions(isDefaultPlugin);
            journalOptionConfigurator(journalOptions);
        }

        MongoDbSnapshotOptions? snapshotOptions = null;
        if (snapshotOptionConfigurator is { })
        {
            snapshotOptions = new MongoDbSnapshotOptions(isDefaultPlugin);
            snapshotOptionConfigurator(snapshotOptions);
        }

        return builder.WithMongoDbPersistence(journalOptions, snapshotOptions);
    }

    /// <summary>
    ///     Adds Akka.Persistence.MongoDb support to this <see cref="ActorSystem"/>. At least one of the options
    ///     have to be populated else this method will throw an exception.
    /// </summary>
    /// <param name="builder">
    ///     The builder instance being configured.
    /// </param>
    /// <param name="journalOptions">
    ///     <para>
    ///         An instance of <see cref="MongoDbJournalOptions"/>, used to configure the journal plugin
    ///     </para>
    ///     <i>Default</i>: <c>null</c>
    /// </param>
    /// <param name="snapshotOptions">
    ///     <para>
    ///         An instance of <see cref="MongoDbSnapshotOptions"/>, used to configure the snapshot store plugin
    ///     </para>
    ///     <i>Default</i>: <c>null</c>
    /// </param>
    /// <param name="journalBuilder">
    ///     <para>
    ///         An <see cref="Action{T}" /> used to configure an <see cref="AkkaPersistenceJournalBuilder" /> instance for event adapters and health checks.
    ///     </para>
    ///     <i>Default</i>: <c>null</c>
    /// </param>
    /// <param name="snapshotBuilder">
    ///     <para>
    ///         An <see cref="Action{T}" /> used to configure an <see cref="AkkaPersistenceSnapshotBuilder" /> instance for health checks.
    ///     </para>
    ///     <i>Default</i>: <c>null</c>
    /// </param>
    /// <returns>
    ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when both <paramref name="journalOptions"/> and <paramref name="snapshotOptions"/> are null.
    /// </exception>
    public static AkkaConfigurationBuilder WithMongoDbPersistence(
        this AkkaConfigurationBuilder builder,
        MongoDbJournalOptions? journalOptions = null,
        MongoDbSnapshotOptions? snapshotOptions = null,
        Action<AkkaPersistenceJournalBuilder>? journalBuilder = null,
        Action<AkkaPersistenceSnapshotBuilder>? snapshotBuilder = null)
    {
        return (journalOptions, snapshotOptions) switch
        {
            (null, null) =>
                throw new ArgumentException(
                    $"{nameof(journalOptions)} and {nameof(snapshotOptions)} could not both be null"),

            (_, null) =>
                builder
                    .WithJournal(journalOptions, journalBuilder)
                    .AddHocon(MongoDbPersistence.DefaultConfiguration(), HoconAddMode.Append),

            (null, _) =>
                builder
                    .WithSnapshot(snapshotOptions, snapshotBuilder),

            (_, _) =>
                builder
                    .WithJournalAndSnapshot(journalOptions, snapshotOptions, journalBuilder, snapshotBuilder)
                    .AddHocon(MongoDbPersistence.DefaultConfiguration(), HoconAddMode.Append),
        };
    }
}