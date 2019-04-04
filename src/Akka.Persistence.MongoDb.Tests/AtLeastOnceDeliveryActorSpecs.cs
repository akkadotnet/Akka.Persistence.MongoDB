using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Akka.Actor;
using Akka.Actor.Dsl;
using Akka.Configuration;
using Akka.Event;
using Akka.Util.Internal;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.MongoDb.Tests
{

    public class AtLeastOnceDeliveryActorSpecs : Akka.TestKit.Xunit2.TestKit, IClassFixture<DatabaseFixture>
    {
        public static readonly AtomicCounter Counter = new AtomicCounter(0);
        private readonly ITestOutputHelper _output;

        public AtLeastOnceDeliveryActorSpecs(ITestOutputHelper output, DatabaseFixture databaseFixture)
            : base(CreateSpecConfig(databaseFixture, Counter.GetAndIncrement()), "AtLeastOnceDeliveryActorSpecs", output)
        {
            _output = output;
            output.WriteLine(databaseFixture.ConnectionString + Counter.Current);
        }

        [Fact]
        public void MongoDb_should_persist_AtLeastOnceDelivery_snapshots_correctly()
        {
            var aldProps = Props.Create(() => new AldActor("fuber", TestActor));
            var ald1 = Sys.ActorOf(aldProps, "ald1");
            Watch(ald1);
            ald1.Tell("hi");
            ald1.Tell("there");

            // make sure initial payload got sent through
            ReceiveN(2).Cast<Delivery>().All(x => x.Msg is string).Should().BeTrue();
            Sys.Stop(ald1);
            ExpectTerminated(ald1);

            var ald2 = Sys.ActorOf(aldProps, "ald2");
            ExpectMsg("recovered");
            var msgs = ReceiveN(2).Cast<Delivery>();
            foreach (var msg in msgs)
            {
                ald2.Tell(msg.ConfirmationId);
            }
            ExpectNoMsg(1000);
        }

        class Delivery
        {
            public Delivery(object msg, long confirmationId)
            {
                Msg = msg;
                ConfirmationId = confirmationId;
            }

            public object Msg { get; }

            public long ConfirmationId { get; }
        }

        class AldActor : AtLeastOnceDeliveryActor
        {
            private IActorRef _targetActor;
            private readonly ILoggingAdapter _log = Context.GetLogger();

            public AldActor(string persistenceId, IActorRef targetActor)
            {
                PersistenceId = persistenceId;
                _targetActor = targetActor;
            }

            protected override bool ReceiveRecover(object message)
            {
                switch (message)
                {
                    case SnapshotOffer offer when offer.Snapshot is AtLeastOnceDeliverySnapshot snapshot:
                        _targetActor.Tell("recovered");
                        foreach (var s in snapshot.UnconfirmedDeliveries)
                        {
                            _log.Info("Have delivery scheduled to {0} with Id {1}", s.Destination, s.DeliveryId);
                        }
                        SetDeliverySnapshot(snapshot);
                        return true;
                }

                return false;
            }

            protected override bool ReceiveCommand(object message)
            {
                switch (message)
                {
                    case string str:
                        Deliver(_targetActor.Path, l => new Delivery(str, l));
                        SaveSnapshot(GetDeliverySnapshot());
                        return true;
                    case long l:
                        ConfirmDelivery(l);
                        SaveSnapshot(GetDeliverySnapshot());
                        return true;
                    case SaveSnapshotSuccess success:
                        DeleteSnapshots(new SnapshotSelectionCriteria(success.Metadata.SequenceNr, success.Metadata.Timestamp.AddMilliseconds(-1)));
                        return true;
                }

                return false;
            }

            public override string PersistenceId { get; }
        }

        private static Config CreateSpecConfig(DatabaseFixture databaseFixture, int id)
        {
            var specString = @"
                akka.persistence {
                    at-least-once-delivery.redeliver-interval = 1s
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
                    snapshot-store {
                        plugin = ""akka.persistence.snapshot-store.mongodb""
                        mongodb {
                            class = ""Akka.Persistence.MongoDb.Snapshot.MongoDbSnapshotStore, Akka.Persistence.MongoDb""
                            connection-string = """ + databaseFixture.ConnectionString + @"""
                            auto-initialize = on
                            collection = ""SnapshotStore""
                        }
                    }
                }";

            return ConfigurationFactory.ParseString(specString);
        }
    }
}
