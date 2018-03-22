//-----------------------------------------------------------------------
// <copyright file="MongoDbPersistence.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Akka.Actor;
using Akka.Configuration;
using MongoDB.Bson.Serialization;

namespace Akka.Persistence.MongoDb
{
    /// <summary>
    /// An actor system extension initializing support for MongoDb persistence layer.
    /// </summary>
    public class MongoDbPersistence : IExtension
    {
        static MongoDbPersistence()
        {
            // Some MongoDB things are statically configured.

            // Register our own serializer for objects that uses the type's FullName + Assembly for the discriminator
            BsonSerializer.RegisterSerializer(typeof(object), new FullTypeNameObjectSerializer());
        }
        /// <summary>
        /// Returns a default configuration for akka persistence MongoDb journal and snapshot store.
        /// </summary>
        /// <returns></returns>
        public static Config DefaultConfiguration()
        {
            return ConfigurationFactory.FromResource<MongoDbPersistence>("Akka.Persistence.MongoDb.reference.conf");
        }

        public static MongoDbPersistence Get(ActorSystem system)
        {
            return system.WithExtension<MongoDbPersistence, MongoDbPersistenceProvider>();
        }

        /// <summary>
        /// The settings for the MongoDb journal.
        /// </summary>
        public MongoDbJournalSettings JournalSettings { get; }

        /// <summary>
        /// The settings for the MongoDb snapshot store.
        /// </summary>
        public MongoDbSnapshotSettings SnapshotStoreSettings { get; }

        public MongoDbPersistence(ExtendedActorSystem system)
        {
            if (system == null)
                throw new ArgumentNullException(nameof(system));

            // Initialize fallback configuration defaults
            system.Settings.InjectTopLevelFallback(DefaultConfiguration());

            // Read config
            var journalConfig = system.Settings.Config.GetConfig("akka.persistence.journal.mongodb");
            JournalSettings = new MongoDbJournalSettings(journalConfig);

            var snapshotConfig = system.Settings.Config.GetConfig("akka.persistence.snapshot-store.mongodb");
            SnapshotStoreSettings = new MongoDbSnapshotSettings(snapshotConfig);
        }
    }
}
