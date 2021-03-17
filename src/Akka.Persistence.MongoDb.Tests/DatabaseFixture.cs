//-----------------------------------------------------------------------
// <copyright file="DatabaseFixture.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Mongo2Go;
using System;
using System.Threading.Tasks;
using MongoDB.Driver;
using Xunit;

namespace Akka.Persistence.MongoDb.Tests
{
    [CollectionDefinition("MongoDbSpec")]
    public sealed class MongoDbFixture : ICollectionFixture<DatabaseFixture>
    {
    }

    public class DatabaseFixture : IAsyncLifetime
    {
        private MongoDbRunner _runner;

        public string ConnectionString { get; private set; }

        public DatabaseFixture()
        {
            
        }

        public void Dispose()
        {
           
        }

        public Task InitializeAsync()
        {
            _runner = MongoDbRunner.Start(singleNodeReplSet: true);
            var strParts = _runner.ConnectionString.Split("?");
            ConnectionString = $"{strParts[0]}akkadotnet/?{strParts[1]}";
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            _runner.Dispose();
            return Task.CompletedTask;
        }
    }
}
