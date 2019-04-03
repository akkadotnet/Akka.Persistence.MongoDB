//-----------------------------------------------------------------------
// <copyright file="MongoDbJournal.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence.Journal;
using Akka.Serialization;
using Akka.Util;
using MongoDB.Driver;

namespace Akka.Persistence.MongoDb.Journal
{
    /// <summary>
    /// An Akka.NET journal implementation that writes events asynchronously to MongoDB.
    /// </summary>
    public class MongoDbJournal : AsyncWriteJournal
    {
        private readonly MongoDbJournalSettings _settings;

        private Lazy<IMongoDatabase> _mongoDatabase;
        private Lazy<IMongoCollection<JournalEntry>> _journalCollection;
        private Lazy<IMongoCollection<MetadataEntry>> _metadataCollection;

        private readonly Func<IPersistentRepresentation, SerializationResult> _serialize;
        private readonly Func<Type, object, string, int?, object> _deserialize;

        public MongoDbJournal()
        {
            _settings = MongoDbPersistence.Get(Context.System).JournalSettings;

            var serialization = Context.System.Serialization;
            switch (_settings.StoredAs)
            {
                case StoredAsType.Binary:
                    _serialize = representation =>
                    {
                        var serializer = serialization.FindSerializerFor(representation.Payload);
                        return new SerializationResult(serializer.ToBinary(representation.Payload), serializer);
                    };
                    _deserialize = (type, serialized, manifest, serializerId) =>
                    {
                        if (serializerId.HasValue)
                        {
                            return serialization.Deserialize((byte[]) serialized, serializerId.Value, manifest);
                        }

                        var deserializer = serialization.FindSerializerForType(type);
                        return deserializer.FromBinary((byte[])serialized, type);
                    };
                    break;
                default:
                    _serialize = representation => new SerializationResult(representation.Payload, null);
                    _deserialize = (type, serialized, manifest, serializerId) => serialized;
                    break;
            }
        }

        protected override void PreStart()
        {
            base.PreStart();

            _mongoDatabase = new Lazy<IMongoDatabase>(() =>
            {
                var connectionString = new MongoUrl(_settings.ConnectionString);
                var client = new MongoClient(connectionString);

                return client.GetDatabase(connectionString.DatabaseName);
            });

            _journalCollection = new Lazy<IMongoCollection<JournalEntry>>(() =>
            {
                var collection = _mongoDatabase.Value.GetCollection<JournalEntry>(_settings.Collection);

                if (_settings.AutoInitialize)
                {
                    collection.Indexes.CreateOneAsync(
                        Builders<JournalEntry>.IndexKeys
                            .Ascending(entry => entry.PersistenceId)
                            .Descending(entry => entry.SequenceNr))
                            .Wait();
                }

                return collection;
            });

            _metadataCollection = new Lazy<IMongoCollection<MetadataEntry>>(() =>
            {
                var collection = _mongoDatabase.Value.GetCollection<MetadataEntry>(_settings.MetadataCollection);

                if (_settings.AutoInitialize)
                {
                    collection.Indexes.CreateOneAsync(
                        Builders<MetadataEntry>.IndexKeys
                            .Ascending(entry => entry.PersistenceId))
                            .Wait();
                }

                return collection;
            });
        }

        public override async Task ReplayMessagesAsync(IActorContext context, string persistenceId, long fromSequenceNr, long toSequenceNr, long max, Action<IPersistentRepresentation> recoveryCallback)
        {
            // Limit allows only integer
            var limitValue = max >= int.MaxValue ? int.MaxValue : (int)max;

            // Do not replay messages if limit equal zero
            if (limitValue == 0)
                return;

            var builder = Builders<JournalEntry>.Filter;
            var filter = builder.Eq(x => x.PersistenceId, persistenceId);
            if (fromSequenceNr > 0)
                filter &= builder.Gte(x => x.SequenceNr, fromSequenceNr);
            if (toSequenceNr != long.MaxValue)
                filter &= builder.Lte(x => x.SequenceNr, toSequenceNr);

            var sort = Builders<JournalEntry>.Sort.Ascending(x => x.SequenceNr);

            var collections = await _journalCollection.Value
                .Find(filter)
                .Sort(sort)
                .Limit(limitValue)
                .ToListAsync();

            collections.ForEach(doc =>
            {
                recoveryCallback(ToPersistenceRepresentation(doc, context.Sender));
            });
        }

