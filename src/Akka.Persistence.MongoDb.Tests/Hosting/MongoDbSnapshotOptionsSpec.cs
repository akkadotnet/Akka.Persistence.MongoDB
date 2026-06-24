using System;
using System.IO;
using System.Text;
using Akka.Configuration;
using Akka.Persistence.MongoDb.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Akka.Persistence.MongoDb.Tests.Hosting
{
    public class MongoDbSnapshotOptionsSpec
    {
        [Fact(DisplayName = "MongoDbSnapshotOptions as default plugin should generate plugin setting")]
        public void DefaultPluginSnapshotOptionsTest()
        {
            var options = new MongoDbSnapshotOptions(true);
            var config = options.ToConfig();

            Assert.Equal("akka.persistence.snapshot-store.mongodb", config.GetString("akka.persistence.snapshot-store.plugin"));
            Assert.True(config.HasPath("akka.persistence.snapshot-store.mongodb"));
        }

        [Fact(DisplayName = "Empty MongoDbSnapshotOptions with default fallback should return default config")]
        public void DefaultSnapshotOptionsTest()
        {
            var options = new MongoDbSnapshotOptions(false);
            var emptyRootConfig = options.ToConfig().WithFallback(options.DefaultConfig);
            var baseRootConfig = Config.Empty
                .WithFallback(MongoDbPersistence.DefaultConfiguration());

            Assert.Equal(baseRootConfig.GetString("akka.persistence.snapshot-store.plugin"), emptyRootConfig.GetString("akka.persistence.snapshot-store.plugin"));

            var config = emptyRootConfig.GetConfig("akka.persistence.snapshot-store.mongodb");
            var baseConfig = baseRootConfig.GetConfig("akka.persistence.snapshot-store.mongodb");
            Assert.NotNull(config);
            Assert.NotNull(baseConfig);

            Assert.Equal(Type.GetType(baseConfig.GetString("class")), Type.GetType(config.GetString("class")));
            Assert.Equal(baseConfig.GetString("connection-string"), config.GetString("connection-string"));
            Assert.Equal(baseConfig.GetBoolean("use-write-transaction"), config.GetBoolean("use-write-transaction"));
            Assert.Equal(baseConfig.GetBoolean("use-read-transaction"), config.GetBoolean("use-read-transaction"));
            Assert.Equal(baseConfig.GetBoolean("auto-initialize"), config.GetBoolean("auto-initialize"));
            Assert.Equal(baseConfig.GetString("plugin-dispatcher"), config.GetString("plugin-dispatcher"));
            Assert.Equal(baseConfig.GetString("collection"), config.GetString("collection"));
            Assert.Equal(baseConfig.GetBoolean("legacy-serialization"), config.GetBoolean("legacy-serialization"));
            Assert.Equal(baseConfig.GetTimeSpan("call-timeout"), config.GetTimeSpan("call-timeout"));
        }

        [Fact(DisplayName = "Empty MongoDbSnapshotOptions with custom identifier should equal empty config with default fallback")]
        public void CustomIdSnapshotOptionsTest()
        {
            var options = new MongoDbSnapshotOptions(false, "custom");
            var emptyRootConfig = options.ToConfig().WithFallback(options.DefaultConfig);
            var baseRootConfig = Config.Empty
                .WithFallback(MongoDbPersistence.DefaultConfiguration());

            Assert.Equal(baseRootConfig.GetString("akka.persistence.snapshot-store.plugin"), emptyRootConfig.GetString("akka.persistence.snapshot-store.plugin"));

            var config = emptyRootConfig.GetConfig("akka.persistence.snapshot-store.custom");
            var baseConfig = baseRootConfig.GetConfig("akka.persistence.snapshot-store.mongodb");
            Assert.NotNull(config);
            Assert.NotNull(baseConfig);

            Assert.Equal(Type.GetType(baseConfig.GetString("class")), Type.GetType(config.GetString("class")));
            Assert.Equal(baseConfig.GetString("connection-string"), config.GetString("connection-string"));
            Assert.Equal(baseConfig.GetBoolean("use-write-transaction"), config.GetBoolean("use-write-transaction"));
            Assert.Equal(baseConfig.GetBoolean("use-read-transaction"), config.GetBoolean("use-read-transaction"));
            Assert.Equal(baseConfig.GetBoolean("auto-initialize"), config.GetBoolean("auto-initialize"));
            Assert.Equal(baseConfig.GetString("plugin-dispatcher"), config.GetString("plugin-dispatcher"));
            Assert.Equal(baseConfig.GetString("collection"), config.GetString("collection"));
            Assert.Equal(baseConfig.GetBoolean("legacy-serialization"), config.GetBoolean("legacy-serialization"));
            Assert.Equal(baseConfig.GetTimeSpan("call-timeout"), config.GetTimeSpan("call-timeout"));
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
                UseReadTransaction = true,
                LegacySerialization = true,
                CallTimeout = TimeSpan.FromHours(2)
            };

            var baseConfig = options.ToConfig()
                .WithFallback(MongoDbPersistence.DefaultConfiguration());

            Assert.Equal("akka.persistence.snapshot-store.custom", baseConfig.GetString("akka.persistence.snapshot-store.plugin"));

            var config = baseConfig.GetConfig("akka.persistence.snapshot-store.custom");
            Assert.NotNull(config);
            Assert.Equal(options.ConnectionString, config.GetString("connection-string"));
            Assert.Equal(options.AutoInitialize, config.GetBoolean("auto-initialize"));
            Assert.Equal(options.Collection, config.GetString("collection"));
            Assert.Equal(options.UseWriteTransaction.Value, config.GetBoolean("use-write-transaction"));
            Assert.Equal(options.UseReadTransaction.Value, config.GetBoolean("use-read-transaction"));
            Assert.Equal(options.LegacySerialization.Value, config.GetBoolean("legacy-serialization"));
            Assert.Equal(options.CallTimeout.Value, config.GetTimeSpan("call-timeout"));
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
                  ""UseReadTransaction"": ""true"",
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

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var jsonConfig = new ConfigurationBuilder().AddJsonStream(stream).Build();

            var options = jsonConfig.GetSection("Akka:SnapshotOptions").Get<MongoDbSnapshotOptions>();
            Assert.Equal("mongodb://localhost:27017", options.ConnectionString);
            Assert.True(options.UseWriteTransaction);
            Assert.True(options.UseReadTransaction);
            Assert.Equal("custommongodb", options.Identifier);
            Assert.True(options.AutoInitialize);
            Assert.False(options.IsDefaultPlugin);
            Assert.Equal("CustomEnventJournalCollection", options.Collection);
            Assert.True(options.LegacySerialization);
            Assert.Equal(TimeSpan.FromMinutes(10), options.CallTimeout);
            Assert.Equal("hyperion", options.Serializer);
        }
    }
}