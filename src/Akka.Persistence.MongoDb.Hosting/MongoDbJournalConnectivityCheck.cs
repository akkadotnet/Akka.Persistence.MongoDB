// -----------------------------------------------------------------------
//  <copyright file="MongoDbJournalConnectivityCheck.cs" company="Akka.NET Project">
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
/// Health check that verifies connectivity to the MongoDB database used by the journal.
/// This is a liveness check that proactively verifies backend connectivity.
/// </summary>
public sealed class MongoDbJournalConnectivityCheck : IAkkaHealthCheck
{
    private readonly string _connectionString;
    private readonly string _journalId;

    public MongoDbJournalConnectivityCheck(string connectionString, string journalId)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _journalId = journalId ?? throw new ArgumentNullException(nameof(journalId));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(AkkaHealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // MongoDB's MongoClient doesn't implement IDisposable, so disposal is not needed
            // The driver manages its own connection pooling and cleanup internally
            var client = new MongoClient(_connectionString);
            await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy($"MongoDB journal '{_journalId}' database connection successful");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy($"MongoDB journal '{_journalId}' database connectivity check timed out");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"MongoDB journal '{_journalId}' database connection failed", ex);
        }
    }
}
