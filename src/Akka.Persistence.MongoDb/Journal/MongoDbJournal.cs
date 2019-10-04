//-----------------------------------------------------------------------
// <copyright file="MongoDbJournal.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence.Journal;
using Akka.Persistence.MongoDb.Query;
using Akka.Streams;
using Akka.Streams.Dsl;
using MongoDB.Bson;
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

        private readonly HashSet<string> _allPersistenceIds = new HashSet<string>();
        private readonly HashSet<IActorRef> _allPersistenceIdSubscribers = new HashSet<IActorRef>();
        private readonly Dictionary<string, ISet<IActorRef>> _tagSubscribers = 
            new Dictionary<string, ISet<IActorRef>>();
        private readonly Dictionary<string, ISet<IActorRef>> _persistenceIdSubscribers 
            = new Dictionary<string, ISet<IActorRef>>();

        private readonly Func<object, SerializationResult> _serialize;
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
                        var serializer = serialization.FindSerializerFor(representation);
                        return new SerializationResult(serializer.ToBinary(representation), serializer);
                    };
                    _deserialize = (type, serialized, manifest, serializerId) =>
                    {
                        if (serializerId.HasValue)
                        {
                            /*
                             * Backwards compat: check to see if manifest is populated before using it.
                             * Otherwise, fall back to using the stored type data instead.
                             * Per: https://github.com/AkkaNetContrib/Akka.Persistence.MongoDB/issues/57
                             */
                            if (string.IsNullOrEmpty(manifest)) 
                                return serialization.Deserialize((byte[]) serialized, serializerId.Value, type);
                            return serialization.Deserialize((byte[])serialized, serializerId.Value, manifest);
                        }

                        var deserializer = serialization.FindSerializerForType(type);
                        return deserializer.FromBinary((byte[])serialized, type);
                    };
                    break;
                default:
                    _serialize = representation => new SerializationResult(representation, null);
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
                    var modelForEntryAndSequenceNr = new CreateIndexModel<JournalEntry>(Builders<JournalEntry>
                        .IndexKeys
                        .Ascending(entry => entry.PersistenceId)
                        .Descending(entry => entry.SequenceNr));

                    collection.Indexes
                        .CreateOneAsync(modelForEntryAndSequenceNr, cancellationToken:CancellationToken.None)
                        .Wait();

                    var modelWithOrdering = new CreateIndexModel<JournalEntry>(
                        Builders<JournalEntry>
                            .IndexKeys
                            .Ascending(entry => entry.Ordering));

                    collection.Indexes
                        .CreateOne(modelWithOrdering);
                }

                return collection;
            });

            _metadataCollection = new Lazy<IMongoCollection<MetadataEntry>>(() =>
            {
                var collection = _mongoDatabase.Value.GetCollection<MetadataEntry>(_settings.MetadataCollection);

                if (_settings.AutoInitialize)
                {
                    var modelWithAscendingPersistenceId = new CreateIndexModel<MetadataEntry>(
                        Builders<MetadataEntry>
                            .IndexKeys
                            .Ascending(entry => entry.PersistenceId));

                    collection.Indexes
                        .CreateOneAsync(modelWithAscendingPersistenceId, cancellationToken:CancellationToken.None)
                            .Wait();
                }

                return collection;
            });
        }

        public override async Task ReplayMessagesAsync(IActorContext context, string persistenceId, long fromSequenceNr, long toSequenceNr, long max, Action<IPersistentRepresentation> recoveryCallback)
        {
            NotifyNewPersistenceIdAdded(persistenceId);

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

            collections.ForEach(doc => {
                recoveryCallback(ToPersistenceRepresentation(doc, context.Sender));
            });
        }

        /// <summary>
        /// Replays all events with given tag withing provided boundaries from current database.
        /// </summary>
        /// <param name="replay">TBD</param>
        /// <returns>TBD</returns>
        private async Task<long> ReplayTaggedMessagesAsync(ReplayTaggedMessages replay)
        {
            /*
             *  NOTE: limit is used like a pagination value, not a cap on the amount
             * of data returned by a query. This was at the root of https://github.com/akkadotnet/Akka.Persistence.MongoDB/issues/80
             */
            // Limit allows only integer
            var limitValue = replay.Max >= int.MaxValue ? int.MaxValue : (int)replay.Max;
            var fromSequenceNr = replay.FromOffset;
            var toSequenceNr = replay.ToOffset;
            var tag = replay.Tag;

            var builder = Builders<JournalEntry>.Filter;
            var seqNoFilter = builder.AnyEq(x => x.Tags, tag);
            if (fromSequenceNr > 0)
                seqNoFilter &= builder.Gt(x => x.Ordering, new BsonTimestamp(fromSequenceNr));
            if (toSequenceNr != long.MaxValue)
                seqNoFilter &= builder.Lte(x => x.Ordering, new BsonTimestamp(toSequenceNr));

            
            // Need to know what the highest seqNo of this query will be
            // and return that as part of the RecoverySuccess message
            var maxSeqNoEntry = await _journalCollection.Value.Find(seqNoFilter)
                .SortByDescending(x => x.Ordering)
                .Limit(1)
                .SingleOrDefaultAsync();

            if (maxSeqNoEntry == null)
                return 0L; // recovered nothing

            var maxOrderingId = maxSeqNoEntry.Ordering.Value;
            var toSeqNo = Math.Min(toSequenceNr, maxOrderingId);

            var readFilter = builder.AnyEq(x => x.Tags, tag);
            if (fromSequenceNr > 0)
                readFilter &= builder.Gt(x => x.Ordering, new BsonTimestamp(fromSequenceNr));
            if (toSequenceNr != long.MaxValue)
                readFilter &= builder.Lte(x => x.Ordering, new BsonTimestamp(toSeqNo));
            var sort = Builders<JournalEntry>.Sort.Ascending(x => x.Ordering);

            await _journalCollection.Value
                .Find(readFilter)
                .Sort(sort)
                .Limit(limitValue)
                .ForEachAsync(entry => {
                    var persistent = ToPersistenceRepresentation(entry, ActorRefs.NoSender);
                    foreach (var adapted in AdaptFromJournal(persistent))
                        replay.ReplyTo.Tell(new ReplayedTaggedMessage(adapted, tag, entry.Ordering.Value), 
                            ActorRefs.NoSender);
                });

            return maxOrderingId;
        }

        public override async Task<long> ReadHighestSequenceNrAsync(string persistenceId, long fromSequenceNr)
        {
            NotifyNewPersistenceIdAdded(persistenceId);

            var builder = Builders<MetadataEntry>.Filter;
            var filter = builder.Eq(x => x.PersistenceId, persistenceId);

            var highestSequenceNr = await _metadataCollection.Value.Find(filter).Project(x => x.SequenceNr).FirstOrDefaultAsync();

            return highestSequenceNr;
        }

        protected override async Task<IImmutableList<Exception>> WriteMessagesAsync(IEnumerable<AtomicWrite> messages)
        {
            var allTags = new HashSet<string>();
            var messageList = messages.ToList();

            var writeTasks = messageList.Select(async message => {
                var persistentMessages = ((IImmutableList<IPersistentRepresentation>)message.Payload).ToArray();

                var journalEntries = persistentMessages.Select(ToJournalEntry).ToList();
                await _journalCollection.Value.InsertManyAsync(journalEntries);

                NotifyNewPersistenceIdAdded(message.PersistenceId);
            });

            await SetHighSequenceId(messageList);

            var result = await Task<IImmutableList<Exception>>
                .Factory
                .ContinueWhenAll(writeTasks.ToArray(),
                    tasks => tasks.Select(t => t.IsFaulted ? TryUnwrapException(t.Exception) : null).ToImmutableList());

            if (HasTagSubscribers && allTags.Count != 0) {
                foreach (var tag in allTags) {
                    NotifyTagChange(tag);
                }
            }

            return result;
        }

        protected override Task DeleteMessagesToAsync(string persistenceId, long toSequenceNr)
        {
            NotifyNewPersistenceIdAdded(persistenceId);

            var builder = Builders<JournalEntry>.Filter;
            var filter = builder.Eq(x => x.PersistenceId, persistenceId);

            if (toSequenceNr != long.MaxValue)
                filter &= builder.Lte(x => x.SequenceNr, toSequenceNr);

            return _journalCollection.Value.DeleteManyAsync(filter);
        }

        private JournalEntry ToJournalEntry(IPersistentRepresentation message)
        {
            object payload = message.Payload;
            if (message.Payload is Tagged tagged)
                payload = tagged.Payload;

            var serializationResult = _serialize(payload);
            var serializer = serializationResult.Serializer;
            var hasSerializer = serializer != null;

            var manifest = "";
            if (hasSerializer && serializer is SerializerWithStringManifest stringManifest)
                manifest = stringManifest.Manifest(message.Payload);
            else if (hasSerializer && serializer.IncludeManifest)
                manifest = message.GetType().TypeQualifiedName();
            else
                manifest = string.IsNullOrEmpty(message.Manifest)
                    ? message.GetType().TypeQualifiedName()
                    : message.Manifest;

            return new JournalEntry
            {
                Id = message.PersistenceId + "_" + message.SequenceNr,
                Ordering = new BsonTimestamp(0), // Auto-populates with timestamp
                IsDeleted = message.IsDeleted,
                Payload = serializationResult.Payload,
                PersistenceId = message.PersistenceId,
                SequenceNr = message.SequenceNr,
                Manifest = manifest,
                Tags = tagged.Tags?.ToList(),
                SerializerId = serializer?.Identifier
            };
        }

        private Persistent ToPersistenceRepresentation(JournalEntry entry, IActorRef sender)
        {
            int? serializerId = null;
            Type type = null;
            if (!entry.SerializerId.HasValue && !string.IsNullOrEmpty(entry.Manifest))
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

        protected override bool ReceivePluginInternal(object message)
        {
            return message.Match()
                .With<ReplayTaggedMessages>(replay => {
                    ReplayTaggedMessagesAsync(replay)
                    .PipeTo(replay.ReplyTo, success: h => new RecoverySuccess(h), failure: e => new ReplayMessagesFailure(e));
                })
                .With<SubscribePersistenceId>(subscribe => {
                    AddPersistenceIdSubscriber(Sender, subscribe.PersistenceId);
                    Context.Watch(Sender);
                })
                .With<SubscribeAllPersistenceIds>(subscribe => {
                    AddAllPersistenceIdSubscriber(Sender);
                    Context.Watch(Sender);
                })
                .With<SubscribeTag>(subscribe => {
                    AddTagSubscriber(Sender, subscribe.Tag);
                    Context.Watch(Sender);
                })
                .With<Terminated>(terminated => RemoveSubscriber(terminated.ActorRef))
                .WasHandled;
        }

        private void AddAllPersistenceIdSubscriber(IActorRef subscriber)
        {
            lock (_allPersistenceIdSubscribers) {
                _allPersistenceIdSubscribers.Add(subscriber);
            }
            subscriber.Tell(new CurrentPersistenceIds(GetAllPersistenceIds()));
        }

        private void AddTagSubscriber(IActorRef subscriber, string tag)
        {
            if (!_tagSubscribers.TryGetValue(tag, out var subscriptions)) {
                subscriptions = new HashSet<IActorRef>();
                _tagSubscribers.Add(tag, subscriptions);
            }

            subscriptions.Add(subscriber);
        }

        private IEnumerable<string> GetAllPersistenceIds()
        {
            return _journalCollection.Value.AsQueryable()
                .Select(je => je.PersistenceId)
                .Distinct()
                .ToList();
        }

        private void AddPersistenceIdSubscriber(IActorRef subscriber, string persistenceId)
        {
            if (!_persistenceIdSubscribers.TryGetValue(persistenceId, out var subscriptions)) {
                subscriptions = new HashSet<IActorRef>();
                _persistenceIdSubscribers.Add(persistenceId, subscriptions);
            }

            subscriptions.Add(subscriber);
        }

        private void RemoveSubscriber(IActorRef subscriber)
        {
            var pidSubscriptions = _persistenceIdSubscribers.Values.Where(x => x.Contains(subscriber));
            foreach (var subscription in pidSubscriptions)
                subscription.Remove(subscriber);

            var tagSubscriptions = _tagSubscribers.Values.Where(x => x.Contains(subscriber));
            foreach (var subscription in tagSubscriptions)
                subscription.Remove(subscriber);

            _allPersistenceIdSubscribers.Remove(subscriber);
        }

        protected bool HasAllPersistenceIdSubscribers => _allPersistenceIdSubscribers.Count != 0;
        protected bool HasTagSubscribers => _tagSubscribers.Count != 0;
        protected bool HasPersistenceIdSubscribers => _persistenceIdSubscribers.Count != 0;

        private void NotifyNewPersistenceIdAdded(string persistenceId)
        {
            var isNew = TryAddPersistenceId(persistenceId);
            if (isNew && HasAllPersistenceIdSubscribers) {
                var added = new PersistenceIdAdded(persistenceId);
                foreach (var subscriber in _allPersistenceIdSubscribers)
                    subscriber.Tell(added);
            }
        }

        private bool TryAddPersistenceId(string persistenceId)
        {
            lock (_allPersistenceIds) {
                return _allPersistenceIds.Add(persistenceId);
            }
        }

        private void NotifyPersistenceIdChange(string persistenceId)
        {
            if (_persistenceIdSubscribers.TryGetValue(persistenceId, out var subscribers)) {
                var changed = new EventAppended(persistenceId);
                foreach (var subscriber in subscribers)
                    subscriber.Tell(changed);
            }
        }

        private void NotifyTagChange(string tag)
        {
            if (_tagSubscribers.TryGetValue(tag, out var subscribers)) {
                var changed = new TaggedEventAppended(tag);
                foreach (var subscriber in subscribers)
                    subscriber.Tell(changed);
            }
        }

    }


}
