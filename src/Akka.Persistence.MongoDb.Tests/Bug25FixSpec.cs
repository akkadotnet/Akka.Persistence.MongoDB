using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.MongoDb.Journal;
using Akka.Util.Internal;
using Akka.Actor;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.MongoDb.Tests
{
    public class Bug25FixSpec : Akka.TestKit.Xunit2.TestKit, IClassFixture<DatabaseFixture>
    {
        class MyJournalActor : ReceivePersistentActor
        {
            private readonly IActorRef _target;

            public MyJournalActor(string persistenceId, IActorRef target)
            {
                PersistenceId = persistenceId;
                _target = target;

                Recover<string>(str =>
                {
                    target.Tell(str);
                });

                Command<string>(str =>
                {
                    Persist(str, s =>
                    {
                        Sender.Tell(s);
                    });
                });
            }

            public override string PersistenceId { get; }
        }

        private readonly MongoUrl _connectionString;
        private Lazy<IMongoDatabase> _database;
        public static readonly AtomicCounter Counter = new AtomicCounter(0);
        private readonly ITestOutputHelper _output;

        public Bug25FixSpec(ITestOutputHelper helper, DatabaseFixture fixture) 
            : base(CreateSpecConfig(fixture, Counter.Current), output: helper)
        {
            _connectionString = new MongoUrl(fixture.ConnectionString + Counter.Current);
            Counter.IncrementAndGet();
            _output = helper;
            _database = new Lazy<IMongoDatabase>(() => 
            new MongoClient(_connectionString)
            .GetDatabase(_connectionString.DatabaseName));
        }

        [Fact(DisplayName = "Bugfix: Should be able to deserialize older entities written without manifests")]
        public async Task Should_deserialize_entries_without_manifest()
        {
            var persistenceId = "fuber";
            var props = Props.Create(() => new MyJournalActor(persistenceId, TestActor));
            var myPersistentActor = Sys.ActorOf(props);
            Watch(myPersistentActor);

            myPersistentActor.Tell("hit");
            ExpectMsg("hit");

            Sys.Stop(myPersistentActor);
            ExpectTerminated(myPersistentActor);

            var records = _database.Value.GetCollection<JournalEntry>("EventJournal");

            var builder = Builders<JournalEntry>.Filter;
            var filter = builder.Eq(x => x.PersistenceId, persistenceId);
            filter &= builder.Eq(x => x.SequenceNr, 1);

            var sort = Builders<JournalEntry>.Sort.Ascending(x => x.SequenceNr);
            
            AwaitCondition(() => records.CountDocuments(filter) > 0);
            var collections = await records
                .Find(filter)
                .Sort(sort)
                .ToListAsync();

            var entry = collections.Single();
            entry.Manifest = null; // null out the manifest
            await records.FindOneAndReplaceAsync(filter, entry);

            // recreate the persistent actor
            var myPersistentActor2 = Sys.ActorOf(props);
            ExpectMsg("hit"); // receive message upon recovery
        }

        private static Config CreateSpecConfig(DatabaseFixture databaseFixture, int id)
        {
            var specString = @"
                akka.test.single-expect-default = 10s
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
                }";

            return ConfigurationFactory.ParseString(specString);
        }
    }
}
