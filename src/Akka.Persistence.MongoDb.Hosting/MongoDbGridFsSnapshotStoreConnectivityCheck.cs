// -----------------------------------------------------------------------
//  <copyright file="MongoDbGridFsSnapshotStoreConnectivityCheck.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2025 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

#nullable enable
namespace Akka.Persistence.MongoDb.Hosting;

/// <summary>
/// Health check that verifies connectivity to the MongoDB database used by the GridFS snapshot store.
/// This is a liveness check that proactively verifies backend connectivity.
/// </summary>
public sealed class MongoDbGridFsSnapshotStoreConnectivityCheck : IAkkaHealthCheck
{
    private readonly string _connectionString;
    private readonly string _snapshotStoreId;

    public MongoDbGridFsSnapshotStoreConnectivityCheck(string connectionString, string snapshotStoreId)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _snapshotStoreId = snapshotStoreId ?? throw new ArgumentNullException(nameof(snapshotStoreId));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(AkkaHealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = new MongoClient(_connectionString);
            await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy($"MongoDB GridFS snapshot store '{_snapshotStoreId}' database connection successful");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy($"MongoDB GridFS snapshot store '{_snapshotStoreId}' database connectivity check timed out");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"MongoDB GridFS snapshot store '{_snapshotStoreId}' database connection failed", ex);
        }
    }
}
