//-----------------------------------------------------------------------
// <copyright file="MongoDbJournalSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Persistence.TCK.Journal;
using Xunit;
using Akka.Configuration;
using Akka.Persistence.MongoDb.Query;
using Akka.Persistence.Query;
using Xunit.Abstractions;
using Akka.Util.Internal;
using System;
using Akka.Actor;
using Akka.Streams.TestKit;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using Akka.Streams.Dsl;
using System.Collections.Generic;
using System.Threading;
using Akka.Streams;
using FluentAssertions;

namespace Akka.Persistence.MongoDb.Tests
{
    /// <summary>
    /// Copied from https://github.com/akkadotnet/akka.net/blob/4f984bceffec20bc92beaf6f138f5a8f153c794b/src/core/Akka.Persistence.TCK/Query/TestActor.cs#L15
    /// since it's marked as `internal` at the moment
    /// </summary>
    internal class QueryTestActor : UntypedPersistentActor, IWithUnboundedStash
    {
        public static Props Props(string persistenceId) => Actor.Props.Create(() => new QueryTestActor(persistenceId));

        public sealed class DeleteCommand
        {
            public DeleteCommand(long toSequenceNr)
            {
                ToSequenceNr = toSequenceNr;
            }

            public long ToSequenceNr { get; }
        }

        public QueryTestActor(string persistenceId)
        {
            PersistenceId = persistenceId;
        }

        public override string PersistenceId { get; }

        protected override void OnRecover(object message)
        {
        }

        protected override void OnCommand(object message)
        {
            switch (message)
            {
                case DeleteCommand delete:
                    DeleteMessages(delete.ToSequenceNr);
                    Become(WhileDeleting(Sender)); // need to wait for delete ACK to return
                    break;
                case string cmd:
                    var sender = Sender;
                    Persist(cmd, e => sender.Tell($"{e}-done"));
                    break;
            }
        }

        protected Receive WhileDeleting(IActorRef originalSender)
        {
            return message =>
            {
                switch (message)
                {
                    case DeleteMessagesSuccess success:
                        originalSender.Tell($"{success.ToSequenceNr}-deleted");
                        Become(OnCommand);
                        Stash.UnstashAll();
                        break;
                    case DeleteMessagesFailure failure:
                        originalSender.Tell($"{failure.ToSequenceNr}-deleted-failed");
                        Become(OnCommand);
                        Stash.UnstashAll();
                        break;
                    default:
                        Stash.Stash();
                        break;
                }

                return true;
            };
        }

        [Collection("MongoDbSpec")]
        public class MongoDbEventsByPersistenceIdSpec : Akka.Persistence.TCK.Query.EventsByPersistenceIdSpec, IClassFixture<DatabaseFixture>
        {
            public static readonly AtomicCounter Counter = new AtomicCounter(0);
            private readonly ITestOutputHelper _output;

            public MongoDbEventsByPersistenceIdSpec(ITestOutputHelper output, DatabaseFixture databaseFixture)
                : base(CreateSpecConfig(databaseFixture, Counter.GetAndIncrement()), "MongoDbEventsByPersistenceIdSpec", output)
            {
                _output = output;
                output.WriteLine(databaseFixture.ConnectionString + Counter.Current);
                ReadJournal = Sys.ReadJournalFor<MongoDbReadJournal>(MongoDbReadJournal.Identifier);
            }


            private static Config CreateSpecConfig(DatabaseFixture databaseFixture, int id)
            {
                var specString = @"
                akka.test.single-expect-default = 3s
                akka.persistence {
                    publish-plugin-commands = on
                    journal {
                        plugin = ""akka.persistence.journal.mongodb""
                        mongodb {
                            class = ""Akka.Persistence.MongoDb.Journal.MongoDbJournal, Akka.Persistence.MongoDb""
                            connection-string = """ + databaseFixture.ConnectionString + id + @"""
                            auto-initialize = on
                            collection = ""EventJournal""
                        }
                    }
                    query {
                        mongodb {
                            class = ""Akka.Persistence.MongoDb.Query.MongoDbReadJournalProvider, Akka.Persistence.MongoDb""
                            refresh-interval = 1s
                        }
                    }
                }";

                return ConfigurationFactory.ParseString(specString);
            }

        }
    }
}
