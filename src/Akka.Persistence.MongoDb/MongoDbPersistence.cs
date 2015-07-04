using Akka.Actor;
using Akka.Configuration;

namespace Akka.Persistence.MongoDb
{
    /// <summary>
    /// Extension Id provider for the MongoDB Persistence extension.
    /// </summary>
    public class MongoDbPersistence : ExtensionIdProvider<MongoDbExtension>
    {
        public static readonly MongoDbPersistence Instance = new MongoDbPersistence();

        private MongoDbPersistence() { }
        
        public static Config DefaultConfig()
        {
            return ConfigurationFactory.FromResource<MongoDbPersistence>("Akka.Persistence.MongoDb.reference.conf");
        }

        public override MongoDbExtension CreateExtension(ExtendedActorSystem system)
        {
            return new MongoDbExtension(system);
        }
    }
}
