//-----------------------------------------------------------------------
// <copyright file="DatabaseFixture.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Util.Internal;
using Mongo2Go;
using MongoDB.Driver.Core.Misc;
using System;

namespace Akka.Persistence.MongoDb.Tests
{
    public class DatabaseFixture : IDisposable
    {
        private MongoDbRunner _runner;
        
        public string ConnectionString { get; private set; }

        public DatabaseFixture()
        {
            _runner = MongoDbRunner.Start(singleNodeReplSet: true);
            //_runner = MongoDbRunner.Start();
            ConnectionString = ConString(_runner.ConnectionString);// + "akkanet";
        }
        private string ConString(string cString)
        {
            var s = cString.Split('?');
            var connectionString = s[0] + $"akkanet?" + s[1];
            return connectionString;
        }
        public void Dispose()
        {
            _runner.Dispose();
        }
    }
}
