using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Configuration;
using Akka.Persistence.TCK;
using Akka.Persistence.TCK.Serialization;
using Akka.TestKit;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.MongoDb.Tests
{
    public class MongoDbJournalSetupSpec : PluginSpec, IClassFixture<DatabaseFixture>
    {
        private const bool SupportsAtomicPersistAllOfSeveralEvents = true;

        public new PersistenceExtension Extension { get; }
        protected IActorRef Journal => Extension.JournalFor(null);

        private TestProbe _senderProbe;
        private TestProbe _receiverProbe;

        // TEST: MongoDb journal plugin set using Setup should behave exactly like when it is
        // set up using connection string.
        public MongoDbJournalSetupSpec(
            DatabaseFixture databaseFixture, 
            ITestOutputHelper output)
            : base (null, null, output)
        {
            InitializeTest(
                null, 
                CreateBootstrapSetup(databaseFixture), 
                nameof(MongoDbSnapshotStoreSetupSpec), 
                null);

            InitializeLogger(Sys);
            Extension = Persistence.Instance.Apply(Sys as ExtendedActorSystem);

            Initialize();
        }

        private static ActorSystemSetup CreateBootstrapSetup(DatabaseFixture fixture)
        {
            var connectionString = new MongoUrl(fixture.ConnectionString);
            var client = new MongoClient(connectionString);
            var databaseName = connectionString.DatabaseName;
            var settings = client.Settings;

            return BootstrapSetup.Create()
                .WithConfig(CreateSpecConfig())
                .And(new MongoDbPersistenceSetup(null, null, databaseName, settings));
        }

        private static Config CreateSpecConfig()
        {
            var specString = @"
                akka.test.single-expect-default = 3s
                akka.persistence {
                    publish-plugin-commands = on
                    journal {
                        plugin = ""akka.persistence.journal.mongodb""
                        mongodb {
                            class = ""Akka.Persistence.MongoDb.Journal.MongoDbJournal, Akka.Persistence.MongoDb""
                            auto-initialize = on
                            collection = ""EventJournal""
                        }
                    }
                }";

            return ConfigurationFactory.ParseString(specString);
        }

        private void Initialize()
        {
            _senderProbe = CreateTestProbe();
            _receiverProbe = CreateTestProbe();
            WriteMessages(1, 5, Pid, _senderProbe.Ref, WriterGuid);
        }

        private bool IsReplayedMessage(ReplayedMessage message, long seqNr, bool isDeleted = false)
        {
            var p = message.Persistent;
            return p.IsDeleted == isDeleted
                   && p.Payload.ToString() == "a-" + seqNr
                   && p.PersistenceId == Pid
                   && p.SequenceNr == seqNr;
        }

        private AtomicWrite[] WriteMessages(int from, int to, string pid, IActorRef sender, string writerGuid)
        {
            Func<long, Persistent> persistent = i => new Persistent("a-" + i, i, pid, string.Empty, false, sender, writerGuid);
            var messages = Enumerable.Range(@from, to - 1)
                .Select(
                    i =>
                        i == to - 1
                            ? new AtomicWrite(
                                new[] {persistent(i), persistent(i + 1)}.ToImmutableList<IPersistentRepresentation>())
                            : new AtomicWrite(persistent(i)))
                .ToArray();
            var probe = CreateTestProbe();

            Journal.Tell(new WriteMessages(messages, probe.Ref, ActorInstanceId));

            probe.ExpectMsg<WriteMessagesSuccessful>();
            for (var i = from; i <= to; i++)
            {
                var n = i;
                probe.ExpectMsg<WriteMessageSuccess>(m =>
                    m.Persistent.Payload.ToString() == ("a-" + n) && m.Persistent.SequenceNr == (long)n &&
                    m.Persistent.PersistenceId == Pid);
            }

            return messages;
        }

        [Fact]
        public void Journal_should_replay_all_messages()
        {
            Journal.Tell(new ReplayMessages(1, long.MaxValue, long.MaxValue, Pid, _receiverProbe.Ref));
            for (var i = 1; i <= 5; i++) _receiverProbe.ExpectMsg<ReplayedMessage>(m => IsReplayedMessage(m, i));
            _receiverProbe.ExpectMsg<RecoverySuccess>(m => m.HighestSequenceNr == 5L);
        }

        [Fact]
        public void Journal_should_replay_messages_using_a_lower_sequence_number_bound()
        {
            Journal.Tell(new ReplayMessages(3, long.MaxValue, long.MaxValue, Pid, _receiverProbe.Ref));
            for (var i = 3; i <= 5; i++) _receiverProbe.ExpectMsg<ReplayedMessage>(m => IsReplayedMessage(m, i));
            _receiverProbe.ExpectMsg<RecoverySuccess>(m => m.HighestSequenceNr == 5L);
        }

        [Fact]
        public void Journal_should_replay_messages_using_an_upper_sequence_number_bound()
        {
            Journal.Tell(new ReplayMessages(1, 3, long.MaxValue, Pid, _receiverProbe.Ref));
            for (var i = 1; i <= 3; i++) _receiverProbe.ExpectMsg<ReplayedMessage>(m => IsReplayedMessage(m, i));
            _receiverProbe.ExpectMsg<RecoverySuccess>(m => m.HighestSequenceNr == 5L);
        }

        [Fact]
        public void Journal_should_replay_messages_using_a_count_limit()
        {
            Journal.Tell(new ReplayMessages(1, long.MaxValue, 3, Pid, _receiverProbe.Ref));
            for (var i = 1; i <= 3; i++) _receiverProbe.ExpectMsg<ReplayedMessage>(m => IsReplayedMessage(m, i));
            _receiverProbe.ExpectMsg<RecoverySuccess>(m => m.HighestSequenceNr == 5L);
        }

        [Fact]
        public void Journal_should_replay_messages_using_lower_and_upper_sequence_number_bound()
        {
            Journal.Tell(new ReplayMessages(2, 3, long.MaxValue, Pid, _receiverProbe.Ref));
            for (var i = 2; i <= 3; i++) _receiverProbe.ExpectMsg<ReplayedMessage>(m => IsReplayedMessage(m, i));
            _receiverProbe.ExpectMsg<RecoverySuccess>(m => m.HighestSequenceNr == 5L);
        }

        [Fact]
        public void Journal_should_replay_messages_using_lower_and_upper_sequence_number_bound_and_count_limit()
        {
            Journal.Tell(new ReplayMessages(2, 5, 2, Pid, _receiverProbe.Ref));
            for (var i = 2; i <= 3; i++) _receiverProbe.ExpectMsg<ReplayedMessage>(m => IsReplayedMessage(m, i));
            _receiverProbe.ExpectMsg<RecoverySuccess>(m => m.HighestSequenceNr == 5L);
        }

        [Fact]
        public void Journal_should_replay_a_single_if_lower_sequence_number_bound_equals_upper_sequence_number_bound()
        {
            Journal.Tell(new ReplayMessages(2, 2, long.MaxValue, Pid, _receiverProbe.Ref));
            _receiverProbe.ExpectMsg<ReplayedMessage>(m => IsReplayedMessage(m, 2));
            _receiverProbe.ExpectMsg<RecoverySuccess>(m => m.HighestSequenceNr == 5L);
        }

        [Fact]
        public void Journal_should_replay_a_single_message_if_count_limit_is_equal_one()
        {
            Journal.Tell(new ReplayMessages(2, 4, 1, Pid, _receiverProbe.Ref));
            _receiverProbe.ExpectMsg<ReplayedMessage>(m => IsReplayedMessage(m, 2));
            _receiverProbe.ExpectMsg<RecoverySuccess>(m => m.HighestSequenceNr == 5L);
        }

        [Fact]
        public void Journal_should_not_replay_messages_if_count_limit_equals_zero()
        {
            Journal.Tell(new ReplayMessages(2, 4, 0, Pid, _receiverProbe.Ref));
            _receiverProbe.ExpectMsg<RecoverySuccess>(m => m.HighestSequenceNr == 5L);
        }

        [Fact]
        public void Journal_should_not_replay_messages_if_lower_sequence_number_bound_is_greater_than_upper_sequence_number_bound()
        {
            Journal.Tell(new ReplayMessages(3, 2, long.MaxValue, Pid, _receiverProbe.Ref));
            _receiverProbe.ExpectMsg<RecoverySuccess>(m => m.HighestSequenceNr == 5L);
        }

        [Fact]
        public void Journal_should_not_replay_messages_if_the_persistent_actor_has_not_yet_written_messages()
        {
            Journal.Tell(new ReplayMessages(0, long.MaxValue, long.MaxValue, "non-existing-pid", _receiverProbe.Ref));
            _receiverProbe.ExpectMsg<RecoverySuccess>(m => m.HighestSequenceNr == 0L);
        }

        [Fact]
        public void Journal_should_not_replay_permanently_deleted_messages_on_range_deletion()
        {
            var receiverProbe2 = CreateTestProbe();
            var command = new DeleteMessagesTo(Pid, 3, receiverProbe2.Ref);
            var subscriber = CreateTestProbe();

            Subscribe<DeleteMessagesTo>(subscriber.Ref);
            Journal.Tell(command);
            subscriber.ExpectMsg<DeleteMessagesTo>(cmd => cmd.PersistenceId == Pid && cmd.ToSequenceNr == 3);
            receiverProbe2.ExpectMsg<DeleteMessagesSuccess>(m => m.ToSequenceNr == command.ToSequenceNr);

            Journal.Tell(new ReplayMessages(1, long.MaxValue, long.MaxValue, Pid, _receiverProbe.Ref));
            _receiverProbe.ExpectMsg<ReplayedMessage>(m => IsReplayedMessage(m, 4));
            _receiverProbe.ExpectMsg<ReplayedMessage>(m => IsReplayedMessage(m, 5));

            receiverProbe2.ExpectNoMsg(TimeSpan.FromMilliseconds(200));
        }

        [Fact]
        public void Journal_should_not_reset_HighestSequenceNr_after_message_deletion()
        {
            Journal.Tell(new ReplayMessages(0, long.MaxValue, long.MaxValue, Pid, _receiverProbe.Ref));
            for (var i = 1; i <= 5; i++) _receiverProbe.ExpectMsg<ReplayedMessage>(m => IsReplayedMessage(m, i));
            _receiverProbe.ExpectMsg<RecoverySuccess>(m => m.HighestSequenceNr == 5L);

            Journal.Tell(new DeleteMessagesTo(Pid, 3L, _receiverProbe.Ref));
            _receiverProbe.ExpectMsg<DeleteMessagesSuccess>(m => m.ToSequenceNr == 3L);

            Journal.Tell(new ReplayMessages(0, long.MaxValue, long.MaxValue, Pid, _receiverProbe.Ref));
            for (var i = 4; i <= 5; i++) _receiverProbe.ExpectMsg<ReplayedMessage>(m => IsReplayedMessage(m, i));
            _receiverProbe.ExpectMsg<RecoverySuccess>(m => m.HighestSequenceNr == 5L);
        }

        [Fact]
        public void Journal_should_not_reset_HighestSequenceNr_after_journal_cleanup()
        {
            Journal.Tell(new ReplayMessages(0, long.MaxValue, long.MaxValue, Pid, _receiverProbe.Ref));
            for (var i = 1; i <= 5; i++) _receiverProbe.ExpectMsg<ReplayedMessage>(m => IsReplayedMessage(m, i));
            _receiverProbe.ExpectMsg<RecoverySuccess>(m => m.HighestSequenceNr == 5L);

            Journal.Tell(new DeleteMessagesTo(Pid, long.MaxValue, _receiverProbe.Ref));
            _receiverProbe.ExpectMsg<DeleteMessagesSuccess>(m => m.ToSequenceNr == long.MaxValue);

            Journal.Tell(new ReplayMessages(0, long.MaxValue, long.MaxValue, Pid, _receiverProbe.Ref));
            _receiverProbe.ExpectMsg<RecoverySuccess>(m => m.HighestSequenceNr == 5L);
        }

        [Fact]
        public void Journal_should_serialize_events()
        {
            if (!SupportsSerialization) return;

            var probe = CreateTestProbe();
            var @event = new TestPayload(probe.Ref);

            var aw = new AtomicWrite(
                new Persistent(@event, 6L, Pid, sender: ActorRefs.NoSender, writerGuid: WriterGuid));

            Journal.Tell(new WriteMessages(new []{ aw }, probe.Ref, ActorInstanceId));

            probe.ExpectMsg<WriteMessagesSuccessful>();
            var pid = Pid;
            var writerGuid = WriterGuid;
            probe.ExpectMsg<WriteMessageSuccess>(o =>
            {
                Assertions.AssertEqual(writerGuid, o.Persistent.WriterGuid);
                Assertions.AssertEqual(pid, o.Persistent.PersistenceId);
                Assertions.AssertEqual(6L, o.Persistent.SequenceNr);
                Assertions.AssertTrue(o.Persistent.Sender == ActorRefs.NoSender || o.Persistent.Sender.Equals(Sys.DeadLetters), $"Expected WriteMessagesSuccess.Persistent.Sender to be null or {Sys.DeadLetters}, but found {o.Persistent.Sender}");
                Assertions.AssertEqual(@event, o.Persistent.Payload);
            });

            Journal.Tell(new ReplayMessages(6L, long.MaxValue, long.MaxValue, Pid, _receiverProbe.Ref));

            _receiverProbe.ExpectMsg<ReplayedMessage>(o =>
            {
                Assertions.AssertEqual(writerGuid, o.Persistent.WriterGuid);
                Assertions.AssertEqual(pid, o.Persistent.PersistenceId);
                Assertions.AssertEqual(6L, o.Persistent.SequenceNr);
                Assertions.AssertTrue(o.Persistent.Sender == ActorRefs.NoSender || o.Persistent.Sender.Equals(Sys.DeadLetters), $"Expected WriteMessagesSuccess.Persistent.Sender to be null or {Sys.DeadLetters}, but found {o.Persistent.Sender}");
                Assertions.AssertEqual(@event, o.Persistent.Payload);
            });

            Assertions.AssertEqual(_receiverProbe.ExpectMsg<RecoverySuccess>().HighestSequenceNr, 6L);
        }

    }
}
