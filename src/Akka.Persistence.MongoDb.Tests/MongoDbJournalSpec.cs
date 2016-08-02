//-----------------------------------------------------------------------
// <copyright file="MongoDbJournalSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Configuration;
using Akka.Persistence.TestKit.Journal;
using Mongo2Go;
using MongoDB.Driver;
using Xunit;

namespace Akka.Persistence.MongoDb.Tests
{
    [Collection("MongoDbSpec")]
    public class MongoDbJournalSpec : JournalSpec
    {
        private static readonly MongoDbRunner Runner = MongoDbRunner.Start(ConfigurationManager.AppSettings[0]);

        protected override bool SupportsRejectingNonSerializableObjects { get; } = false;

        private static readonly string SpecConfig = @"
            akka.test.single-expect-default = 3s
            akka.persistence {
                publish-plugin-commands = on
                journal {
                    plugin = ""akka.persistence.journal.mongodb""
                    mongodb {
                        class = ""Akka.Persistence.MongoDb.Journal.MongoDbJournal, Akka.Persistence.MongoDb""
                        connection-string = ""<ConnectionString>""
                        auto-initialize = on
                        collection = ""EventJournal""
                    }
                }
            }";

        public MongoDbJournalSpec() : base(CreateSpecConfig(), "MongoDbJournalSpec")
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
                .DropCollectionAsync("EventJournal").Wait();
            
            base.Dispose(disposing);
        }
    }
}
