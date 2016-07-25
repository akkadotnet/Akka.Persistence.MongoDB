//-----------------------------------------------------------------------
// <copyright file="MongoDbSnapshotStoreSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Configuration;
using Akka.Persistence.TestKit.Snapshot;
using Mongo2Go;
using MongoDB.Driver;
using Xunit;

namespace Akka.Persistence.MongoDb.Tests
{
    [Collection("MongoDbSpec")]
    public class MongoDbSnapshotStoreSpec : SnapshotStoreSpec
    {
        private static readonly MongoDbRunner Runner = MongoDbRunner.Start(ConfigurationManager.AppSettings[0]);

        private static readonly string SpecConfig = @"
            akka.test.single-expect-default = 3s
            akka.persistence {
                publish-plugin-commands = on
                snapshot-store {
                    plugin = ""akka.persistence.snapshot-store.mongodb""
                    mongodb {
                        class = ""Akka.Persistence.MongoDb.Snapshot.MongoDbSnapshotStore, Akka.Persistence.MongoDb""
                        connection-string = ""<ConnectionString>""
                        auto-initialize = on
                        collection = ""SnapshotStore""
                    }
                }
            }";

        public MongoDbSnapshotStoreSpec() : base(CreateSpecConfig(), "MongoDbSnapshotStoreSpec")
        {
            AppDomain.CurrentDomain.DomainUnload += (_, __) =>
            {
                try
                {
                    Runner.Dispose();
                }
                catch { }
            };


            Initialize();
        }

        private static string CreateSpecConfig()
        {
            return SpecConfig.Replace("<ConnectionString>", Runner.ConnectionString + "akkanet");
        }

        protected override void Dispose(bool disposing)
        {
            new MongoClient(Runner.ConnectionString)
                .GetDatabase("akkanet")
                .DropCollectionAsync("SnapshotStore").Wait();

            base.Dispose(disposing);
        }
    }
}
