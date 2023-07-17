using System;
using System.IO;
using System.Text;
using Akka.Configuration;
using Akka.Persistence.MongoDb.Hosting;
using FluentAssertions;
using FluentAssertions.Extensions;
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

            config.GetString("akka.persistence.journal.plugin").Should().Be("akka.persistence.journal.mongodb");
            config.HasPath("akka.persistence.journal.mongodb").Should().BeTrue();
        }

        [Fact(DisplayName = "Empty MongoDbJournalOptions should equal empty config with default fallback")]
        public void DefaultJournalOptionsTest()
        {
            var options = new MongoDbJournalOptions(false);
            var emptyRootConfig = options.ToConfig().WithFallback(options.DefaultConfig);
            var baseRootConfig = Config.Empty
                .WithFallback(MongoDbPersistence.DefaultConfiguration());

            emptyRootConfig.GetString("akka.persistence.journal.plugin").Should().Be(baseRootConfig.GetString("akka.persistence.journal.plugin"));

            var config = emptyRootConfig.GetConfig("akka.persistence.journal.mongodb");
            var baseConfig = baseRootConfig.GetConfig("akka.persistence.journal.mongodb");
            config.Should().NotBeNull();
            baseConfig.Should().NotBeNull();

            config.GetString("class").Should().Be(baseConfig.GetString("class"));
            config.GetString("connection-string").Should().Be(baseConfig.GetString("connection-string"));
            config.GetBoolean("use-write-transaction").Should().Be(baseConfig.GetBoolean("use-write-transaction"));
            config.GetBoolean("auto-initialize").Should().Be(baseConfig.GetBoolean("auto-initialize"));
            config.GetString("plugin-dispatcher").Should().Be(baseConfig.GetString("plugin-dispatcher"));
            config.GetString("collection").Should().Be(baseConfig.GetString("collection"));
            config.GetString("metadata-collection").Should().Be(baseConfig.GetString("metadata-collection"));
            config.GetBoolean("legacy-serialization").Should().Be(baseConfig.GetBoolean("legacy-serialization"));
            config.GetTimeSpan("call-timeout").Should().Be(baseConfig.GetTimeSpan("call-timeout"));
        }

        [Fact(DisplayName = "Empty MongoDbJournalOptions with custom identifier should equal empty config with default fallback")]
        public void CustomIdJournalOptionsTest()
        {
            var options = new MongoDbJournalOptions(false, "custom");
            var emptyRootConfig = options.ToConfig().WithFallback(options.DefaultConfig);
            var baseRootConfig = Config.Empty
                .WithFallback(MongoDbPersistence.DefaultConfiguration());

            emptyRootConfig.GetString("akka.persistence.journal.plugin").Should().Be(baseRootConfig.GetString("akka.persistence.journal.plugin"));

            var config = emptyRootConfig.GetConfig("akka.persistence.journal.custom");
            var baseConfig = baseRootConfig.GetConfig("akka.persistence.journal.mongodb");
            config.Should().NotBeNull();
            baseConfig.Should().NotBeNull();

            config.GetString("class").Should().Be(baseConfig.GetString("class"));
            config.GetString("connection-string").Should().Be(baseConfig.GetString("connection-string"));
            config.GetBoolean("use-write-transaction").Should().Be(baseConfig.GetBoolean("use-write-transaction"));
            config.GetBoolean("auto-initialize").Should().Be(baseConfig.GetBoolean("auto-initialize"));
            config.GetString("plugin-dispatcher").Should().Be(baseConfig.GetString("plugin-dispatcher"));
            config.GetString("collection").Should().Be(baseConfig.GetString("collection"));
            config.GetString("metadata-collection").Should().Be(baseConfig.GetString("metadata-collection"));
            config.GetBoolean("legacy-serialization").Should().Be(baseConfig.GetBoolean("legacy-serialization"));
            config.GetTimeSpan("call-timeout").Should().Be(baseConfig.GetTimeSpan("call-timeout"));
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
                LegacySerialization = true,
                CallTimeout = TimeSpan.FromHours(2)
            };

            var baseConfig = options.ToConfig();

            baseConfig.GetString("akka.persistence.journal.plugin").Should().Be("akka.persistence.journal.custom");

            var config = baseConfig.GetConfig("akka.persistence.journal.custom");
            config.Should().NotBeNull();
            config.GetString("connection-string").Should().Be(options.ConnectionString);
            config.GetBoolean("auto-initialize").Should().Be(options.AutoInitialize);
            config.GetString("collection").Should().Be(options.Collection);
            config.GetString("metadata-collection").Should().Be(options.MetadataCollection);
            config.GetBoolean("use-write-transaction").Should().Be(options.UseWriteTransaction);
            config.GetBoolean("legacy-serialization").Should().Be(options.LegacySerialization);
            config.GetTimeSpan("call-timeout").Should().Be(options.CallTimeout);
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
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(Json));
            var jsonConfig = new ConfigurationBuilder().AddJsonStream(stream).Build();

            var options = jsonConfig.GetSection("Akka:JournalOptions").Get<MongoDbJournalOptions>();
            options.ConnectionString.Should().Be("mongodb://localhost:27017");
            options.UseWriteTransaction.Should().BeTrue();
            options.Identifier.Should().Be("custommongodb");
            options.AutoInitialize.Should().BeTrue();
            options.IsDefaultPlugin.Should().BeFalse();
            options.Collection.Should().Be("CustomEnventJournalCollection");
            options.MetadataCollection.Should().Be("CustomMetadataCollection");
            options.LegacySerialization.Should().BeTrue();
            options.CallTimeout.Should().Be(10.Minutes());
            options.Serializer.Should().Be("hyperion");

            // Dispose called here as project not using latest language features.
            stream.Dispose();
        }
    }
}