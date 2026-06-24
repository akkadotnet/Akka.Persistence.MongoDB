// -----------------------------------------------------------------------
//  <copyright file="MongoDbConnectivityCheckSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2025 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Hosting;
using Akka.Persistence.MongoDb.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Akka.Persistence.MongoDb.Tests.Hosting;

public class MongoDbConnectivityCheckSpec : IClassFixture<DatabaseFixture>
{
    private const string InvalidConnectionString = "mongodb://invalid-host:27017/akka-test";
    private readonly ITestOutputHelper _output;
    private readonly DatabaseFixture _fixture;
    private readonly string _validConnectionString;

    public MongoDbConnectivityCheckSpec(ITestOutputHelper output, DatabaseFixture fixture)
    {
        _output = output;
        _fixture = fixture;
        _validConnectionString = fixture.ConnectionString;
    }

    // Happy path tests - verify health checks work with real MongoDB
    [Fact]
    public async Task Journal_Connectivity_Check_Should_Return_Healthy_When_Connected()
    {
        // Arrange
        var check = new MongoDbJournalConnectivityCheck(_validConnectionString, "mongodb");
        var context = new AkkaHealthCheckContext(null!);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Null(result.Exception);
        Assert.Contains("successful", result.Description);
    }

    [Fact]
    public async Task Snapshot_Connectivity_Check_Should_Return_Healthy_When_Connected()
    {
        // Arrange
        var check = new MongoDbSnapshotStoreConnectivityCheck(_validConnectionString, "mongodb");
        var context = new AkkaHealthCheckContext(null!);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Null(result.Exception);
        Assert.Contains("successful", result.Description);
    }

    [Fact]
    public async Task GridFS_Snapshot_Connectivity_Check_Should_Return_Healthy_When_Connected()
    {
        // Arrange
        var check = new MongoDbGridFsSnapshotStoreConnectivityCheck(_validConnectionString, "mongodb-gridfs");
        var context = new AkkaHealthCheckContext(null!);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Null(result.Exception);
        Assert.Contains("successful", result.Description);
    }

    // Unhappy path tests - verify health checks detect connection failures
    [Fact]
    public async Task Journal_Connectivity_Check_Should_Return_Unhealthy_When_Disconnected()
    {
        // Arrange
        var check = new MongoDbJournalConnectivityCheck(InvalidConnectionString, "mongodb");
        var context = new AkkaHealthCheckContext(null!);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public async Task Snapshot_Connectivity_Check_Should_Return_Unhealthy_When_Disconnected()
    {
        // Arrange
        var check = new MongoDbSnapshotStoreConnectivityCheck(InvalidConnectionString, "mongodb");
        var context = new AkkaHealthCheckContext(null!);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public void Journal_Connectivity_Check_Should_Require_ConnectionString()
    {
        // Act & Assert
        var action = () => new MongoDbJournalConnectivityCheck(null!, "mongodb");
        Assert.Equal("connectionString", Assert.Throws<ArgumentNullException>(action).ParamName);
    }

    [Fact]
    public void Journal_Connectivity_Check_Should_Require_JournalId()
    {
        // Act & Assert
        var action = () => new MongoDbJournalConnectivityCheck("mongodb://localhost", null!);
        Assert.Equal("journalId", Assert.Throws<ArgumentNullException>(action).ParamName);
    }

    [Fact]
    public void Snapshot_Connectivity_Check_Should_Require_ConnectionString()
    {
        // Act & Assert
        var action = () => new MongoDbSnapshotStoreConnectivityCheck(null!, "mongodb");
        Assert.Equal("connectionString", Assert.Throws<ArgumentNullException>(action).ParamName);
    }

    [Fact]
    public void Snapshot_Connectivity_Check_Should_Require_SnapshotStoreId()
    {
        // Act & Assert
        var action = () => new MongoDbSnapshotStoreConnectivityCheck("mongodb://localhost", null!);
        Assert.Equal("snapshotStoreId", Assert.Throws<ArgumentNullException>(action).ParamName);
    }

    [Fact]
    public async Task GridFS_Snapshot_Connectivity_Check_Should_Return_Unhealthy_When_Disconnected()
    {
        // Arrange
        var check = new MongoDbGridFsSnapshotStoreConnectivityCheck(InvalidConnectionString, "mongodb-gridfs");
        var context = new AkkaHealthCheckContext(null!);

        // Act
        var result = await check.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public void GridFS_Snapshot_Connectivity_Check_Should_Require_ConnectionString()
    {
        // Act & Assert
        var action = () => new MongoDbGridFsSnapshotStoreConnectivityCheck(null!, "mongodb-gridfs");
        Assert.Equal("connectionString", Assert.Throws<ArgumentNullException>(action).ParamName);
    }

    [Fact]
    public void GridFS_Snapshot_Connectivity_Check_Should_Require_SnapshotStoreId()
    {
        // Act & Assert
        var action = () => new MongoDbGridFsSnapshotStoreConnectivityCheck("mongodb://localhost", null!);
        Assert.Equal("snapshotStoreId", Assert.Throws<ArgumentNullException>(action).ParamName);
    }
}
