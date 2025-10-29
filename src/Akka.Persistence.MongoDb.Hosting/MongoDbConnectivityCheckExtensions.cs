using System;
using System.Collections.Generic;
using Akka.Hosting;
using Akka.Persistence.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;

#nullable enable
namespace Akka.Persistence.MongoDb.Hosting;

/// <summary>
/// Extension methods for MongoDB persistence connectivity checks
/// </summary>
public static class MongoDbConnectivityCheckExtensions
{
    /// <summary>
    /// Adds a connectivity check for the MongoDB journal using the simplified Akka.Hosting 1.5.55.1+ API.
    /// This is a liveness check that proactively verifies database connectivity.
    /// Options are automatically accessed from the builder.
    /// </summary>
    /// <param name="builder">The journal builder</param>
    /// <param name="unHealthyStatus">The status to return when check fails. Defaults to Unhealthy.</param>
    /// <param name="name">Optional name for the health check. Defaults to "Akka.Persistence.MongoDB.Journal.{id}.Connectivity"</param>
    /// <param name="tags">Optional tags for the health check. Defaults to ["akka", "persistence", "mongodb", "journal", "connectivity"]</param>
    /// <returns>The journal builder for chaining</returns>
    public static AkkaPersistenceJournalBuilder WithConnectivityCheck(
        this AkkaPersistenceJournalBuilder builder,
        HealthStatus unHealthyStatus = HealthStatus.Unhealthy,
        string? name = null,
        IEnumerable<string>? tags = null)
    {
        // Get options from builder - this is the Akka.Hosting 1.5.55.1 simplified API
        var journalOptions = builder.Options as MongoDbJournalOptions
            ?? throw new InvalidOperationException(
                $"Options must be {nameof(MongoDbJournalOptions)}");

        if (string.IsNullOrWhiteSpace(journalOptions.ConnectionString))
            throw new ArgumentException("ConnectionString must be set on MongoDbJournalOptions");

        var registration = new AkkaHealthCheckRegistration(
            name ?? $"Akka.Persistence.MongoDB.Journal.{journalOptions.Identifier}.Connectivity",
            new MongoDbJournalConnectivityCheck(journalOptions.ConnectionString!, journalOptions.Identifier),
            unHealthyStatus,
            tags ?? new[] { "akka", "persistence", "mongodb", "journal", "connectivity" });

        return builder.WithCustomHealthCheck(registration);
    }

    /// <summary>
    /// Adds a connectivity check for the MongoDB journal (legacy API for backward compatibility).
    /// This is a liveness check that proactively verifies database connectivity.
    /// </summary>
    /// <param name="builder">The journal builder</param>
    /// <param name="journalOptions">The journal options containing connection details</param>
    /// <param name="unHealthyStatus">The status to return when check fails. Defaults to Unhealthy.</param>
    /// <param name="name">Optional name for the health check. Defaults to "Akka.Persistence.MongoDB.Journal.{id}.Connectivity"</param>
    /// <param name="tags">Optional tags for the health check. Defaults to ["akka", "persistence", "mongodb", "journal", "connectivity"]</param>
    /// <returns>The journal builder for chaining</returns>
    [Obsolete("Use the simplified API without passing journalOptions. Options are now automatically accessed from builder.Options. This overload will be removed in a future version.")]
    public static AkkaPersistenceJournalBuilder WithConnectivityCheck(
        this AkkaPersistenceJournalBuilder builder,
        MongoDbJournalOptions journalOptions,
        HealthStatus unHealthyStatus = HealthStatus.Unhealthy,
        string? name = null,
        string[]? tags = null)
    {
        if (journalOptions is null)
            throw new ArgumentNullException(nameof(journalOptions));

        if (string.IsNullOrWhiteSpace(journalOptions.ConnectionString))
            throw new ArgumentException("ConnectionString must be set on MongoDbJournalOptions", nameof(journalOptions));

        var registration = new AkkaHealthCheckRegistration(
            name ?? $"Akka.Persistence.MongoDB.Journal.{journalOptions.Identifier}.Connectivity",
            new MongoDbJournalConnectivityCheck(journalOptions.ConnectionString, journalOptions.Identifier),
            unHealthyStatus,
            tags ?? new[] { "akka", "persistence", "mongodb", "journal", "connectivity" });

        return builder.WithCustomHealthCheck(registration);
    }

