//-----------------------------------------------------------------------
// <copyright file="MongoDbPersistenceProvider.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Actor;

namespace Akka.Persistence.MongoDb
{
    /// <summary>
    /// Extension Id provider for the MongoDb Persistence extension.
    /// </summary>
    public class MongoDbPersistenceProvider : ExtensionIdProvider<MongoDbPersistence>
    {
        /// <summary>
        /// Creates an actor system extension for akka persistence MongoDb support.
        /// </summary>
        /// <param name="system"></param>
        /// <returns></returns>
        public override MongoDbPersistence CreateExtension(ExtendedActorSystem system)
        {
            return new MongoDbPersistence(system);
        }
    }
}
