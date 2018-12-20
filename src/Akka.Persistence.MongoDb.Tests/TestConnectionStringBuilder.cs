using System;
using System.Collections.Generic;
using System.Text;

namespace Akka.Persistence.MongoDb.Tests
{
    public class TestConnectionStringBuilder : IMongoDbJournalConnectionStringBuilder, IMongoDbSnapshotConnectionStringBuilder
    {
        private string _connectionString;

        public string GetConnectionString(string persistenceId)
        {
            return _connectionString;
        }

        public void Init(MongoDbJournalSettings config)
        {
            _connectionString = config.ConnectionString;
        }

        public void Init(MongoDbSnapshotSettings config)
        {
            _connectionString = config.ConnectionString;
        }
    }
}
