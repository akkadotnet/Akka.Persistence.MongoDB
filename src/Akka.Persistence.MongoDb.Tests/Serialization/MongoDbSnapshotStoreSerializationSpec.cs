using Akka.Configuration;
using Akka.Persistence.TCK.Serialization;
using Akka.Util.Internal;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.MongoDb.Tests.Serialization
{
    [Collection("MongoDbSpec")]
    public class MongoDbSnapshotStoreSerializationSpec : SnapshotStoreSerializationSpec, IClassFixture<DatabaseFixture>
    {
        public static readonly AtomicCounter Counter = new AtomicCounter(0);
        private readonly ITestOutputHelper _output;

        public MongoDbSnapshotStoreSerializationSpec(ITestOutputHelper output, DatabaseFixture databaseFixture)
            : base(CreateSpecConfig(databaseFixture, Counter.GetAndIncrement()), nameof(MongoDbSnapshotStoreSerializationSpec), output)
        {
            _output = output;
            output.WriteLine(ConnectionString(databaseFixture, Counter.Current));
        }

        private static Config CreateSpecConfig(DatabaseFixture databaseFixture, int id)
        {
            var specString = @"
                akka.test.single-expect-default = 3s
                akka.persistence {
                    publish-plugin-commands = on
                    snapshot-store {
                        plugin = ""akka.persistence.snapshot-store.mongodb""
                        mongodb {
                            class = ""Akka.Persistence.MongoDb.Snapshot.MongoDbSnapshotStore, Akka.Persistence.MongoDb""
                            connection-string = """ + ConnectionString(databaseFixture, id) + @"""
                            auto-initialize = on
                            collection = ""SnapshotStore""
                        }
                    }
                }";

            return ConfigurationFactory.ParseString(specString);
        }
        private static string ConnectionString(DatabaseFixture databaseFixture, int id)
        {
            var s = databaseFixture.ConnectionString.Split('?');
            var connectionString = s[0] + $"{id}?" + s[1];
            return connectionString;
        }
    }
}
