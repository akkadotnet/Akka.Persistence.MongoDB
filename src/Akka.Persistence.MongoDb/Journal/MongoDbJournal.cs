//-----------------------------------------------------------------------
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

#nullable enable
namespace Akka.Persistence.MongoDb.Journal
{
    /// <summary>
    /// An Akka.NET journal implementation that writes events asynchronously to MongoDB.
    /// </summary>
    public class MongoDbJournal : AsyncWriteJournal
    {
        private static readonly BsonTimestamp ZeroTimestamp = new(0);
        private static readonly ClientSessionOptions EmptySessionOptions = new();

        private readonly MongoDbJournalSettings _settings;
        // ReSharper disable InconsistentNaming
        private IMongoDatabase? _mongoDatabase_DoNotUseDirectly;
        private IMongoCollection<JournalEntry>? _journalCollection_DoNotUseDirectly;
        private IMongoCollection<MetadataEntry>? _metadataCollection_DoNotUseDirectly;
        // ReSharper enable InconsistentNaming

        /// <summary>
        /// Used to cancel all outstanding commands when the actor is stopped.
        /// </summary>
        private readonly CancellationTokenSource _pendingCommandsCancellation = new();

        private readonly Akka.Serialization.Serialization _serialization;

        public MongoDbJournal() : this(MongoDbPersistence.Get(Context.System).JournalSettings)
        {
        }

        // This constructor is needed because config can come from both Akka.Persistence and Akka.Cluster.Sharding
        public MongoDbJournal(Config config) : this(new MongoDbJournalSettings(config))
        {
        }

        private MongoDbJournal(MongoDbJournalSettings settings)
        {
            _settings = settings;
            _serialization = Context.System.Serialization;
        }

        private IMongoDatabase GetMongoDb()
        {
            if (_mongoDatabase_DoNotUseDirectly is not null)
                return _mongoDatabase_DoNotUseDirectly;
            
            MongoClient client;
            var setupOption = Context.System.Settings.Setup.Get<MongoDbPersistenceSetup>();
            if (setupOption.HasValue && setupOption.Value.JournalConnectionSettings != null)
            {
                client = new MongoClient(setupOption.Value.JournalConnectionSettings);
                _mongoDatabase_DoNotUseDirectly = client.GetDatabase(setupOption.Value.JournalDatabaseName);
                return _mongoDatabase_DoNotUseDirectly;
            }

            //Default LinqProvider has been changed to LINQ3.LinqProvider can be changed back to LINQ2 in the following way:
            var connectionString = new MongoUrl(_settings.ConnectionString);
            var clientSettings = MongoClientSettings.FromUrl(connectionString);
            clientSettings.LinqProvider = LinqProvider.V2;
            
            client = new MongoClient(clientSettings);
            _mongoDatabase_DoNotUseDirectly = client.GetDatabase(connectionString.DatabaseName);
            return _mongoDatabase_DoNotUseDirectly;
        }
        
        private async Task<IMongoCollection<JournalEntry>> GetJournalCollection(CancellationToken token)
        {
            if (_journalCollection_DoNotUseDirectly is not null)
                return _journalCollection_DoNotUseDirectly;
            
            _journalCollection_DoNotUseDirectly = GetMongoDb().GetCollection<JournalEntry>(_settings.Collection);
            
            if (!_settings.AutoInitialize) 
                return _journalCollection_DoNotUseDirectly;
                
            var modelForEntryAndSequenceNr = new CreateIndexModel<JournalEntry>(Builders<JournalEntry>
                .IndexKeys
                .Ascending(entry => entry.PersistenceId)
                .Descending(entry => entry.SequenceNr));

            await _journalCollection_DoNotUseDirectly.Indexes
                .CreateOneAsync(modelForEntryAndSequenceNr, cancellationToken: token);

            var modelWithOrdering = new CreateIndexModel<JournalEntry>(
                Builders<JournalEntry>
                    .IndexKeys
                    .Ascending(entry => entry.Ordering));

            await _journalCollection_DoNotUseDirectly.Indexes
                .CreateOneAsync(modelWithOrdering, cancellationToken: token);

            await _journalCollection_DoNotUseDirectly.Indexes
                .CreateOneAsync(modelWithOrdering, cancellationToken: token);

            var tagsWithOrdering = new CreateIndexModel<JournalEntry>(
                Builders<JournalEntry>
                    .IndexKeys
                    .Ascending(entry => entry.Tags)
                    .Ascending(entry => entry.Ordering));

            await _journalCollection_DoNotUseDirectly.Indexes
                .CreateOneAsync(tagsWithOrdering, cancellationToken:token);

            return _journalCollection_DoNotUseDirectly;
        }