        public override async Task<long> ReadHighestSequenceNrAsync(string persistenceId, long fromSequenceNr)
        {
            var builder = Builders<MetadataEntry>.Filter;
            var filter = builder.Eq(x => x.PersistenceId, persistenceId);

            var highestSequenceNr = await _metadataCollection.Value.Find(filter).Project(x => x.SequenceNr).FirstOrDefaultAsync();

            return highestSequenceNr;
        }

        protected override async Task<IImmutableList<Exception>> WriteMessagesAsync(IEnumerable<AtomicWrite> messages)
        {
            var messageList = messages.ToList();
            var writeTasks = messageList.Select(async message =>
            {
                var persistentMessages = ((IImmutableList<IPersistentRepresentation>)message.Payload).ToArray();

                var journalEntries = persistentMessages.Select(ToJournalEntry).ToList();
                await _journalCollection.Value.InsertManyAsync(journalEntries);
            });

            await SetHighSequenceId(messageList);

            return await Task<IImmutableList<Exception>>
                .Factory
                .ContinueWhenAll(writeTasks.ToArray(),
                    tasks => tasks.Select(t => t.IsFaulted ? TryUnwrapException(t.Exception) : null).ToImmutableList());
        }

        protected override Task DeleteMessagesToAsync(string persistenceId, long toSequenceNr)
        {
            var builder = Builders<JournalEntry>.Filter;
            var filter = builder.Eq(x => x.PersistenceId, persistenceId);

            if (toSequenceNr != long.MaxValue)
                filter &= builder.Lte(x => x.SequenceNr, toSequenceNr);

            return _journalCollection.Value.DeleteManyAsync(filter);
        }

        private JournalEntry ToJournalEntry(IPersistentRepresentation message)
        {
            var serializationResult = _serialize(message);
            var serializer = serializationResult.Serializer;
            var hasSerializer = serializer != null;

            var manifest = "";
            if (hasSerializer && serializer is SerializerWithStringManifest)
                manifest = ((SerializerWithStringManifest)serializer).Manifest(message.Payload);
            else if (hasSerializer && serializer.IncludeManifest)
                manifest = message.GetType().TypeQualifiedName();
            else
                manifest = string.IsNullOrEmpty(message.Manifest)
                    ? message.GetType().TypeQualifiedName()
                    : message.Manifest;

            return new JournalEntry
            {
                Id = message.PersistenceId + "_" + message.SequenceNr,
                IsDeleted = message.IsDeleted,
                Payload = serializationResult.Payload,
                PersistenceId = message.PersistenceId,
                SequenceNr = message.SequenceNr,
                Manifest = manifest,
                SerializerId = serializer?.Identifier
            };
        }

        private Persistent ToPersistenceRepresentation(JournalEntry entry, IActorRef sender)
        {
            int? serializerId = null;
            Type type = null;
            if (!entry.SerializerId.HasValue)
                type = Type.GetType(entry.Manifest, true);
            else
                serializerId = entry.SerializerId;

            var deserialized = _deserialize(type, entry.Payload, entry.Manifest, serializerId);

            return new Persistent(deserialized, entry.SequenceNr, entry.PersistenceId, entry.Manifest, entry.IsDeleted, sender);
        }

        private async Task SetHighSequenceId(IList<AtomicWrite> messages)
        {
            var persistenceId = messages.Select(c => c.PersistenceId).First();
            var highSequenceId = messages.Max(c => c.HighestSequenceNr);
            var builder = Builders<MetadataEntry>.Filter;
            var filter = builder.Eq(x => x.PersistenceId, persistenceId);

            var metadataEntry = new MetadataEntry
            {
                Id = persistenceId,
                PersistenceId = persistenceId,
                SequenceNr = highSequenceId
            };

            await _metadataCollection.Value.ReplaceOneAsync(filter, metadataEntry, new UpdateOptions() { IsUpsert = true });
        }
    }
}
