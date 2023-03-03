﻿//-----------------------------------------------------------------------
// <copyright file="MongoDbJournal.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Actor;
using Akka.Persistence.Journal;
using Akka.Persistence.MongoDb.Query;
using Akka.Util;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Util.Internal;
using Akka.Streams.Stage;

namespace Akka.Persistence.MongoDb.Journal
{
    /// <summary>
    /// An Akka.NET journal implementation that writes events asynchronously to MongoDB.
    /// </summary>
    public class MongoDbJournal : AsyncWriteJournal
    {
        private static readonly BsonTimestamp ZeroTimestamp = new BsonTimestamp(0);
        
        private readonly MongoDbJournalSettings _settings;
        private MongoClient _client;
        private Lazy<IMongoDatabase> _mongoDatabase;
        private Lazy<IMongoCollection<JournalEntry>> _journalCollection;
        private Lazy<IMongoCollection<MetadataEntry>> _metadataCollection;

        private ImmutableDictionary<string, IImmutableSet<IActorRef>> _persistenceIdSubscribers = ImmutableDictionary.Create<string, IImmutableSet<IActorRef>>();
        private ImmutableDictionary<string, IImmutableSet<IActorRef>> _tagSubscribers = ImmutableDictionary.Create<string, IImmutableSet<IActorRef>>();
        private readonly HashSet<IActorRef> _newEventsSubscriber = new HashSet<IActorRef>();
        
        private readonly Akka.Serialization.Serialization _serialization;

        public MongoDbJournal() : this(MongoDbPersistence.Get(Context.System).JournalSettings)
        { }

        // This constructor is needed because config can come from both Akka.Persistence and Akka.Cluster.Sharding
        public MongoDbJournal(Config config) : this(new MongoDbJournalSettings(config))
        { }

        private MongoDbJournal(MongoDbJournalSettings settings)
        {
            _settings = settings;
            _serialization = Context.System.Serialization;
        }