        private async Task<IMongoCollection<MetadataEntry>> GetMetadataCollection(CancellationToken token)
        {
            if (_metadataCollection_DoNotUseDirectly is not null)
                return _metadataCollection_DoNotUseDirectly;
            
            _metadataCollection_DoNotUseDirectly = GetMongoDb()
                .GetCollection<MetadataEntry>(_settings.MetadataCollection);
            
            if (!_settings.AutoInitialize) 
                return _metadataCollection_DoNotUseDirectly;
                
            var modelWithAscendingPersistenceId = new CreateIndexModel<MetadataEntry>(
                Builders<MetadataEntry>
                    .IndexKeys
                    .Ascending(entry => entry.PersistenceId));

            await _metadataCollection_DoNotUseDirectly.Indexes
                .CreateOneAsync(modelWithAscendingPersistenceId, cancellationToken: token);
                
            return _metadataCollection_DoNotUseDirectly;
        }
        
        private CancellationTokenSource CreatePerCallCts()
        {
            var unitedCts = CancellationTokenSource.CreateLinkedTokenSource(_pendingCommandsCancellation.Token);
            unitedCts.CancelAfter(_settings.CallTimeout);
            return unitedCts;
        }

        private async Task MaybeWithTransaction(Func<IClientSessionHandle?, CancellationToken, Task> act, CancellationToken token)
        {
            if (!_settings.Transaction)
            {
                await act(null, token);
                return;
            }
            
            using var session = await GetMongoDb().Client.StartSessionAsync(EmptySessionOptions, token);
            await session.WithTransactionAsync(
                async (s, ct) =>
                {
                    await act(s, ct);
                    return Task.FromResult(NotUsed.Instance);
                }, cancellationToken:token);
        }
        
        private async Task<T> MaybeWithTransaction<T>(Func<IClientSessionHandle?, CancellationToken, Task<T>> act, CancellationToken token)
        {
            if (!_settings.Transaction) 
                return await act(null, token);
            
            using var session = await GetMongoDb().Client.StartSessionAsync(EmptySessionOptions, token);
            return await session.WithTransactionAsync(act, cancellationToken:token);
        }

        public override async Task ReplayMessagesAsync(
            IActorContext context,
            string persistenceId,
            long fromSequenceNr,
            long toSequenceNr,
            long max,
            Action<IPersistentRepresentation> recoveryCallback)
        {
            // Limit allows only integer
            var limitValue = max >= int.MaxValue ? int.MaxValue : (int)max;

            // Do not replay messages if limit equal zero
            if (limitValue == 0)
                return;
            
            using var unitedCts = CreatePerCallCts();
            var journalCollection = await GetJournalCollection(unitedCts.Token);

            await MaybeWithTransaction(async (session, ct) =>
            {
                var collections = await journalCollection
                    .ReplayMessagesQuery(session, persistenceId, fromSequenceNr, toSequenceNr, limitValue)
                    .ToListAsync(ct);
                
                collections.ForEach(doc => recoveryCallback(ToPersistenceRepresentation(doc, context.Sender)));
            }, unitedCts.Token);
        }