    /// <summary>
    /// Adds a connectivity check for the MongoDB snapshot store using the simplified Akka.Hosting 1.5.55.1+ API.
    /// This is a liveness check that proactively verifies database connectivity.
    /// Options are automatically accessed from the builder.
    /// </summary>
    /// <param name="builder">The snapshot builder</param>
    /// <param name="unHealthyStatus">The status to return when check fails. Defaults to Unhealthy.</param>
    /// <param name="name">Optional name for the health check. Defaults to "Akka.Persistence.MongoDB.SnapshotStore.{id}.Connectivity"</param>
    /// <param name="tags">Optional tags for the health check. Defaults to ["akka", "persistence", "mongodb", "snapshot-store", "connectivity"]</param>
    /// <returns>The snapshot builder for chaining</returns>
    public static AkkaPersistenceSnapshotBuilder WithConnectivityCheck(
        this AkkaPersistenceSnapshotBuilder builder,
        HealthStatus unHealthyStatus = HealthStatus.Unhealthy,
        string? name = null,
        IEnumerable<string>? tags = null)
    {
        // Get options from builder - this is the Akka.Hosting 1.5.55.1 simplified API
        // First try MongoDbSnapshotOptions, then GridFS
        if (builder.Options is MongoDbSnapshotOptions snapshotOptions)
        {
            if (string.IsNullOrWhiteSpace(snapshotOptions.ConnectionString))
                throw new ArgumentException("ConnectionString must be set on MongoDbSnapshotOptions");

            var registration = new AkkaHealthCheckRegistration(
                name ?? $"Akka.Persistence.MongoDB.SnapshotStore.{snapshotOptions.Identifier}.Connectivity",
                new MongoDbSnapshotStoreConnectivityCheck(snapshotOptions.ConnectionString!, snapshotOptions.Identifier),
                unHealthyStatus,
                tags ?? new[] { "akka", "persistence", "mongodb", "snapshot-store", "connectivity" });

            return builder.WithCustomHealthCheck(registration);
        }
        else if (builder.Options is MongoDbGridFsSnapshotOptions gridFsOptions)
        {
            if (string.IsNullOrWhiteSpace(gridFsOptions.ConnectionString))
                throw new ArgumentException("ConnectionString must be set on MongoDbGridFsSnapshotOptions");

            var registration = new AkkaHealthCheckRegistration(
                name ?? $"Akka.Persistence.MongoDB.GridFsSnapshotStore.{gridFsOptions.Identifier}.Connectivity",
                new MongoDbGridFsSnapshotStoreConnectivityCheck(gridFsOptions.ConnectionString!, gridFsOptions.Identifier),
                unHealthyStatus,
                tags ?? new[] { "akka", "persistence", "mongodb", "gridfs", "snapshot-store", "connectivity" });

            return builder.WithCustomHealthCheck(registration);
        }
        else
        {
            throw new InvalidOperationException(
                $"Options must be either {nameof(MongoDbSnapshotOptions)} or {nameof(MongoDbGridFsSnapshotOptions)}");
        }
    }

