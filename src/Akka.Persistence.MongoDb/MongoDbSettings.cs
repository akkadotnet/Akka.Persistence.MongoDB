//-----------------------------------------------------------------------
// <copyright file="MongoDbSettings.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

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
        /// Connection string used to access the MongoDb, also specifies the database.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Flag determining in in case of event journal or metadata table missing, they should be automatically initialized.
        /// </summary>
        public bool AutoInitialize { get; private set; }

        /// <summary>
        /// Name of the collection for the event journal or snapshots
        /// </summary>
        public string Collection { get; private set; }

        protected MongoDbSettings(Config config)
        {
            ConnectionString = config.GetString("connection-string");
            Collection = config.GetString("collection");
            AutoInitialize = config.GetBoolean("auto-initialize");
        }
    }


    /// <summary>
    /// Settings for the MongoDB journal implementation, parsed from HOCON configuration.
    /// </summary>
    public class MongoDbJournalSettings : MongoDbSettings
    {
        public string MetadataCollection { get; private set; }

        public MongoDbJournalSettings(Config config) : base(config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config),
                    "MongoDb journal settings cannot be initialized, because required HOCON section couldn't been found");

            MetadataCollection = config.GetString("metadata-collection");
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
                throw new ArgumentNullException(nameof(config),
                    "MongoDb snapshot settings cannot be initialized, because required HOCON section couldn't been found");
        }
    }
}