        protected override void PreStart()
        {
            base.PreStart();

            _mongoDatabase = new Lazy<IMongoDatabase>(() =>
            {
                var setupOption = Context.System.Settings.Setup.Get<MongoDbPersistenceSetup>();
                if (setupOption.HasValue && setupOption.Value.JournalConnectionSettings != null)
                {
                    _client = new MongoClient(setupOption.Value.JournalConnectionSettings);
                    return _client.GetDatabase(setupOption.Value.JournalDatabaseName);
                }
                
                var connectionString = new MongoUrl(_settings.ConnectionString);
                _client = new MongoClient(connectionString);
                return _client.GetDatabase(connectionString.DatabaseName);
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
                        .CreateOneAsync(modelForEntryAndSequenceNr, cancellationToken: CancellationToken.None)
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
                        .CreateOneAsync(modelWithAscendingPersistenceId, cancellationToken: CancellationToken.None)
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
            // Limit allows only integer;
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
                .ForEachAsync(entry =>
                {
                    var persistent = ToPersistenceRepresentation(entry, ActorRefs.NoSender);
                    foreach (var adapted in AdaptFromJournal(persistent))
                        replay.ReplyTo.Tell(new ReplayedTaggedMessage(adapted, tag, entry.Ordering.Value),
                            ActorRefs.NoSender);
                });

            return maxOrderingId;
        }

        /// <summary>
        /// Asynchronously reads a highest sequence number of the event stream related with provided <paramref name="persistenceId"/>.
        /// </summary>
        /// <param name="persistenceId">TBD</param>
        /// <param name="fromSequenceNr">TBD</param>
        /// <returns>long</returns>
        public override async Task<long> ReadHighestSequenceNrAsync(string persistenceId, long fromSequenceNr)
        {

            var builder = Builders<MetadataEntry>.Filter;
            var filter = builder.Eq(x => x.PersistenceId, persistenceId);

            
            var metadataHighestSequenceNrTask = _metadataCollection.Value.Find(filter).Project(x => x.SequenceNr).FirstOrDefaultAsync();

            var journalHighestSequenceNrTask = _journalCollection.Value.Find(Builders<JournalEntry>
                .Filter.Eq(x => x.PersistenceId, persistenceId))
                .SortByDescending(x => x.SequenceNr)
                .Project(x => x.SequenceNr)
                .FirstOrDefaultAsync();

            // journal data is usually good enough, except in cases when it's been deleted.
            await Task.WhenAll(metadataHighestSequenceNrTask, journalHighestSequenceNrTask);

            return Math.Max(journalHighestSequenceNrTask.Result, metadataHighestSequenceNrTask.Result);
        }

        protected override async Task<IImmutableList<Exception>> WriteMessagesAsync(IEnumerable<AtomicWrite> messages)
        {
            var allTags = ImmutableHashSet<string>.Empty;
            var persistentIds = new HashSet<string>();
            var messageList = messages.ToList();

            var writeTasks = messageList.Select(async message =>
            {
                var persistentMessages = ((IImmutableList<IPersistentRepresentation>)message.Payload);

                if (HasTagSubscribers)
                {
                    foreach (var p in persistentMessages)
                    {
                        if (p.Payload is Tagged t)
                        {
                            allTags = allTags.Union(t.Tags);
                        }
                    }
                }

                var journalEntries = persistentMessages.Select(ToJournalEntry);
                await InsertEntries(journalEntries);

                if (HasPersistenceIdSubscribers)
                    persistentIds.Add(message.PersistenceId);
            });

            var result = await Task<IImmutableList<Exception>>
                .Factory
                .ContinueWhenAll(writeTasks.ToArray(),
                    tasks => tasks.Select(t => t.IsFaulted ? TryUnwrapException(t.Exception) : null).ToImmutableList());

            if (HasPersistenceIdSubscribers)
            {
                foreach (var id in persistentIds)
                {
                    NotifyPersistenceIdChange(id);
                }
            }

            if (HasTagSubscribers && allTags.Count != 0)
            {
                foreach (var tag in allTags)
                {
                    NotifyTagChange(tag);
                }
            }
            if (HasNewEventSubscribers)
                NotifyNewEventAppended();
            return result;
        }
        private async ValueTask InsertEntries(IEnumerable<JournalEntry> entries)
        {
            if (_settings.Transaction)
            {
                var sessionOptions = new ClientSessionOptions { };
                using (var session = await _client.StartSessionAsync(sessionOptions/*, cancellationToken*/))
                {
                    // Begin transaction
                    session.StartTransaction();
                    try
                    {
                        foreach (var entry in entries)
                        {
                            await _journalCollection.Value.InsertOneAsync(session, entry);
                        }

                        //All good , lets commit the transaction 
                        await session.CommitTransactionAsync();
                    }
                    catch (Exception ex) 
                    {
                        await session.AbortTransactionAsync();
                        throw ex;   
                    }

                }
            }
            else
                await _journalCollection.Value.InsertManyAsync(entries, new InsertManyOptions { IsOrdered = true });
        }
        private void NotifyNewEventAppended()
        {
            if (HasNewEventSubscribers)
            {
                foreach (var subscriber in _newEventsSubscriber)
                {
                    subscriber.Tell(NewEventAppended.Instance);
                }
            }
        }
        protected override async Task DeleteMessagesToAsync(string persistenceId, long toSequenceNr)
        {
            var builder = Builders<JournalEntry>.Filter;
            var filter = builder.Eq(x => x.PersistenceId, persistenceId);

            // read highest sequence number before we start
            var highestSeqNo = await ReadHighestSequenceNrAsync(persistenceId, 0L);

            if (toSequenceNr != long.MaxValue)
                filter &= builder.Lte(x => x.SequenceNr, toSequenceNr);

            // only update the sequence number of the top of the journal
            // is about to be deleted.
            if (highestSeqNo <= toSequenceNr) 
            {
                await SetHighSequenceId(persistenceId, highestSeqNo);
            }

            await _journalCollection.Value.DeleteManyAsync(filter);
        }

        private JournalEntry ToJournalEntry(IPersistentRepresentation message)
        {
            object payload = message.Payload;
            if (message.Payload is Tagged tagged)
            {
                payload = tagged.Payload;
                message = message.WithPayload(payload); // need to update the internal payload when working with tags
            }

            // per https://github.com/akkadotnet/Akka.Persistence.MongoDB/issues/107
            // BSON serialization
            if (_settings.LegacySerialization)
            {
                var manifest = string.IsNullOrEmpty(message.Manifest) ? payload.GetType().TypeQualifiedName() : message.Manifest;
                return new JournalEntry
                {
                    Id = message.PersistenceId + "_" + message.SequenceNr,
                    Ordering = new BsonTimestamp(0), // Auto-populates with timestamp
                    IsDeleted = message.IsDeleted,
                    Payload = payload,
                    PersistenceId = message.PersistenceId,
                    SequenceNr = message.SequenceNr,
                    Manifest = manifest,
                    Tags = tagged.Tags?.ToList(),
                    SerializerId = null // don't need a serializer ID here either; only for backwards-compat
                };
            }

            // default serialization
            var serializer = _serialization.FindSerializerFor(message);
            var binary = serializer.ToBinary(message);


            return new JournalEntry
            {
                Id = message.PersistenceId + "_" + message.SequenceNr,
                Ordering = new BsonTimestamp(0), // Auto-populates with timestamp
                IsDeleted = message.IsDeleted,
                Payload = binary,
                PersistenceId = message.PersistenceId,
                SequenceNr = message.SequenceNr,
                Manifest = string.Empty, // don't need a manifest here - it's embedded inside the PersistentMessage
                Tags = tagged.Tags?.ToList(),
                SerializerId = null // don't need a serializer ID here either; only for backwards-compat
            };
        }

        private static long ToTicks(BsonTimestamp bson)
        {


            // BSON Timestamps are stored natively as Unix epoch seconds + an ordinal value

            // need to use BsonTimestamp.Timestamp because the ordinal value doesn't actually have any
            // bearing on the time - it's used to try to somewhat order the events that all occurred concurrently
            // according to the MongoDb clock. No need to include that data in the EventEnvelope.Timestamp field
            // which is used entirely for end-user purposes.
            //
            // See https://docs.mongodb.com/manual/reference/bson-types/#timestamps
            bson = bson ?? ZeroTimestamp;
            return DateTimeOffset.FromUnixTimeSeconds(bson.Timestamp).Ticks;
        }

        private Persistent ToPersistenceRepresentation(JournalEntry entry, IActorRef sender)
        {

            if (_settings.LegacySerialization)
            {
                var manifest = string.IsNullOrEmpty(entry.Manifest) ? entry.Payload.GetType().TypeQualifiedName() : entry.Manifest;

                return new Persistent(
                    entry.Payload,
                    entry.SequenceNr,
                    entry.PersistenceId,
                    manifest,
                    entry.IsDeleted,
                    sender,
                    timestamp: ToTicks(entry.Ordering)); // MongoDb timestamps are stored as Unix Epoch
            }

            var legacy = entry.SerializerId.HasValue || !string.IsNullOrEmpty(entry.Manifest);
            if (!legacy)
            {
                var ser = _serialization.FindSerializerForType(typeof(Persistent));
                var output = ser.FromBinary<Persistent>((byte[])entry.Payload);

                // backwards compatibility for https://github.com/akkadotnet/akka.net/pull/4680
                // it the timestamp is not defined in the binary payload
                if (output.Timestamp == 0L)
                {
                    output = (Persistent)output.WithTimestamp(ToTicks(entry.Ordering));
                }

                return output;
            }

            int? serializerId = null;
            Type type = null;

            // legacy serialization
            if (!entry.SerializerId.HasValue && !string.IsNullOrEmpty(entry.Manifest))
                type = Type.GetType(entry.Manifest, true);
            else
                serializerId = entry.SerializerId;

            if (entry.Payload is byte[] bytes)
            {
                object deserialized = null;
                if (serializerId.HasValue)
                {
                    deserialized = _serialization.Deserialize(bytes, serializerId.Value, entry.Manifest);
                }
                else
                {
                    var deserializer = _serialization.FindSerializerForType(type);
                    deserialized = deserializer.FromBinary(bytes, type);
                }

                if (deserialized is Persistent p)
                    return (Persistent)p.WithTimestamp(ToTicks(entry.Ordering));

                return new Persistent(deserialized, entry.SequenceNr, entry.PersistenceId, entry.Manifest, entry.IsDeleted, sender, timestamp: ToTicks(entry.Ordering));
            }
            else // backwards compat for object serialization - Payload was already deserialized by BSON
            {
                return new Persistent(entry.Payload, entry.SequenceNr, entry.PersistenceId, entry.Manifest,
                    entry.IsDeleted, sender, timestamp: ToTicks(entry.Ordering));
            }

        }

        private async Task SetHighSequenceId(string persistenceId, long maxSeqNo)
        {
            var builder = Builders<MetadataEntry>.Filter;
            var filter = builder.Eq(x => x.PersistenceId, persistenceId);

            var metadataEntry = new MetadataEntry
            {
                Id = persistenceId,
                PersistenceId = persistenceId,
                SequenceNr = maxSeqNo
            };

            await _metadataCollection.Value.ReplaceOneAsync(filter, metadataEntry, new ReplaceOptions() { IsUpsert = true });
        }

        protected override bool ReceivePluginInternal(object message)
        {
            switch (message)
            {
                case ReplayTaggedMessages replay:
                    ReplayTaggedMessagesAsync(replay)
                        .PipeTo(replay.ReplyTo, success: h => new RecoverySuccess(h), failure: e => new ReplayMessagesFailure(e));
                    return true;
                case ReplayAllEvents replay:
                    ReplayAllEventsAsync(replay)
                        .PipeTo(replay.ReplyTo, success: h => new EventReplaySuccess(h),
                            failure: e => new EventReplayFailure(e));
                    return true;
                case SubscribePersistenceId subscribe:
                    AddPersistenceIdSubscriber(Sender, subscribe.PersistenceId);
                    Context.Watch(Sender);
                    return true;
                case SelectCurrentPersistenceIds request:
                    SelectAllPersistenceIdsAsync(request.Offset)
                        .PipeTo(request.ReplyTo, success: result => new CurrentPersistenceIds(result.Ids, request.Offset));
                    return true;
                case SubscribeTag subscribe:
                    AddTagSubscriber(Sender, subscribe.Tag);
                    Context.Watch(Sender);
                    return true;
                case SubscribeNewEvents _:
                    AddNewEventsSubscriber(Sender);
                    Context.Watch(Sender);
                    return true;
                case Terminated terminated:
                    RemoveSubscriber(terminated.ActorRef);
                    return true;
                default:
                    return false;
            }
        }
        private void AddNewEventsSubscriber(IActorRef subscriber)
        {
            _newEventsSubscriber.Add(subscriber);
        }
        protected virtual async Task<(IEnumerable<string> Ids, long LastOrdering)> SelectAllPersistenceIdsAsync(long offset)
        {
            var ids = await GetAllPersistenceIds(offset);
            var lastOrdering = await GetHighestOrdering();
            return (ids, lastOrdering);
        }

        protected virtual async Task<long> ReplayAllEventsAsync(ReplayAllEvents replay)
        {
            var limitValue = replay.Max >= int.MaxValue ? int.MaxValue : (int)replay.Max;

            var fromSequenceNr = replay.FromOffset;
            var toSequenceNr = replay.ToOffset;
            var builder = Builders<JournalEntry>.Filter;

            var seqNoFilter = builder.Empty;
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

            var readFilter = builder.Empty;
            if (fromSequenceNr > 0)
                readFilter &= builder.Gt(x => x.Ordering, new BsonTimestamp(fromSequenceNr));
            if (toSequenceNr != long.MaxValue)
                readFilter &= builder.Lte(x => x.Ordering, new BsonTimestamp(toSeqNo));
            var sort = Builders<JournalEntry>.Sort.Ascending(x => x.Ordering);

            await _journalCollection.Value.Find(readFilter)
                .Sort(sort)
                .Limit(limitValue)
                .ForEachAsync(entry =>
                {
                    var persistent = ToPersistenceRepresentation(entry, ActorRefs.NoSender);
                    foreach (var adapted in AdaptFromJournal(persistent))
                    {
                        replay.ReplyTo.Tell(new ReplayedEvent(adapted, entry.Ordering.Value), ActorRefs.NoSender);
                    }
                });
            return maxOrderingId;
        }

        private void AddTagSubscriber(IActorRef subscriber, string tag)
        {
            if (!_tagSubscribers.TryGetValue(tag, out var subscriptions))
            {
                _tagSubscribers = _tagSubscribers.Add(tag, ImmutableHashSet.Create(subscriber));
            }
            else
            {
                _tagSubscribers = _tagSubscribers.SetItem(tag, subscriptions.Add(subscriber));
            }
        }

        private async Task<IEnumerable<string>> GetAllPersistenceIds(long offset)
        {
            var ids = await _journalCollection.Value
                .DistinctAsync(x => x.PersistenceId, entry => entry.Ordering > new BsonTimestamp(offset));
           
            var hashset = new List<string>();
            while (await ids.MoveNextAsync())
            {
                hashset.AddRange(ids.Current);
            }
            return hashset;
        }

        private async Task<long> GetHighestOrdering()
        {
            var max = await _journalCollection.Value.AsQueryable()
                    .Select(je => je.Ordering)
                    .MaxAsync();

            return max.Value;
        }
        
        private void AddPersistenceIdSubscriber(IActorRef subscriber, string persistenceId)
        {
            if (!_persistenceIdSubscribers.TryGetValue(persistenceId, out var subscriptions))
            {
                _persistenceIdSubscribers = _persistenceIdSubscribers.Add(persistenceId, ImmutableHashSet.Create(subscriber));
            }
            else
            {
                _persistenceIdSubscribers = _persistenceIdSubscribers.SetItem(persistenceId, subscriptions.Add(subscriber));
            }
        }

        private void RemoveSubscriber(IActorRef subscriber)
        {
            _persistenceIdSubscribers = _persistenceIdSubscribers.SetItems(_persistenceIdSubscribers
                .Where(kv => kv.Value.Contains(subscriber))
                .Select(kv => new KeyValuePair<string, IImmutableSet<IActorRef>>(kv.Key, kv.Value.Remove(subscriber))));

            _tagSubscribers = _tagSubscribers.SetItems(_tagSubscribers
                .Where(kv => kv.Value.Contains(subscriber))
                .Select(kv => new KeyValuePair<string, IImmutableSet<IActorRef>>(kv.Key, kv.Value.Remove(subscriber))));

            _newEventsSubscriber.Remove(subscriber);
        }
        
        /// <summary>
        /// TBD
        /// </summary>
        protected bool HasTagSubscribers => _tagSubscribers.Count != 0;

        /// <summary>
        /// TBD
        /// </summary>
        protected bool HasNewEventSubscribers => _newEventsSubscriber.Count != 0;
        /// <summary>
        /// TBD
        /// </summary>
        protected bool HasPersistenceIdSubscribers => _persistenceIdSubscribers.Count != 0;


        private void NotifyPersistenceIdChange(string persistenceId)
        {
            if (_persistenceIdSubscribers.TryGetValue(persistenceId, out var subscribers))
            {
                var changed = new EventAppended(persistenceId);
                foreach (var subscriber in subscribers)
                    subscriber.Tell(changed);
            }
        }

        private void NotifyTagChange(string tag)
        {
            if (_tagSubscribers.TryGetValue(tag, out var subscribers))
            {
                var changed = new TaggedEventAppended(tag);
                foreach (var subscriber in subscribers)
                    subscriber.Tell(changed);
            }
        }

    }


}
