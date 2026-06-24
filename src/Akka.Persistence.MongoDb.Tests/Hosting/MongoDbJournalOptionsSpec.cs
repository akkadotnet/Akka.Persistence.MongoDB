using System;
using System.IO;
using System.Text;
using Akka.Configuration;
using Akka.Persistence.MongoDb.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Akka.Persistence.MongoDb.Tests.Hosting
{
    public class MongoDbJournalOptionsSpec
    {
        [Fact(DisplayName = "MongoDbJournalOptions as default plugin should generate plugin setting")]
        public void DefaultPluginJournalOptionsTest()
        {
            var options = new MongoDbJournalOptions(true);
            var config = options.ToConfig();

            Assert.Equal("akka.persistence.journal.mongodb", config.GetString("akka.persistence.journal.plugin"));
            Assert.True(config.HasPath("akka.persistence.journal.mongodb"));
        }

        [Fact(DisplayName = "Empty MongoDbJournalOptions should equal empty config with default fallback")]
        public void DefaultJournalOptionsTest()
        {
            var options = new MongoDbJournalOptions(false);
            var emptyRootConfig = options.ToConfig().WithFallback(options.DefaultConfig);
            var baseRootConfig = Config.Empty
                .WithFallback(MongoDbPersistence.DefaultConfiguration());

            Assert.Equal(baseRootConfig.GetString("akka.persistence.journal.plugin"), emptyRootConfig.GetString("akka.persistence.journal.plugin"));

            var config = emptyRootConfig.GetConfig("akka.persistence.journal.mongodb");
            var baseConfig = baseRootConfig.GetConfig("akka.persistence.journal.mongodb");
            Assert.NotNull(config);
            Assert.NotNull(baseConfig);

            Assert.Equal(baseConfig.GetString("class"), config.GetString("class"));
            Assert.Equal(baseConfig.GetString("connection-string"), config.GetString("connection-string"));
            Assert.Equal(baseConfig.GetBoolean("use-write-transaction"), config.GetBoolean("use-write-transaction"));
            Assert.Equal(baseConfig.GetBoolean("use-read-transaction"), config.GetBoolean("use-read-transaction"));
            Assert.Equal(baseConfig.GetBoolean("read-batch-size"), config.GetBoolean("read-batch-size"));
            Assert.Equal(baseConfig.GetBoolean("auto-initialize"), config.GetBoolean("auto-initialize"));
            Assert.Equal(baseConfig.GetString("plugin-dispatcher"), config.GetString("plugin-dispatcher"));
            Assert.Equal(baseConfig.GetString("collection"), config.GetString("collection"));
            Assert.Equal(baseConfig.GetString("metadata-collection"), config.GetString("metadata-collection"));
            Assert.Equal(baseConfig.GetBoolean("legacy-serialization"), config.GetBoolean("legacy-serialization"));
            Assert.Equal(baseConfig.GetTimeSpan("call-timeout"), config.GetTimeSpan("call-timeout"));
        }

        [Fact(DisplayName = "Empty MongoDbJournalOptions with custom identifier should equal empty config with default fallback")]
        public void CustomIdJournalOptionsTest()
        {
            var options = new MongoDbJournalOptions(false, "custom");
            var emptyRootConfig = options.ToConfig().WithFallback(options.DefaultConfig);
            var baseRootConfig = Config.Empty
                .WithFallback(MongoDbPersistence.DefaultConfiguration());

            Assert.Equal(baseRootConfig.GetString("akka.persistence.journal.plugin"), emptyRootConfig.GetString("akka.persistence.journal.plugin"));

            var config = emptyRootConfig.GetConfig("akka.persistence.journal.custom");
            var baseConfig = baseRootConfig.GetConfig("akka.persistence.journal.mongodb");
            Assert.NotNull(config);
            Assert.NotNull(baseConfig);

            Assert.Equal(baseConfig.GetString("class"), config.GetString("class"));
            Assert.Equal(baseConfig.GetString("connection-string"), config.GetString("connection-string"));
            Assert.Equal(baseConfig.GetBoolean("use-write-transaction"), config.GetBoolean("use-write-transaction"));
            Assert.Equal(baseConfig.GetBoolean("use-read-transaction"), config.GetBoolean("use-read-transaction"));
            Assert.Equal(baseConfig.GetBoolean("read-batch-size"), config.GetBoolean("read-batch-size"));
            Assert.Equal(baseConfig.GetBoolean("auto-initialize"), config.GetBoolean("auto-initialize"));
            Assert.Equal(baseConfig.GetString("plugin-dispatcher"), config.GetString("plugin-dispatcher"));
            Assert.Equal(baseConfig.GetString("collection"), config.GetString("collection"));
            Assert.Equal(baseConfig.GetString("metadata-collection"), config.GetString("metadata-collection"));
            Assert.Equal(baseConfig.GetBoolean("legacy-serialization"), config.GetBoolean("legacy-serialization"));
            Assert.Equal(baseConfig.GetTimeSpan("call-timeout"), config.GetTimeSpan("call-timeout"));
        }

        [Fact(DisplayName = "MongoDbJournalOptions should generate proper config")]
        public void JournalOptionsTest()
        {
            var options = new MongoDbJournalOptions(true)
            {
                Identifier = "custom",
                AutoInitialize = true,
                ConnectionString = "testConnection",
                Collection = "testCollection",
                MetadataCollection = "metadataCollection",
                UseWriteTransaction = true,
                UseReadTransaction = true,
                ReadBatchSize = 16,
                LegacySerialization = true,
                CallTimeout = TimeSpan.FromHours(2)
            };

            var baseConfig = options.ToConfig();

            Assert.Equal("akka.persistence.journal.custom", baseConfig.GetString("akka.persistence.journal.plugin"));

            var config = baseConfig.GetConfig("akka.persistence.journal.custom");
            Assert.NotNull(config);
            Assert.Equal(options.ConnectionString, config.GetString("connection-string"));
            Assert.Equal(options.AutoInitialize, config.GetBoolean("auto-initialize"));
            Assert.Equal(options.Collection, config.GetString("collection"));
            Assert.Equal(options.MetadataCollection, config.GetString("metadata-collection"));
            Assert.Equal(options.UseWriteTransaction.Value, config.GetBoolean("use-write-transaction"));
            Assert.Equal(options.UseReadTransaction.Value, config.GetBoolean("use-read-transaction"));
            Assert.NotEqual("off", config.GetString("read-batch-size").ToLowerInvariant());
            Assert.NotEqual("false", config.GetString("read-batch-size").ToLowerInvariant());
            Assert.Equal(options.ReadBatchSize.Value, config.GetInt("read-batch-size"));
            Assert.Equal(options.LegacySerialization.Value, config.GetBoolean("legacy-serialization"));
            Assert.Equal(options.CallTimeout.Value, config.GetTimeSpan("call-timeout"));
        }

        const string Json = @"
        {
          ""Logging"": {
            ""LogLevel"": {
              ""Default"": ""Information"",
              ""Microsoft.AspNetCore"": ""Warning""
            }
          },
          ""Akka"": {
            ""JournalOptions"": {
              ""ConnectionString"": ""mongodb://localhost:27017"",
              ""UseWriteTransaction"": ""true"",
              ""UseReadTransaction"": ""true"",
              ""ReadBatchSize"": 16,
              ""Identifier"": ""custommongodb"",
              ""AutoInitialize"": true,
              ""IsDefaultPlugin"": false,
              ""Collection"": ""CustomEnventJournalCollection"",
              ""MetadataCollection"": ""CustomMetadataCollection"",
              ""LegacySerialization"" : ""true"",
              ""CallTimeout"": ""00:10:00"",
              ""Serializer"": ""hyperion"",
            }
          }
        }";

        [Fact(DisplayName = "MongoDbJournalOptions should be bindable to IConfiguration")]
        public void JournalOptionsIConfigurationBindingTest()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Json));
            var jsonConfig = new ConfigurationBuilder().AddJsonStream(stream).Build();

            var options = jsonConfig.GetSection("Akka:JournalOptions").Get<MongoDbJournalOptions>();
            Assert.Equal("mongodb://localhost:27017", options.ConnectionString);
            Assert.True(options.UseWriteTransaction);
            Assert.True(options.UseReadTransaction);
            Assert.Equal(16, options.ReadBatchSize);
            Assert.Equal("custommongodb", options.Identifier);
            Assert.True(options.AutoInitialize);
            Assert.False(options.IsDefaultPlugin);
            Assert.Equal("CustomEnventJournalCollection", options.Collection);
            Assert.Equal("CustomMetadataCollection", options.MetadataCollection);
            Assert.True(options.LegacySerialization);
            Assert.Equal(TimeSpan.FromMinutes(10), options.CallTimeout);
            Assert.Equal("hyperion", options.Serializer);
        }
    }
}