        /// <summary>
        /// Replays all events with given tag withing provided boundaries from current database.
        /// </summary>
        /// <param name="replay">TBD</param>
        /// <returns>TBD</returns>
        private async Task<long> ReplayTaggedMessagesAsync(ReplayTaggedMessages replay)
        {
            using var unitedCts = CreatePerCallCts();
            var journalCollection = await GetJournalCollection(unitedCts.Token);

            return await MaybeWithTransaction(async (s, ct) =>
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

                // Need to know what the highest seqNo of this query will be
                // and return that as part of the RecoverySuccess message
                var maxSeqNoEntry = await journalCollection.MaxOrderingIdQuery(s, fromSequenceNr, toSequenceNr, tag)
                    .SingleOrDefaultAsync(ct);

                if (maxSeqNoEntry == null)
                    return 0L; // recovered nothing

                var maxOrderingId = maxSeqNoEntry.Value;

                await journalCollection.MessagesQuery(s, fromSequenceNr, toSequenceNr, maxOrderingId, tag, limitValue)
                    .ForEachAsync(entry =>
                    {
                        var persistent = ToPersistenceRepresentation(entry, ActorRefs.NoSender);
                        foreach (var adapted in AdaptFromJournal(persistent))
                            replay.ReplyTo.Tell(new ReplayedTaggedMessage(adapted, tag, entry.Ordering.Value),
                                ActorRefs.NoSender);
                    }, ct);
                
                return maxOrderingId;
            }, unitedCts.Token);
        }

        /// <summary>
        /// Asynchronously reads a highest sequence number of the event stream related with provided <paramref name="persistenceId"/>.
        /// </summary>
        /// <param name="persistenceId">TBD</param>
        /// <param name="fromSequenceNr">TBD</param>
        /// <returns>long</returns>
        public override async Task<long> ReadHighestSequenceNrAsync(string persistenceId, long fromSequenceNr)
        {
            using var unitedCts = CreatePerCallCts();
            var token = unitedCts.Token;

            return await MaybeWithTransaction(
                async (s, ct) => await ReadHighestSequenceNrOperation(s, persistenceId, ct), 
                token);
        }

        /// <summary>
        /// NOTE: This method is meant to be a part of a persistence operation
        /// The session parameter signals if this query is being called as part of a transaction block or not
        /// NEVER CALL THIS METHOD OUTSIDE OF MaybeWithTransaction BLOCK
        /// </summary>
        private async Task<long> ReadHighestSequenceNrOperation(
            IClientSessionHandle? session,
            string persistenceId,
            CancellationToken token)
        {
            var journalCollection = await GetJournalCollection(token);
            var metadataCollection = await GetMetadataCollection(token);
            
            var metadataHighestSequenceNrTask = metadataCollection
                .MaxSequenceNrQuery(session, persistenceId)
                .FirstOrDefaultAsync(token);

            var journalHighestSequenceNrTask = journalCollection
                .MaxSequenceNrQuery(session, persistenceId)
                .FirstOrDefaultAsync(token);

            // journal data is usually good enough, except in cases when it's been deleted.
            await Task.WhenAll(metadataHighestSequenceNrTask, journalHighestSequenceNrTask);

            return Math.Max(journalHighestSequenceNrTask.Result, metadataHighestSequenceNrTask.Result);
        }
        
        protected override async Task<IImmutableList<Exception?>> WriteMessagesAsync(IEnumerable<AtomicWrite> messages)
        {
            using var unitedCts = CreatePerCallCts();
            var journalCollection = await GetJournalCollection(unitedCts.Token);
            
            var writeTasks = messages.Select(async message =>
            {
                var persistentMessages = (IImmutableList<IPersistentRepresentation>)message.Payload;

                var journalEntries = persistentMessages.Select(ToJournalEntry);
                await InsertEntries(journalCollection, journalEntries, unitedCts.Token);
            }).ToArray();

            var result = await Task<IImmutableList<Exception?>>
                .Factory
                .ContinueWhenAll(
                    tasks: writeTasks.ToArray(),
                    continuationFunction: tasks => tasks.Select(t => t.IsFaulted ? TryUnwrapException(t.Exception) : null).ToImmutableList(), 
                    cancellationToken: unitedCts.Token);
            
            return result;
        }

