using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence.Journal;
using Akka.Serialization;
using MongoDB.Driver;

namespace Akka.Persistence.MongoDb.Journal
{
    /// <summary>
    /// An Akka.NET journal implementation that writes events asynchronously to MongoDB.
    /// </summary>
    public class MongoDbJournal : AsyncWriteJournal
    {
        private static readonly Type PersistentRepresentationType = typeof(IPersistentRepresentation);
        private readonly Serializer _serializer;

        private readonly IMongoCollection<JournalEntry> _collection;

        public MongoDbJournal()
        {
            var mongoDbExtension = MongoDbPersistence.Instance.Apply(Context.System);

            _collection = mongoDbExtension.JournalCollection;
            _serializer = Context.System.Serialization.FindSerializerForType(PersistentRepresentationType);
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

            if (fromSequenceNr > 0)
                filter &= builder.Gte(x => x.SequenceNr, fromSequenceNr);

            if (toSequenceNr != long.MaxValue)
                filter &= builder.Lte(x => x.SequenceNr, toSequenceNr);

            return
                _collection
                    .Find(filter)
                    .Limit(maxValue)
                    .ForEachAsync(doc => replayCallback(ToPersistanceRepresentation(doc, sender)));
        }

        public override Task<long> ReadHighestSequenceNrAsync(string persistenceId, long fromSequenceNr)
        {
            var builder = Builders<JournalEntry>.Filter;
            var filter = builder.Eq(x => x.PersistenceId, persistenceId);
            var sort = Builders<JournalEntry>.Sort.Descending(x => x.SequenceNr);

            return
                _collection
                    .Find(filter)
                    .Sort(sort)
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
                Payload = _serializer.ToBinary(message),
                PersistenceId = message.PersistenceId,
                SequenceNr = message.SequenceNr
            };
        }

        private Persistent ToPersistanceRepresentation(JournalEntry entry, IActorRef sender)
        {
            var payload =
                (IPersistentRepresentation)_serializer.FromBinary(entry.Payload, typeof(IPersistentRepresentation));

            return new Persistent(payload.Payload, entry.SequenceNr, entry.PersistenceId, entry.IsDeleted, sender);
        }
    }
}
