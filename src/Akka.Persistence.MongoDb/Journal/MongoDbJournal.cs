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
        private readonly MongoDbJournalSettings _settings;

        private ConcurrentDictionary<string, DbConfig> _dbConfigCache;
        private IMongoDbJournalConnectionStringBuilder _connStringBuilder;
        private DbConfig _defaultDbConfig;

        private class DbConfig
        {
            public Lazy<IMongoDatabase> MongoDatabase;
            public Lazy<IMongoCollection<JournalEntry>> JournalCollection;
            public Lazy<IMongoCollection<MetadataEntry>> MetadataCollection;
        }

        public MongoDbJournal()
        {
            _settings = MongoDbPersistence.Get(Context.System).JournalSettings;
        }

        protected override void PreStart()
        {
            base.PreStart();

            if (String.IsNullOrWhiteSpace(_settings.ConnectionStringBuilder)) {
                _defaultDbConfig = MakeDbConfig(_settings.ConnectionString); 
            }
            else {
                _dbConfigCache = new ConcurrentDictionary<string, DbConfig>();
                _connStringBuilder = (IMongoDbJournalConnectionStringBuilder)Activator.CreateInstance(Type.GetType(_settings.ConnectionStringBuilder));
                _connStringBuilder.Init(_settings);
            }
        }

        private DbConfig GetDbConfig(string persistenceId)
        {
            DbConfig dbConfig;
            if (_connStringBuilder != null) {
                var connectionString = _connStringBuilder.GetConnectionString(persistenceId);
                dbConfig = _dbConfigCache.GetOrAdd(connectionString, cs => MakeDbConfig(cs));
            }
            else {
                dbConfig = _defaultDbConfig;
            }
            return dbConfig;
        }

        private DbConfig MakeDbConfig(string connection)
        {
            var config = new DbConfig {
                MongoDatabase = new Lazy<IMongoDatabase>(() => {
                    var connectionString = new MongoUrl(connection);
                    var client = new MongoClient(connectionString);

                    return client.GetDatabase(connectionString.DatabaseName);
                })
            };

            config.JournalCollection = new Lazy<IMongoCollection<JournalEntry>>(() => {
                var collection = config.MongoDatabase.Value.GetCollection<JournalEntry>(_settings.Collection);

                if (_settings.AutoInitialize) {
                    collection.Indexes.CreateOneAsync(
                        Builders<JournalEntry>.IndexKeys
                            .Ascending(entry => entry.PersistenceId)
                            .Descending(entry => entry.SequenceNr))
                            .Wait();
                }

                return collection;
            });

            config.MetadataCollection = new Lazy<IMongoCollection<MetadataEntry>>(() => {
                var collection = config.MongoDatabase.Value.GetCollection<MetadataEntry>(_settings.MetadataCollection);

                if (_settings.AutoInitialize) {
                    collection.Indexes.CreateOneAsync(
                        Builders<MetadataEntry>.IndexKeys
                            .Ascending(entry => entry.PersistenceId))
                            .Wait();
                }

                return collection;
            });
            return config;
        }

        public override async Task ReplayMessagesAsync(IActorContext context, string persistenceId, long fromSequenceNr, long toSequenceNr, long max, Action<IPersistentRepresentation> recoveryCallback)
        {
            DbConfig dbConfig = GetDbConfig(persistenceId);

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

            var collections = await dbConfig.JournalCollection.Value
                .Find(filter)
                .Sort(sort)
                .Limit(limitValue)
                .ToListAsync();

            collections.ForEach(doc => {
                recoveryCallback(ToPersistenceRepresentation(doc, context.Sender));
            });
        }

        public override async Task<long> ReadHighestSequenceNrAsync(string persistenceId, long fromSequenceNr)
        {
            DbConfig dbConfig = GetDbConfig(persistenceId);

            var builder = Builders<MetadataEntry>.Filter;
            var filter = builder.Eq(x => x.PersistenceId, persistenceId);

            var highestSequenceNr = await dbConfig.MetadataCollection.Value.Find(filter).Project(x => x.SequenceNr).FirstOrDefaultAsync();

            return highestSequenceNr;
        }

        protected override async Task<IImmutableList<Exception>> WriteMessagesAsync(IEnumerable<AtomicWrite> messages)
        {
            var messageList = messages.ToList();
            var writeTasks = messageList.Select(async message =>
            {
                DbConfig dbConfig = GetDbConfig(message.PersistenceId);

                var persistentMessages = ((IImmutableList<IPersistentRepresentation>)message.Payload).ToArray();

                var journalEntries = persistentMessages.Select(ToJournalEntry).ToList();
                await dbConfig.JournalCollection.Value.InsertManyAsync(journalEntries);
            });

            await SetHighSequenceId(messageList);

            return await Task<IImmutableList<Exception>>
                .Factory
                .ContinueWhenAll(writeTasks.ToArray(),
                    tasks => tasks.Select(t => t.IsFaulted ? TryUnwrapException(t.Exception) : null).ToImmutableList());
        }

        protected override Task DeleteMessagesToAsync(string persistenceId, long toSequenceNr)
        {
            DbConfig dbConfig = GetDbConfig(persistenceId);

            var builder = Builders<JournalEntry>.Filter;
            var filter = builder.Eq(x => x.PersistenceId, persistenceId);

            if (toSequenceNr != long.MaxValue)
                filter &= builder.Lte(x => x.SequenceNr, toSequenceNr);

            return dbConfig.JournalCollection.Value.DeleteManyAsync(filter);
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

        private Persistent ToPersistenceRepresentation(JournalEntry entry, IActorRef sender)
        {
            return new Persistent(entry.Payload, entry.SequenceNr, entry.PersistenceId, entry.Manifest, entry.IsDeleted, sender);
        }

        private async Task SetHighSequenceId(IList<AtomicWrite> messages)
        {
            var messageGroups = messages
                .GroupBy(m => GetDbConfig(m.PersistenceId))
                .ToList();

            foreach (var group in messageGroups) {
                var dbConfig = group.Key;

                var persistenceId = messages.Select(c => c.PersistenceId).First();
                var highSequenceId = messages.Max(c => c.HighestSequenceNr);
                var builder = Builders<MetadataEntry>.Filter;
                var filter = builder.Eq(x => x.PersistenceId, persistenceId);

                var metadataEntry = new MetadataEntry {
                    Id = persistenceId,
                    PersistenceId = persistenceId,
                    SequenceNr = highSequenceId
                };

                await dbConfig.MetadataCollection.Value.ReplaceOneAsync(filter, metadataEntry, new UpdateOptions() { IsUpsert = true });
            };
        }
    }
}
