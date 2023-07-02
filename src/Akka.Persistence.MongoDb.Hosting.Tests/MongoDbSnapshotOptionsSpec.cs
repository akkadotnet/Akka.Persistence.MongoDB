using System;
using System.IO;
using System.Text;
using Akka.Configuration;
using FluentAssertions;
using FluentAssertions.Extensions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Akka.Persistence.MongoDb.Hosting.Tests
{
    public class MongoDbSnapshotOptionsSpec
    {
        [Fact(DisplayName = "MongoDbSnapshotOptions as default plugin should generate plugin setting")]
        public void DefaultPluginSnapshotOptionsTest()
        {
            var options = new MongoDbSnapshotOptions(true);
            var config = options.ToConfig();

            config.GetString("akka.persistence.snapshot-store.plugin").Should().Be("akka.persistence.snapshot-store.mongodb");
            config.HasPath("akka.persistence.snapshot-store.mongodb").Should().BeTrue();
        }

        [Fact(DisplayName = "Empty MongoDbSnapshotOptions with default fallback should return default config")]
        public void DefaultSnapshotOptionsTest()
        {
            var options = new MongoDbSnapshotOptions(false);
            var emptyRootConfig = options.ToConfig().WithFallback(options.DefaultConfig);
            var baseRootConfig = Config.Empty
                .WithFallback(MongoDbPersistence.DefaultConfiguration());

            emptyRootConfig.GetString("akka.persistence.snapshot-store.plugin").Should().Be(baseRootConfig.GetString("akka.persistence.snapshot-store.plugin"));

            var config = emptyRootConfig.GetConfig("akka.persistence.snapshot-store.mongodb");
            var baseConfig = baseRootConfig.GetConfig("akka.persistence.snapshot-store.mongodb");
            config.Should().NotBeNull();
            baseConfig.Should().NotBeNull();

            config.GetString("class").Should().Be(baseConfig.GetString("class"));
            config.GetString("connection-string").Should().Be(baseConfig.GetString("connection-string"));
            config.GetBoolean("use-write-transaction").Should().Be(baseConfig.GetBoolean("use-write-transaction"));
            config.GetBoolean("auto-initialize").Should().Be(baseConfig.GetBoolean("auto-initialize"));
            config.GetString("plugin-dispatcher").Should().Be(baseConfig.GetString("plugin-dispatcher"));
            config.GetString("collection").Should().Be(baseConfig.GetString("collection"));
            config.GetBoolean("legacy-serialization").Should().Be(baseConfig.GetBoolean("legacy-serialization"));
            config.GetTimeSpan("call-timeout").Should().Be(baseConfig.GetTimeSpan("call-timeout"));
        }

        [Fact(DisplayName = "Empty MongoDbSnapshotOptions with custom identifier should equal empty config with default fallback")]
        public void CustomIdSnapshotOptionsTest()
        {
            var options = new MongoDbSnapshotOptions(false, "custom");
            var emptyRootConfig = options.ToConfig().WithFallback(options.DefaultConfig);
            var baseRootConfig = Config.Empty
                .WithFallback(MongoDbPersistence.DefaultConfiguration());

            emptyRootConfig.GetString("akka.persistence.snapshot-store.plugin").Should().Be(baseRootConfig.GetString("akka.persistence.snapshot-store.plugin"));

            var config = emptyRootConfig.GetConfig("akka.persistence.snapshot-store.custom");
            var baseConfig = baseRootConfig.GetConfig("akka.persistence.snapshot-store.mongodb");
            config.Should().NotBeNull();
            baseConfig.Should().NotBeNull();

            config.GetString("class").Should().Be(baseConfig.GetString("class"));
            config.GetString("connection-string").Should().Be(baseConfig.GetString("connection-string"));
            config.GetBoolean("use-write-transaction").Should().Be(baseConfig.GetBoolean("use-write-transaction"));
            config.GetBoolean("auto-initialize").Should().Be(baseConfig.GetBoolean("auto-initialize"));
            config.GetString("plugin-dispatcher").Should().Be(baseConfig.GetString("plugin-dispatcher"));
            config.GetString("collection").Should().Be(baseConfig.GetString("collection"));
            config.GetBoolean("legacy-serialization").Should().Be(baseConfig.GetBoolean("legacy-serialization"));
            config.GetTimeSpan("call-timeout").Should().Be(baseConfig.GetTimeSpan("call-timeout"));
        }

        [Fact(DisplayName = "MongoDbSnapshotOptions should generate proper config")]
        public void SnapshotOptionsTest()
        {
            var options = new MongoDbSnapshotOptions(true)
            {
                Identifier = "custom",
                AutoInitialize = true,
                ConnectionString = "testConnection",
                Collection = "testCollection",
                UseWriteTransaction = true,
                LegacySerialization = true,
                CallTimeout = TimeSpan.FromHours(2)
            };

            var baseConfig = options.ToConfig()
                .WithFallback(MongoDbPersistence.DefaultConfiguration());

            baseConfig.GetString("akka.persistence.snapshot-store.plugin").Should().Be("akka.persistence.snapshot-store.custom");

            var config = baseConfig.GetConfig("akka.persistence.snapshot-store.custom");
            config.Should().NotBeNull();
            config.GetString("connection-string").Should().Be(options.ConnectionString);
            config.GetBoolean("auto-initialize").Should().Be(options.AutoInitialize);
            config.GetString("collection").Should().Be(options.Collection);
            config.GetBoolean("use-write-transaction").Should().Be(options.UseWriteTransaction);
            config.GetBoolean("legacy-serialization").Should().Be(options.LegacySerialization);
            config.GetTimeSpan("call-timeout").Should().Be(options.CallTimeout);
        }

        [Fact(DisplayName = "MongoDbSnapshotOptions should be bindable to IConfiguration")]
        public void SnapshotOptionsIConfigurationBindingTest()
        {
            const string json = @"
            {
              ""Logging"": {
                ""LogLevel"": {
                  ""Default"": ""Information"",
                  ""Microsoft.AspNetCore"": ""Warning""
                }
              },
              ""Akka"": {
                ""SnapshotOptions"": {
                  ""ConnectionString"": ""mongodb://localhost:27017"",
                  ""UseWriteTransaction"": ""true"",
                  ""Identifier"": ""custommongodb"",
                  ""AutoInitialize"": true,
                  ""IsDefaultPlugin"": false,
                  
                  ""Collection"": ""CustomEnventJournalCollection"",
                  ""LegacySerialization"" : ""true"",
                  ""CallTimeout"": ""00:10:00"",
                  ""Serializer"": ""hyperion"",
                }
              }
            }";

            var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var jsonConfig = new ConfigurationBuilder().AddJsonStream(stream).Build();

            var options = jsonConfig.GetSection("Akka:SnapshotOptions").Get<MongoDbSnapshotOptions>();
            options.ConnectionString.Should().Be("mongodb://localhost:27017");
            options.UseWriteTransaction.Should().BeTrue();
            options.Identifier.Should().Be("custommongodb");
            options.AutoInitialize.Should().BeTrue();
            options.IsDefaultPlugin.Should().BeFalse();
            options.Collection.Should().Be("CustomEnventJournalCollection");
            options.LegacySerialization.Should().BeTrue();
            options.CallTimeout.Should().Be(10.Minutes());
            options.Serializer.Should().Be("hyperion");

            // Dispose called here as project not using latest language features.
            stream.Dispose();
        }
    }
}