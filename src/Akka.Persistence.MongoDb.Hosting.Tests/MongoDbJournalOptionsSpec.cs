using Akka.Configuration;
using FluentAssertions;
using Xunit;

namespace Akka.Persistence.MongoDb.Hosting.Tests
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

            AssertJournalConfig(config, baseConfig);
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

            AssertJournalConfig(config, baseConfig);
        }

//        [Fact(DisplayName = "MongoDbJournalOptions should generate proper config")]
//        public void JournalOptionsTest()
//        {
//            var options = new MongoDbJournalOptions(true)
//            {
//                Identifier = "custom",
//                AutoInitialize = true,
//                ConnectionString = "testConnection",
//                Collection = "testCollection",
//                MetadataCollection = "metadataCollection",
//                UseWriteTransaction = true,
//                LegacySerialization = true,
//                CallTimeout = TimeSpan.FromHours(2)
//            };
//            options.Adapters.AddWriteEventAdapter<EventAdapters.EventMapper1>("mapper1", new[] { typeof(EventAdapters.Event1) });
//            options.Adapters.AddReadEventAdapter<EventAdapters.ReadAdapter>("reader1", new[] { typeof(EventAdapters.Event1) });
//            options.Adapters.AddEventAdapter<EventAdapters.ComboAdapter>("combo", boundTypes: new[] { typeof(EventAdapters.Event2) });
//            options.Adapters.AddWriteEventAdapter<EventAdapters.Tagger>("tagger", boundTypes: new[] { typeof(EventAdapters.Event1), typeof(EventAdapters.Event2) });

//            var baseConfig = options.ToConfig();

//            baseConfig.GetString("akka.persistence.journal.plugin").Should().Be("akka.persistence.journal.custom");

//            var config = baseConfig.GetConfig("akka.persistence.journal.custom");
//            config.Should().NotBeNull();
//            config.GetString("connection-string").Should().Be(options.ConnectionString);
//            config.GetTimeSpan("connection-timeout").Should().Be(options.ConnectionTimeout);
//            config.GetString("schema-name").Should().Be(options.SchemaName);
//            config.GetString("table-name").Should().Be(options.TableName);
//            config.GetBoolean("auto-initialize").Should().Be(options.AutoInitialize);
//            config.GetString("metadata-table-name").Should().Be(options.MetadataTableName);
//            config.GetBoolean("sequential-access").Should().Be(options.SequentialAccess);
//            config.GetString("stored-as").Should().Be(options.StoredAs.ToHocon());
//            config.GetBoolean("use-bigint-identity-for-ordering-column").Should().Be(options.UseBigIntIdentityForOrderingColumn);

//            config.GetStringList($"event-adapter-bindings.\"{typeof(EventAdapters.Event1).TypeQualifiedName()}\"").Should()
//                .BeEquivalentTo("mapper1", "reader1", "tagger");
//            config.GetStringList($"event-adapter-bindings.\"{typeof(EventAdapters.Event2).TypeQualifiedName()}\"").Should()
//                .BeEquivalentTo("combo", "tagger");

//            config.GetString("event-adapters.mapper1").Should().Be(typeof(EventAdapters.EventMapper1).TypeQualifiedName());
//            config.GetString("event-adapters.reader1").Should().Be(typeof(EventAdapters.ReadAdapter).TypeQualifiedName());
//            config.GetString("event-adapters.combo").Should().Be(typeof(EventAdapters.ComboAdapter).TypeQualifiedName());
//            config.GetString("event-adapters.tagger").Should().Be(typeof(EventAdapters.Tagger).TypeQualifiedName());

//        }

//        const string Json = @"
//{
//  ""Logging"": {
//    ""LogLevel"": {
//      ""Default"": ""Information"",
//      ""Microsoft.AspNetCore"": ""Warning""
//    }
//  },
//  ""Akka"": {
//    ""JournalOptions"": {
//      ""StoredAs"": ""JsonB"",
//      ""UseBigIntIdentityForOrderingColumn"": true,

//      ""ConnectionString"": ""Server=localhost,1533;Database=Akka;User Id=sa;"",
//      ""ConnectionTimeout"": ""00:00:55"",
//      ""SchemaName"": ""schema"",
//      ""TableName"" : ""journal"",
//      ""MetadataTableName"": ""meta"",
//      ""SequentialAccess"": false,

//      ""IsDefaultPlugin"": false,
//      ""Identifier"": ""custom"",
//      ""AutoInitialize"": true,
//      ""Serializer"": ""hyperion""
//    }
//  }
//}";

//        [Fact(DisplayName = "MongoDbJournalOptions should be bindable to IConfiguration")]
//        public void JournalOptionsIConfigurationBindingTest()
//        {
//            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Json));
//            var jsonConfig = new ConfigurationBuilder().AddJsonStream(stream).Build();

//            var options = jsonConfig.GetSection("Akka:JournalOptions").Get<MongoDbJournalOptions>();
//            options.IsDefaultPlugin.Should().BeFalse();
//            options.Identifier.Should().Be("custom");
//            options.AutoInitialize.Should().BeTrue();
//            options.Serializer.Should().Be("hyperion");
//            options.ConnectionString.Should().Be("Server=localhost,1533;Database=Akka;User Id=sa;");
//            options.ConnectionTimeout.Should().Be(55.Seconds());
//            options.SchemaName.Should().Be("schema");
//            options.TableName.Should().Be("journal");
//            options.MetadataTableName.Should().Be("meta");
//            options.SequentialAccess.Should().BeFalse();

//            options.StoredAs.Should().Be(StoredAsType.JsonB);
//            options.UseBigIntIdentityForOrderingColumn.Should().BeTrue();
//        }


        private static void AssertJournalConfig(Config underTest, Config reference)
        {
            underTest.GetString("class").Should().Be(reference.GetString("class"));
            underTest.GetString("connection-string").Should().Be(reference.GetString("connection-string"));
            underTest.GetBoolean("use-write-transaction").Should().Be(reference.GetBoolean("use-write-transaction"));
            underTest.GetBoolean("auto-initialize").Should().Be(reference.GetBoolean("auto-initialize"));
            underTest.GetString("plugin-dispatcher").Should().Be(reference.GetString("plugin-dispatcher"));
            underTest.GetString("collection").Should().Be(reference.GetString("collection"));
            underTest.GetString("metadata-collection").Should().Be(reference.GetString("metadata-collection"));
            underTest.GetBoolean("legacy-serialization").Should().Be(reference.GetBoolean("legacy-serialization"));
            underTest.GetTimeSpan("call-timeout").Should().Be(reference.GetTimeSpan("call-timeout"));
        }
    }
}