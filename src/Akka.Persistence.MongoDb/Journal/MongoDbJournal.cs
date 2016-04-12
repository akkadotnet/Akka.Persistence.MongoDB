using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence.Journal;
using MongoDB.Driver;

namespace Akka.Persistence.MongoDb.Journal
{
    /// <summary>
    /// An Akka.NET journal implementation that writes events asynchronously to MongoDB.
    /// </summary>
    public class MongoDbJournal : AsyncWriteJournal
    {
        private readonly IMongoCollection<JournalEntry> _collection;

        public MongoDbJournal()
        {
            _collection = MongoDbPersistence.Instance.Apply(Context.System).JournalCollection;
        }

        public override Task ReplayMessagesAsync(string persistenceId, long fromSequenceNr, long toSequenceNr, long max, Action<IPersistentRepresentation> replayCallback)
        {
            // Limit(0) doesn't work...
            if (max == 0)
                return Task.Run(() => {});

            // Limit allows only integer
            var maxValue = max >= int.MaxValue ? int.MaxValue : (int)max;
            var sender = Context.Sender;
            var builder = Builders<JournalEntry>.Filter;
            var filter = builder.Eq(x => x.PersistenceId, persistenceId);
            var sort = Builders<JournalEntry>.Sort.Ascending(x => x.SequenceNr);

            if (fromSequenceNr > 0)
                filter &= builder.Gte(x => x.SequenceNr, fromSequenceNr);

            if (toSequenceNr != long.MaxValue)
                filter &= builder.Lte(x => x.SequenceNr, toSequenceNr);

            return
                _collection
                    .Find(filter)
                    .Sort(sort)
                    .Limit(maxValue)
                    .ForEachAsync(doc => replayCallback(ToPersistanceRepresentation(doc, sender)));
        }

        public override Task<long> ReadHighestSequenceNrAsync(string persistenceId, long fromSequenceNr)
        {
            var builder = Builders<JournalEntry>.Filter;
            var filter = builder.Eq(x => x.PersistenceId, persistenceId);

            return
                _collection
                    .Find(filter)
                    .SortByDescending(x=>x.SequenceNr)
                    .Limit(1)
                    .Project(x => x.SequenceNr)
                    .FirstOrDefaultAsync();
        }

        protected override Task WriteMessagesAsync(IEnumerable<IPersistentRepresentation> messages)
        {
            var entries = messages.Select(ToJournalEntry).ToList();
            return _collection.InsertManyAsync(entries);
        }

        protected override Task DeleteMessagesToAsync(string persistenceId, long toSequenceNr, bool isPermanent)
        {
            var builder = Builders<JournalEntry>.Filter;
            var filter = builder.Eq(x => x.PersistenceId, persistenceId);
            var update = Builders<JournalEntry>.Update.Set(x => x.IsDeleted, true);

            if (toSequenceNr != long.MaxValue)
                filter &= builder.Lte(x => x.SequenceNr, toSequenceNr);

            if (isPermanent)
                return _collection.DeleteManyAsync(filter);

            return _collection.UpdateManyAsync(filter, update);
        }

        private JournalEntry ToJournalEntry(IPersistentRepresentation message)
        {
            return new JournalEntry
            {
                Id = message.PersistenceId + "_" + message.SequenceNr,
                IsDeleted = message.IsDeleted,
                Payload = message.Payload,
                PersistenceId = message.PersistenceId,
                SequenceNr = message.SequenceNr,
                Manifest = message.Manifest
            };
        }

        private Persistent ToPersistanceRepresentation(JournalEntry entry, IActorRef sender)
        {
            return new Persistent(entry.Payload, entry.SequenceNr, entry.Manifest, entry.PersistenceId, entry.IsDeleted, sender);
        }
    }
}
