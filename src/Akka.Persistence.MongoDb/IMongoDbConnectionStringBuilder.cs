using Akka.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Akka.Persistence.MongoDb
{
    public interface IMongoDbJournalConnectionStringBuilder
    {
        /// <summary>
        /// Initialise the builder with the MongoDB config section
        /// </summary>
        /// <param name="config"></param>
        void Init(MongoDbJournalSettings config);

        /// <summary>
        /// Return a MongoDb connection string for a given persistence id
        /// </summary>
        /// <param name="persistenceId"></param>
        /// <returns>Connection string</returns>
        string GetConnectionString(string persistenceId);
    }

    public interface IMongoDbSnapshotConnectionStringBuilder
    {
        /// <summary>
        /// Initialise the builder with the MongoDB config section
        /// </summary>
        /// <param name="config"></param>
        void Init(MongoDbSnapshotSettings config);

        /// <summary>
        /// Return a MongoDb connection string for a given persistence id
        /// </summary>
        /// <param name="persistenceId"></param>
        /// <returns>Connection string</returns>
        string GetConnectionString(string persistenceId);
    }
}