    /// <summary>
    /// Adds a connectivity check for the MongoDB snapshot store (legacy API for backward compatibility).
    /// This is a liveness check that proactively verifies database connectivity.
    /// </summary>
    /// <param name="builder">The snapshot builder</param>
    /// <param name="snapshotOptions">The snapshot options containing connection details</param>
    /// <param name="unHealthyStatus">The status to return when check fails. Defaults to Unhealthy.</param>
    /// <param name="name">Optional name for the health check. Defaults to "Akka.Persistence.MongoDB.SnapshotStore.{id}.Connectivity"</param>
    /// <param name="tags">Optional tags for the health check. Defaults to ["akka", "persistence", "mongodb", "snapshot-store", "connectivity"]</param>
    /// <returns>The snapshot builder for chaining</returns>
    [Obsolete("Use the simplified API without passing snapshotOptions. Options are now automatically accessed from builder.Options. This overload will be removed in a future version.")]
    public static AkkaPersistenceSnapshotBuilder WithConnectivityCheck(
        this AkkaPersistenceSnapshotBuilder builder,
        MongoDbSnapshotOptions snapshotOptions,
        HealthStatus unHealthyStatus = HealthStatus.Unhealthy,
        string? name = null,
        string[]? tags = null)
    {
        if (snapshotOptions is null)
            throw new ArgumentNullException(nameof(snapshotOptions));

        if (string.IsNullOrWhiteSpace(snapshotOptions.ConnectionString))
            throw new ArgumentException("ConnectionString must be set on MongoDbSnapshotOptions", nameof(snapshotOptions));

        var registration = new AkkaHealthCheckRegistration(
            name ?? $"Akka.Persistence.MongoDB.SnapshotStore.{snapshotOptions.Identifier}.Connectivity",
            new MongoDbSnapshotStoreConnectivityCheck(snapshotOptions.ConnectionString, snapshotOptions.Identifier),
            unHealthyStatus,
            tags ?? new[] { "akka", "persistence", "mongodb", "snapshot-store", "connectivity" });

        return builder.WithCustomHealthCheck(registration);
    }

    /// <summary>
    /// Adds a connectivity check for the MongoDB GridFS snapshot store (legacy API for backward compatibility).
    /// This is a liveness check that proactively verifies database connectivity.
    /// </summary>
    /// <param name="builder">The snapshot builder</param>
    /// <param name="snapshotOptions">The GridFS snapshot options containing connection details</param>
    /// <param name="unHealthyStatus">The status to return when check fails. Defaults to Unhealthy.</param>
    /// <param name="name">Optional name for the health check. Defaults to "Akka.Persistence.MongoDB.GridFsSnapshotStore.{id}.Connectivity"</param>
    /// <param name="tags">Optional tags for the health check. Defaults to ["akka", "persistence", "mongodb", "gridfs", "snapshot-store", "connectivity"]</param>
    /// <returns>The snapshot builder for chaining</returns>
    [Obsolete("Use the simplified API without passing snapshotOptions. Options are now automatically accessed from builder.Options. This overload will be removed in a future version.")]
    public static AkkaPersistenceSnapshotBuilder WithConnectivityCheck(
        this AkkaPersistenceSnapshotBuilder builder,
        MongoDbGridFsSnapshotOptions snapshotOptions,
        HealthStatus unHealthyStatus = HealthStatus.Unhealthy,
        string? name = null,
        string[]? tags = null)
    {
        if (snapshotOptions is null)
            throw new ArgumentNullException(nameof(snapshotOptions));

        if (string.IsNullOrWhiteSpace(snapshotOptions.ConnectionString))
            throw new ArgumentException("ConnectionString must be set on MongoDbGridFsSnapshotOptions", nameof(snapshotOptions));

        var registration = new AkkaHealthCheckRegistration(
            name ?? $"Akka.Persistence.MongoDB.GridFsSnapshotStore.{snapshotOptions.Identifier}.Connectivity",
            new MongoDbGridFsSnapshotStoreConnectivityCheck(snapshotOptions.ConnectionString, snapshotOptions.Identifier),
            unHealthyStatus,
            tags ?? new[] { "akka", "persistence", "mongodb", "gridfs", "snapshot-store", "connectivity" });

        return builder.WithCustomHealthCheck(registration);
    }
}
