using System;
using Akka.Configuration;

namespace Akka.Persistence.MongoDb
{
    /// <summary>
    /// Settings for the MongoDB persistence implementation, parsed from HOCON configuration.
    /// </summary>
    public abstract class MongoDbSettings
    {
        /// <summary>
        /// Connection string used to access the MongoDB, also specifies the database.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Name of the collection for the event journal or snapshots
        /// </summary>
        public string Collection { get; private set; }

        protected MongoDbSettings(Config config)
        {
            ConnectionString = config.GetString("connection-string");
            Collection = config.GetString("collection");
        }
    }


    /// <summary>
    /// Settings for the MongoDB journal implementation, parsed from HOCON configuration.
    /// </summary>
    public class MongoDbJournalSettings : MongoDbSettings
    {
        public MongoDbJournalSettings(Config config) : base(config)
        {
            if (config == null)
                throw new ArgumentNullException("config",
                    "MongoDB journal settings cannot be initialized, because required HOCON section couldn't been found");
        }
    }


    /// <summary>
    /// Settings for the MongoDB snapshot implementation, parsed from HOCON configuration.
    /// </summary>
    public class MongoDbSnapshotSettings : MongoDbSettings
    {
        public MongoDbSnapshotSettings(Config config) : base(config)
        {
            if (config == null)
                throw new ArgumentNullException("config",
                    "MongoDB snapshot settings cannot be initialized, because required HOCON section couldn't been found");
        }
    }
}
