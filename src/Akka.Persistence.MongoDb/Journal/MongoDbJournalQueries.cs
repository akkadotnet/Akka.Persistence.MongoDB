// -----------------------------------------------------------------------
//   <copyright file="MongoDbJournalQueries.cs" company="Petabridge, LLC">
//     Copyright (C) 2015-2023 .NET Petabridge, LLC
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

#nullable enable
namespace Akka.Persistence.MongoDb.Journal;

internal static class MongoDbJournalQueries
{
    public static IFindFluent<JournalEntry, JournalEntry> ReplayMessagesQuery(
        this IMongoCollection<JournalEntry> collection,
        IClientSessionHandle? session,
        string persistenceId,
        long fromSequenceNr,
        long toSequenceNr,
        int limit)
    {
        var builder = Builders<JournalEntry>.Filter;
        var filter = builder.Eq(x => x.PersistenceId, persistenceId);
        if (fromSequenceNr > 0)
            filter &= builder.Gte(x => x.SequenceNr, fromSequenceNr);
        if (toSequenceNr != long.MaxValue)
            filter &= builder.Lte(x => x.SequenceNr, toSequenceNr);

        var sort = Builders<JournalEntry>.Sort.Ascending(x => x.SequenceNr);
            
        return (session is not null ? collection.Find(session, filter) : collection.Find(filter))
            .Sort(sort)
            .Limit(limit);
    }

    public static IFindFluent<JournalEntry, BsonTimestamp> MaxOrderingIdQuery(
        this IMongoCollection<JournalEntry> collection,
        IClientSessionHandle? session,
        long fromSequenceNr,
        long toSequenceNr,
        string? tag)
    {
        var builder = Builders<JournalEntry>.Filter;

        var filter = FilterDefinition<JournalEntry>.Empty;
        if(!string.IsNullOrWhiteSpace(tag))
            filter &= builder.AnyEq(x => x.Tags, tag);
        if (fromSequenceNr > 0)
            filter &= builder.Gt(x => x.Ordering, new BsonTimestamp(fromSequenceNr));
        if (toSequenceNr != long.MaxValue)
            filter &= builder.Lte(x => x.Ordering, new BsonTimestamp(toSequenceNr));

        return (session is not null ? collection.Find(session, filter) : collection.Find(filter))
            .SortByDescending(x => x.Ordering)
            .Limit(1)
            .Project(e => e.Ordering);
    }

    public static IFindFluent<JournalEntry, JournalEntry> MessagesQuery(
            this IMongoCollection<JournalEntry> collection,
            IClientSessionHandle? session,
            long fromSequenceNr,
            long toSequenceNr,
            long maxOrderingId,
            string? tag,
            int limit
        )
    {
        var builder = Builders<JournalEntry>.Filter;
        var toSeqNo = Math.Min(toSequenceNr, maxOrderingId);

        var filter = FilterDefinition<JournalEntry>.Empty;
        if(!string.IsNullOrWhiteSpace(tag))
            filter &= builder.AnyEq(x => x.Tags, tag);
        if (fromSequenceNr > 0)
            filter &= builder.Gt(x => x.Ordering, new BsonTimestamp(fromSequenceNr));
        if (toSequenceNr != long.MaxValue)
            filter &= builder.Lte(x => x.Ordering, new BsonTimestamp(toSeqNo));
        
        var sort = Builders<JournalEntry>.Sort.Ascending(x => x.Ordering);

        return (session is not null ? collection.Find(session, filter) : collection.Find(filter))
            .Sort(sort)
            .Limit(limit);
    }

    public static IFindFluent<MetadataEntry, long> MaxSequenceNrQuery(
        this IMongoCollection<MetadataEntry> collection,
        IClientSessionHandle? session,
        string persistenceId)
    {
        var filter = Builders<MetadataEntry>.Filter.Eq(x => x.PersistenceId, persistenceId);
        
        return (session is not null ? collection.Find(session, filter) : collection.Find(filter))
            .Project(x => x.SequenceNr);
    }
    
    public static IFindFluent<JournalEntry, long> MaxSequenceNrQuery(
        this IMongoCollection<JournalEntry> collection,
        IClientSessionHandle? session,
        string persistenceId)
    {
        var filter = Builders<JournalEntry>.Filter.Eq(x => x.PersistenceId, persistenceId);
        
        return (session is not null ? collection.Find(session, filter) : collection.Find(filter))
            .SortByDescending(x => x.SequenceNr)
            .Project(x => x.SequenceNr);
    }

    public static async Task<IEnumerable<string>> AllPersistenceIdsQuery(this IMongoCollection<JournalEntry> collection, IClientSessionHandle? session, long offset, CancellationToken token)
    {
        IAsyncCursor<string> idCursor;
        if (session is not null)
        {
            idCursor = await collection
                .DistinctAsync(
                    session: session,
                    field: x => x.PersistenceId, 
                    filter: entry => entry.Ordering > new BsonTimestamp(offset),
                    cancellationToken: token);
        }
        else
        {
            idCursor = await collection
                .DistinctAsync(
                    field: x => x.PersistenceId, 
                    filter: entry => entry.Ordering > new BsonTimestamp(offset),
                    cancellationToken: token);
        }
        var hashset = new List<string>();
        while (await idCursor.MoveNextAsync(token))
        {
            hashset.AddRange(idCursor.Current);
        }

        return hashset;
    }

    public static async Task<long> HighestOrderingQuery(this IMongoCollection<JournalEntry> collection, IClientSessionHandle? session, CancellationToken token)
    {
        var max = await (session is not null ? collection.AsQueryable(session) : collection.AsQueryable())
            .Select(je => je.Ordering)
            .MaxAsync(token);

        return max.Value;
    }
    
    public static async Task SetHighSequenceIdQuery(this IMongoCollection<MetadataEntry> collection, IClientSessionHandle? session, string persistenceId, long maxSeqNo, CancellationToken token)
    {
        var builder = Builders<MetadataEntry>.Filter;
        var filter = builder.Eq(x => x.PersistenceId, persistenceId);

        var metadataEntry = new MetadataEntry
        {
            Id = persistenceId,
            PersistenceId = persistenceId,
            SequenceNr = maxSeqNo
        };
            
        if (session is not null)
        {
            await collection.ReplaceOneAsync(
                session: session,
                filter: filter,
                replacement: metadataEntry,
                options: new ReplaceOptions { IsUpsert = true }, 
                cancellationToken: token);
        }
        else
        {
            await collection.ReplaceOneAsync(
                filter: filter,
                replacement: metadataEntry,
                options: new ReplaceOptions { IsUpsert = true }, 
                cancellationToken: token);
        }
    }

}