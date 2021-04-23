using System;
using System.Collections.Generic;
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
    public class MongoDbSnapshotStoreSetupSpec : PluginSpec, IClassFixture<DatabaseFixture>
    {
        public new PersistenceExtension Extension { get; }
        private IActorRef SnapshotStore => Extension.SnapshotStoreFor(null);
        private List<SnapshotMetadata> _metadata;
        private readonly TestProbe _senderProbe;
        private const int SnapshotByteSizeLimit = 10000;

        // TEST: MongoDb snapshot plugin set using Setup should behave exactly like when it is
        // set up using connection string.
        public MongoDbSnapshotStoreSetupSpec(
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
            _senderProbe = CreateTestProbe(Sys);

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
                .And(new MongoDbPersistenceSetup(databaseName, settings, null, null));
        }

        private static Config CreateSpecConfig()
        {
            var specString = @"
                akka.test.single-expect-default = 3s
                akka.persistence {
                    publish-plugin-commands = on
                    snapshot-store {
                        plugin = ""akka.persistence.snapshot-store.mongodb""
                        mongodb {
                            class = ""Akka.Persistence.MongoDb.Snapshot.MongoDbSnapshotStore, Akka.Persistence.MongoDb""
                            auto-initialize = on
                            collection = ""SnapshotStore""
                        }
                    }
                }";

            return ConfigurationFactory.ParseString(specString);
        }

        private void Initialize()
        {
            _metadata = WriteSnapshots().ToList();
        }

        private IEnumerable<SnapshotMetadata> WriteSnapshots()
        {
            for (int i = 1; i <= 5; i++)
            {
                var metadata = new SnapshotMetadata(Pid, i + 10);
                SnapshotStore.Tell(new SaveSnapshot(metadata, $"s-{i}"), _senderProbe.Ref);
                yield return _senderProbe.ExpectMsg<SaveSnapshotSuccess>().Metadata;
            }
        }

        [Fact]
        public void SnapshotStore_should_not_load_a_snapshot_given_an_invalid_persistence_id()
        {
            SnapshotStore.Tell(new LoadSnapshot("invalid", SnapshotSelectionCriteria.Latest, long.MaxValue), _senderProbe.Ref);
            _senderProbe.ExpectMsg<LoadSnapshotResult>(result => result.Snapshot == null && result.ToSequenceNr == long.MaxValue);
        }

        [Fact]
        public void SnapshotStore_should_not_load_a_snapshot_given_non_matching_timestamp_criteria()
        {
            SnapshotStore.Tell(new LoadSnapshot(Pid, new SnapshotSelectionCriteria(long.MaxValue, new DateTime(100000)), long.MaxValue), _senderProbe.Ref);
            _senderProbe.ExpectMsg<LoadSnapshotResult>(result => result.Snapshot == null && result.ToSequenceNr == long.MaxValue);
        }

        [Fact]
        public void SnapshotStore_should_not_load_a_snapshot_given_non_matching_sequence_number_criteria()
        {
            SnapshotStore.Tell(new LoadSnapshot(Pid, new SnapshotSelectionCriteria(7), long.MaxValue), _senderProbe.Ref);
            _senderProbe.ExpectMsg<LoadSnapshotResult>(result => result.Snapshot == null && result.ToSequenceNr == long.MaxValue);

            SnapshotStore.Tell(new LoadSnapshot(Pid, SnapshotSelectionCriteria.Latest, 7), _senderProbe.Ref);
            _senderProbe.ExpectMsg<LoadSnapshotResult>(result => result.Snapshot == null && result.ToSequenceNr == 7);
        }

        [Fact]
        public void SnapshotStore_should_load_the_most_recent_snapshot()
        {
            SnapshotStore.Tell(new LoadSnapshot(Pid, SnapshotSelectionCriteria.Latest, long.MaxValue), _senderProbe.Ref);
            _senderProbe.ExpectMsg<LoadSnapshotResult>(result => 
                result.ToSequenceNr == long.MaxValue 
                && result.Snapshot != null 
                && result.Snapshot.Metadata.Equals(_metadata[4])
                && result.Snapshot.Snapshot.ToString() == "s-5");
        }

        [Fact]
        public void SnapshotStore_should_load_the_most_recent_snapshot_matching_an_upper_sequence_number_bound()
        {
            SnapshotStore.Tell(new LoadSnapshot(Pid, new SnapshotSelectionCriteria(13), long.MaxValue), _senderProbe.Ref);
            _senderProbe.ExpectMsg<LoadSnapshotResult>(result =>
                result.ToSequenceNr == long.MaxValue
                && result.Snapshot != null
                && result.Snapshot.Metadata.Equals(_metadata[2])
                && result.Snapshot.Snapshot.ToString() == "s-3");

            SnapshotStore.Tell(new LoadSnapshot(Pid, SnapshotSelectionCriteria.Latest, 13), _senderProbe.Ref);
            _senderProbe.ExpectMsg<LoadSnapshotResult>(result =>
                result.ToSequenceNr == 13
                && result.Snapshot != null
                && result.Snapshot.Metadata.Equals(_metadata[2])
                && result.Snapshot.Snapshot.ToString() == "s-3");
        }

        [Fact]
        public void SnapshotStore_should_load_the_most_recent_snapshot_matching_an_upper_sequence_number_and_timestamp_bound()
        {
            SnapshotStore.Tell(new LoadSnapshot(Pid, new SnapshotSelectionCriteria(13, _metadata[2].Timestamp), long.MaxValue), _senderProbe.Ref);
            _senderProbe.ExpectMsg<LoadSnapshotResult>(result =>
                result.ToSequenceNr == long.MaxValue
                && result.Snapshot != null
                && result.Snapshot.Metadata.Equals(_metadata[2])
                && result.Snapshot.Snapshot.ToString() == "s-3");

            SnapshotStore.Tell(new LoadSnapshot(Pid, new SnapshotSelectionCriteria(long.MaxValue, _metadata[2].Timestamp), 13), _senderProbe.Ref);
            _senderProbe.ExpectMsg<LoadSnapshotResult>(result =>
                result.ToSequenceNr == 13
                && result.Snapshot != null
                && result.Snapshot.Metadata.Equals(_metadata[2])
                && result.Snapshot.Snapshot.ToString() == "s-3");
        }

        [Fact]
        public void SnapshotStore_should_delete_a_single_snapshot_identified_by_SequenceNr_in_snapshot_metadata()
        {
            var md = _metadata[2];
            md = new SnapshotMetadata(md.PersistenceId, md.SequenceNr); // don't care about timestamp for delete of a single snap
            var command = new DeleteSnapshot(md);
            var sub = CreateTestProbe();

            Subscribe<DeleteSnapshot>(sub.Ref);
            SnapshotStore.Tell(command, _senderProbe.Ref);
            sub.ExpectMsg(command);
            _senderProbe.ExpectMsg<DeleteSnapshotSuccess>();

            SnapshotStore.Tell(new LoadSnapshot(Pid, new SnapshotSelectionCriteria(md.SequenceNr), long.MaxValue), _senderProbe.Ref);
            _senderProbe.ExpectMsg<LoadSnapshotResult>(result =>
                result.ToSequenceNr == long.MaxValue
                && result.Snapshot != null
                && result.Snapshot.Metadata.Equals(_metadata[1])
                && result.Snapshot.Snapshot.ToString() == "s-2");
        }

        [Fact]
        public void SnapshotStore_should_delete_all_snapshots_matching_upper_sequence_number_and_timestamp_bounds()
        {
            var md = _metadata[2];
            var command = new DeleteSnapshots(Pid, new SnapshotSelectionCriteria(md.SequenceNr, md.Timestamp));
            var sub = CreateTestProbe();

            Subscribe<DeleteSnapshots>(sub.Ref);
            SnapshotStore.Tell(command, _senderProbe.Ref);
            sub.ExpectMsg(command);
            _senderProbe.ExpectMsg<DeleteSnapshotsSuccess>();

            SnapshotStore.Tell(new LoadSnapshot(Pid, new SnapshotSelectionCriteria(md.SequenceNr, md.Timestamp), long.MaxValue), _senderProbe.Ref);
            _senderProbe.ExpectMsg<LoadSnapshotResult>(result => result.Snapshot == null && result.ToSequenceNr == long.MaxValue);


            SnapshotStore.Tell(new LoadSnapshot(Pid, new SnapshotSelectionCriteria(_metadata[3].SequenceNr, _metadata[3].Timestamp), long.MaxValue), _senderProbe.Ref);
            _senderProbe.ExpectMsg<LoadSnapshotResult>(result =>
                result.ToSequenceNr == long.MaxValue
                && result.Snapshot != null
                && result.Snapshot.Metadata.Equals(_metadata[3])
                && result.Snapshot.Snapshot.ToString() == "s-4");
        }

        [Fact]
        public void SnapshotStore_should_not_delete_snapshots_with_non_matching_upper_timestamp_bounds()
        {
            var md = _metadata[3];
            var criteria = new SnapshotSelectionCriteria(md.SequenceNr, md.Timestamp.Subtract(TimeSpan.FromTicks(1)));
            var command = new DeleteSnapshots(Pid, criteria);
            var sub = CreateTestProbe();

            Subscribe<DeleteSnapshots>(sub.Ref);
            SnapshotStore.Tell(command, _senderProbe.Ref);
            sub.ExpectMsg(command);
            _senderProbe.ExpectMsg<DeleteSnapshotsSuccess>(m => m.Criteria.Equals(criteria));

            SnapshotStore.Tell(new LoadSnapshot(Pid, new SnapshotSelectionCriteria(md.SequenceNr, md.Timestamp), long.MaxValue), _senderProbe.Ref);
            _senderProbe.ExpectMsg<LoadSnapshotResult>(result =>
                result.ToSequenceNr == long.MaxValue
                && result.Snapshot != null
                && result.Snapshot.Metadata.Equals(_metadata[3])
                && result.Snapshot.Snapshot.ToString() == "s-4");
        }

        [Fact]
        public void SnapshotStore_should_save_and_overwrite_snapshot_with_same_sequence_number()
        {
            var md = _metadata[4];
            SnapshotStore.Tell(new SaveSnapshot(md, "s-5-modified"), _senderProbe.Ref);
            var md2 = _senderProbe.ExpectMsg<SaveSnapshotSuccess>().Metadata;
            Assert.Equal(md.SequenceNr, md2.SequenceNr);
            SnapshotStore.Tell(new LoadSnapshot(Pid, new SnapshotSelectionCriteria(md.SequenceNr), long.MaxValue), _senderProbe.Ref);
            var result = _senderProbe.ExpectMsg<LoadSnapshotResult>();
            Assert.Equal("s-5-modified", result.Snapshot.Snapshot.ToString());
            Assert.Equal(md.SequenceNr, result.Snapshot.Metadata.SequenceNr);
            // metadata timestamp may have been changed
        }

        [Fact]
        public void SnapshotStore_should_save_bigger_size_snapshot()
        {
            var metadata = new SnapshotMetadata(Pid, 100);
            var bigSnapshot = new byte[SnapshotByteSizeLimit];
            new Random().NextBytes(bigSnapshot);
            SnapshotStore.Tell(new SaveSnapshot(metadata, bigSnapshot), _senderProbe.Ref);
            _senderProbe.ExpectMsg<SaveSnapshotSuccess>();
        }


        [Fact]
        public void ShouldSerializeSnapshots()
        {
            if (!SupportsSerialization) return;

            var probe = CreateTestProbe();
            var metadata = new SnapshotMetadata(Pid, 100L);
            var snap = new TestPayload(probe.Ref);

            SnapshotStore.Tell(new SaveSnapshot(metadata, snap), _senderProbe.Ref);
            _senderProbe.ExpectMsg<SaveSnapshotSuccess>(o =>
            {
                Assertions.AssertEqual(metadata.PersistenceId, o.Metadata.PersistenceId);
                Assertions.AssertEqual(metadata.SequenceNr, o.Metadata.SequenceNr);
            });

            var pid = Pid;
            SnapshotStore.Tell(new LoadSnapshot(pid, SnapshotSelectionCriteria.Latest, long.MaxValue), _senderProbe.Ref);
            _senderProbe.ExpectMsg<LoadSnapshotResult>(l =>
            {
                Assertions.AssertEqual(pid, l.Snapshot.Metadata.PersistenceId);
                Assertions.AssertEqual(100L, l.Snapshot.Metadata.SequenceNr);
                Assertions.AssertEqual(l.Snapshot.Snapshot, snap);
            });
        }
    }
}