        private async ValueTask InsertEntries(IMongoCollection<JournalEntry> collection, IEnumerable<JournalEntry> entries, CancellationToken token)
        {
            await MaybeWithTransaction(async (session, ct) =>
            {
                //https://www.mongodb.com/community/forums/t/insertone-vs-insertmany-is-one-preferred-over-the-other/135982/2
                //https://www.mongodb.com/docs/manual/core/transactions-production-consideration/#runtime-limit
                //https://www.mongodb.com/docs/manual/core/transactions-production-consideration/#oplog-size-limit
                //https://www.mongodb.com/docs/manual/reference/limits/#mongodb-limits-and-thresholds
                //16MB: if is bigger than this that means you do it one by one. LET'S TALK ABOUT THIS
                if(session is not null)
                    await collection
                        .InsertManyAsync(session, entries, new InsertManyOptions { IsOrdered = true }, cancellationToken: ct);
                else
                    await collection
                        .InsertManyAsync(entries, new InsertManyOptions { IsOrdered = true }, cancellationToken: ct);
            }, token);
        }

        protected override async Task DeleteMessagesToAsync(string persistenceId, long toSequenceNr)
        {
            using var unitedCts = CreatePerCallCts();
            var journalCollection = await GetJournalCollection(unitedCts.Token);
            var metadataCollection = await GetMetadataCollection(unitedCts.Token);

            await MaybeWithTransaction(async (session, token) =>
            {
                var builder = Builders<JournalEntry>.Filter;
                var filter = builder.Eq(x => x.PersistenceId, persistenceId);

                // read highest sequence number before we start
                var highestSeqNo = await ReadHighestSequenceNrOperation(session, persistenceId, token);
                
                if (toSequenceNr != long.MaxValue)
                    filter &= builder.Lte(x => x.SequenceNr, toSequenceNr);
                
                // only update the sequence number of the top of the journal
                // is about to be deleted.
                if (highestSeqNo <= toSequenceNr)
                {
                    await metadataCollection.SetHighSequenceIdQuery(session, persistenceId, highestSeqNo, token);
                }
                
                if(session is not null)
                    await journalCollection.DeleteManyAsync(session, filter, cancellationToken: token);
                else
                    await journalCollection.DeleteManyAsync(filter, cancellationToken: token);
            }, unitedCts.Token);
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
                var manifest = string.IsNullOrEmpty(message.Manifest)
                    ? payload.GetType().TypeQualifiedName()
                    : message.Manifest;
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

        private static long ToTicks(BsonTimestamp? bson)
        {
            // BSON Timestamps are stored natively as Unix epoch seconds + an ordinal value

            // need to use BsonTimestamp.Timestamp because the ordinal value doesn't actually have any
            // bearing on the time - it's used to try to somewhat order the events that all occurred concurrently
            // according to the MongoDb clock. No need to include that data in the EventEnvelope.Timestamp field
            // which is used entirely for end-user purposes.
            //
            // See https://docs.mongodb.com/manual/reference/bson-types/#timestamps
            bson ??= ZeroTimestamp;
            return DateTimeOffset.FromUnixTimeSeconds(bson.Timestamp).Ticks;
        }

        private Persistent ToPersistenceRepresentation(JournalEntry entry, IActorRef sender)
        {
            if (_settings.LegacySerialization)
            {
                var manifest = string.IsNullOrEmpty(entry.Manifest)
                    ? entry.Payload.GetType().TypeQualifiedName()
                    : entry.Manifest;

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
            Type? type = null;

            // legacy serialization
            if (!entry.SerializerId.HasValue && !string.IsNullOrEmpty(entry.Manifest))
                type = Type.GetType(entry.Manifest, true);
            else
                serializerId = entry.SerializerId;

            if (entry.Payload is byte[] bytes)
            {
                object? deserialized;
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

                return new Persistent(deserialized, entry.SequenceNr, entry.PersistenceId, entry.Manifest,
                    entry.IsDeleted, sender, timestamp: ToTicks(entry.Ordering));
            }

            // backwards compat for object serialization - Payload was already deserialized by BSON
            return new Persistent(entry.Payload, entry.SequenceNr, entry.PersistenceId, entry.Manifest,
                entry.IsDeleted, sender, timestamp: ToTicks(entry.Ordering));
        }

        protected override bool ReceivePluginInternal(object message)
        {
            switch (message)
            {
                case ReplayTaggedMessages replay:
                    ReplayTaggedMessagesAsync(replay)
                        .PipeTo(replay.ReplyTo, success: h => new RecoverySuccess(h),
                            failure: e => new ReplayMessagesFailure(e));
                    return true;
                case ReplayAllEvents replay:
                    ReplayAllEventsAsync(replay)
                        .PipeTo(replay.ReplyTo, success: h => new EventReplaySuccess(h),
                            failure: e => new EventReplayFailure(e));
                    return true;
                case SelectCurrentPersistenceIds request:
                    SelectAllPersistenceIdsAsync(request.Offset)
                        .PipeTo(request.ReplyTo,
                            success: result => new CurrentPersistenceIds(result.Ids, request.Offset));
                    return true;
                default:
                    return false;
            }
        }

        protected virtual async Task<(IEnumerable<string> Ids, long LastOrdering)> SelectAllPersistenceIdsAsync(
            long offset)
        {
            using var unitedCts = CreatePerCallCts();
            var journalCollection = await GetJournalCollection(unitedCts.Token);

            return await MaybeWithTransaction(async (session, token) =>
            {
                var ids = await journalCollection.AllPersistenceIdsQuery(session, offset, token);
                var lastOrdering = await journalCollection.HighestOrderingQuery(session, token);
                return (ids, lastOrdering);
            }, unitedCts.Token);
        }

        protected virtual async Task<long> ReplayAllEventsAsync(ReplayAllEvents replay)
        {
            using var unitedCts = CreatePerCallCts();
            var journalCollection = await GetJournalCollection(unitedCts.Token);

            return await MaybeWithTransaction(async (session, token) =>
            {
                var limitValue = replay.Max >= int.MaxValue ? int.MaxValue : (int)replay.Max;

                var fromSequenceNr = replay.FromOffset;
                var toSequenceNr = replay.ToOffset;

                // Need to know what the highest seqNo of this query will be
                // and return that as part of the RecoverySuccess message
                var maxSeqNoEntry = await journalCollection.MaxOrderingIdQuery(session, fromSequenceNr, toSequenceNr, null)
                    .SingleOrDefaultAsync(token);

                if (maxSeqNoEntry == null)
                    return 0L; // recovered nothing
                        
                var maxOrderingId = maxSeqNoEntry.Value;

                await journalCollection.MessagesQuery(session, fromSequenceNr, toSequenceNr, maxOrderingId, null, limitValue)
                    .ForEachAsync(entry =>
                    {
                        var persistent = ToPersistenceRepresentation(entry, ActorRefs.NoSender);
                        foreach (var adapted in AdaptFromJournal(persistent))
                        {
                            replay.ReplyTo.Tell(new ReplayedEvent(adapted, entry.Ordering.Value), ActorRefs.NoSender);
                        }
                    }, token);
                        
                return maxOrderingId;
            }, unitedCts.Token);
        }

        protected override void PostStop()
        {
            // cancel any / all pending operations
            _pendingCommandsCancellation.Cancel();
            _pendingCommandsCancellation.Dispose();
            base.PostStop();
        }
    }
}