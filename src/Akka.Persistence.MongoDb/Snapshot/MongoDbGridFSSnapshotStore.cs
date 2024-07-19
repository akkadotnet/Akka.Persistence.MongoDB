//-----------------------------------------------------------------------
// <copyright file="MongoDbSnapshotStore.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Event;
using Akka.Persistence.Snapshot;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using MongoDB.Driver.Linq;

#nullable enable
namespace Akka.Persistence.MongoDb.Snapshot;

/// <summary>
/// A large snapshot SnapshotStore implementation for writing snapshots to MongoDB.
/// Implements MongoDB GridFS storage mechanisms to support snapshots larger than 16 megabytes
/// </summary>
// ReSharper disable once InconsistentNaming
public class MongoDbGridFSSnapshotStore : SnapshotStore
{
    private const string PersistenceIdKey = "_pid";
    private const string SequenceNrKey = "_snr";
    private const string TimestampKey = "_ts";
    
    private static readonly ClientSessionOptions EmptySessionOptions = new();
        
    private readonly MongoDbSnapshotSettings _settings;
    private readonly GridFSBucketOptions _bucketOptions;
    // ReSharper disable InconsistentNaming
    private IMongoDatabase? _mongoDatabase_DoNotUseDirectly;
    private GridFSBucket? _snapshotGridFSBucket_DoNotUseDirectly;
    // ReSharper enable InconsistentNaming

    /// <summary>
    /// Used to cancel all outstanding commands when the actor is stopped.
    /// </summary>
    private readonly CancellationTokenSource _pendingCommandsCancellation = new();

    private readonly Akka.Serialization.Serialization _serialization;
    private readonly ILoggingAdapter _log;

    public MongoDbGridFSSnapshotStore() : this(MongoDbPersistence.Get(Context.System).SnapshotStoreSettings)
    {
    }

    public MongoDbGridFSSnapshotStore(Config config) : this(new MongoDbSnapshotSettings(config))
    {
    }

    public MongoDbGridFSSnapshotStore(MongoDbSnapshotSettings settings)
    {
        _settings = settings;
        _serialization = Context.System.Serialization;
        _bucketOptions = new GridFSBucketOptions
        {
            //ReadConcern = settings.Transaction ? ReadConcern.Snapshot : ReadConcern.Default,
            //WriteConcern = WriteConcern.WMajority, 
            BucketName = settings.Collection, 
            // ChunkSizeBytes = 1024 * 1024
        };
        _log = Context.GetLogger();
    }

    private CancellationTokenSource CreatePerCallCts()
    {
        var unitedCts =
            CancellationTokenSource.CreateLinkedTokenSource(_pendingCommandsCancellation.Token);
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
        
    private async Task<T?> MaybeWithTransaction<T>(Func<IClientSessionHandle?, CancellationToken, Task<T?>> act, CancellationToken token)
    {
        if (!_settings.Transaction) 
            return await act(null, token);
            
        using var session = await GetMongoDb().Client.StartSessionAsync(EmptySessionOptions, token);
        return await session.WithTransactionAsync(act, cancellationToken:token);
    }
        
    private IMongoDatabase GetMongoDb()
    {
        if (_mongoDatabase_DoNotUseDirectly is not null)
            return _mongoDatabase_DoNotUseDirectly;
        
        MongoClient client;
        var setupOption = Context.System.Settings.Setup.Get<MongoDbPersistenceSetup>();
        if (!setupOption.HasValue || setupOption.Value.SnapshotConnectionSettings == null)
        {
            //Default LinqProvider has been changed to LINQ3.LinqProvider can be changed back to LINQ2 in the following way:
            var connectionString = new MongoUrl(_settings.ConnectionString);
            var clientSettings = MongoClientSettings.FromUrl(connectionString);
            clientSettings.LinqProvider = LinqProvider.V2;
            client = new MongoClient(clientSettings);
            _mongoDatabase_DoNotUseDirectly = client.GetDatabase(connectionString.DatabaseName);
            return _mongoDatabase_DoNotUseDirectly;
        }

        client = new MongoClient(setupOption.Value.SnapshotConnectionSettings);
        _mongoDatabase_DoNotUseDirectly = client.GetDatabase(setupOption.Value.SnapshotDatabaseName);
        return _mongoDatabase_DoNotUseDirectly;
    }

    private GridFSBucket GetGridFSBucket()
    {
        var db = GetMongoDb();
        _snapshotGridFSBucket_DoNotUseDirectly = new GridFSBucket(db, _bucketOptions);

        return _snapshotGridFSBucket_DoNotUseDirectly;
    }

    private IMongoCollection<GridFSFileInfo> GetFilesCollection()
    {
        return GetMongoDb().GetCollection<GridFSFileInfo>(_settings.Collection + ".files");
    }

    private IMongoCollection<BsonDocument> GetChunksCollection()
    {
        return GetMongoDb().GetCollection<BsonDocument>(_settings.Collection + ".chunks");
    }
    
    protected override void PostStop()
    {
        // cancel any pending database commands during shutdown
        _pendingCommandsCancellation.Cancel();
        _pendingCommandsCancellation.Dispose();
        base.PostStop();
    }

    protected override async Task<SelectedSnapshot?> LoadAsync(string persistenceId, SnapshotSelectionCriteria criteria)
    {
        using var unitedCts = CreatePerCallCts();
        var filesCollection = GetFilesCollection();
        var chunksCollection = GetChunksCollection();
        
        var filter = CreateRangeFilter(persistenceId, criteria);

        return await MaybeWithTransaction<SelectedSnapshot?>(async (session, token) =>
        {
            var info = await (session is not null 
                    ? filesCollection.Find(session, filter) 
                    : filesCollection.Find(filter)) 
                .SortByDescending(x => x.Metadata[SequenceNrKey])
                .Limit(1)
                .FirstOrDefaultAsync(token);
            
            if (info is null)
                return null;
            
            var chunkFilter = Builders<BsonDocument>.Filter.Eq(doc => doc["files_id"], info.Id);
            var chunkCursor = await (session is not null
                    ? chunksCollection.Find(session, chunkFilter)
                    : chunksCollection.Find(chunkFilter))
                .SortBy(x => x["n"])
                .ToCursorAsync(token);

            using var memoryStream = new MemoryStream();
            while (await chunkCursor.MoveNextAsync(token))
            {
                var chunks = chunkCursor.Current;
                foreach (var doc in chunks)
                {
                    var data = doc["data"].AsByteArray;
                    await memoryStream.WriteAsync(data, 0, data.Length, token);
                }
            }
            
            return ToSelectedSnapshot(info.Metadata, memoryStream.ToArray());
        }, unitedCts.Token);
    }

    protected override async Task SaveAsync(SnapshotMetadata metadata, object snapshot)
    {
        using var unitedCts = CreatePerCallCts();
        var bucket = GetGridFSBucket();
        var filesCollection = GetFilesCollection();
        
        var (fileName, option, bytes) = ToSnapshotFileMetadata(metadata, snapshot);

        await MaybeWithTransaction(async (session, token) =>
        {
            var filter = Builders<GridFSFileInfo>.Filter.Eq(i => i.Filename, fileName);
            await DeleteFileAsync(filter, filesCollection, session, token);
            await bucket.UploadFromBytesAsync(fileName, bytes, option, unitedCts.Token);
        }, unitedCts.Token);
        
    }

    protected override async Task DeleteAsync(SnapshotMetadata metadata)
    {
        using var unitedCts = CreatePerCallCts();

        var builder = Builders<GridFSFileInfo>.Filter;
        var filters = new List<FilterDefinition<GridFSFileInfo>>
        {
            builder.Eq(x => x.Metadata[PersistenceIdKey], metadata.PersistenceId)
        };

        if (metadata.SequenceNr is > 0 and < long.MaxValue)
            filters.Add(builder.Eq(x => x.Metadata[SequenceNrKey], metadata.SequenceNr)); 

        if (metadata.Timestamp != DateTime.MinValue && metadata.Timestamp != DateTime.MaxValue)
            filters.Add(builder.Eq(x => x.Metadata[TimestampKey], metadata.Timestamp.Ticks));

        var filter = builder.And(filters);

        await MaybeWithTransaction(async (session, token) =>
        {
            await DeleteFileAsync(filter, GetFilesCollection(), session, token);
        }, unitedCts.Token);
    }

    protected override async Task DeleteAsync(string persistenceId, SnapshotSelectionCriteria criteria)
    {
        using var unitedCts = CreatePerCallCts();
        var filesCollection = GetFilesCollection();
        var filter = CreateRangeFilter(persistenceId, criteria);

        await MaybeWithTransaction(async (session, token) =>
        {
            var infoCursor = await (session is not null
                ? filesCollection.Find(session, filter)
                : filesCollection.Find(filter)).ToCursorAsync(token);
            
            while (await infoCursor.MoveNextAsync(token))
            {
                await Task.WhenAll(infoCursor.Current.Select(info => DeleteFileAsync(info, filesCollection, session, token)));
            }
        }, unitedCts.Token);
    }

    private async Task DeleteFileAsync(
        GridFSFileInfo info,
        IMongoCollection<GridFSFileInfo> filesCollection,
        IClientSessionHandle? session,
        CancellationToken token)
    {
        var filesFilter = Builders<GridFSFileInfo>.Filter.Eq(i => i.Filename, info.Filename);
        await DeleteFileAsync(filesFilter, filesCollection, session, token);
    }

    private async Task DeleteFileAsync(
        FilterDefinition<GridFSFileInfo> filesFilter,
        IMongoCollection<GridFSFileInfo> filesCollection, 
        IClientSessionHandle? session,
        CancellationToken token)
    {
        var info = session is not null
            ? await filesCollection.FindOneAndDeleteAsync(session, filesFilter, cancellationToken: token)
            : await filesCollection.FindOneAndDeleteAsync(filesFilter, cancellationToken: token);
        
        if(info is null)
            return;
        
        var chunkFilter = Builders<BsonDocument>.Filter.Eq(doc => doc["files_id"], info.Id);
        var chunkCollection = GetChunksCollection();
        var result = session is not null 
            ? await chunkCollection.DeleteManyAsync(session, chunkFilter, cancellationToken: token)
            : await chunkCollection.DeleteManyAsync(chunkFilter, token);
        _log.Info($"Chunks deleted: {result.DeletedCount}");
    }

    private static FilterDefinition<GridFSFileInfo> CreateRangeFilter(string persistenceId, SnapshotSelectionCriteria criteria)
    {
        var builder = Builders<GridFSFileInfo>.Filter;
        var filters = new List<FilterDefinition<GridFSFileInfo>>
        {
            builder.Eq(x => x.Metadata[PersistenceIdKey], persistenceId)
        };
        
        if (criteria.MaxSequenceNr is > 0 and < long.MaxValue)
            filters.Add(builder.Lte(x => x.Metadata[SequenceNrKey], criteria.MaxSequenceNr)); 

        if (criteria.MaxTimeStamp != DateTime.MinValue && criteria.MaxTimeStamp != DateTime.MaxValue)
            filters.Add(builder.Lte(x => x.Metadata[TimestampKey], criteria.MaxTimeStamp.Ticks));

        return builder.And(filters);
    }

    private (string, GridFSUploadOptions, byte[]) ToSnapshotFileMetadata(SnapshotMetadata metadata, object snapshot)
    {
        var option = new GridFSUploadOptions
        {
            Metadata = new BsonDocument
            {
                [PersistenceIdKey] = metadata.PersistenceId,
                [SequenceNrKey] = metadata.SequenceNr,
                [TimestampKey] = metadata.Timestamp.Ticks,
            }
        };
        
        if (_settings.LegacySerialization)
        {
            var payload = new GridFsPayloadEnvelope
            {
                Payload = snapshot
            };
            return (metadata.PersistenceId + "_" + metadata.SequenceNr, option, payload.ToBson());
        }
        
        var snapshotRep = new Serialization.Snapshot(snapshot);
        var serializer = _serialization.FindSerializerFor(snapshotRep);
        var binary = serializer.ToBinary(snapshotRep);
        return (metadata.PersistenceId + "_" + metadata.SequenceNr, option, binary);
    }
    
    private SelectedSnapshot ToSelectedSnapshot(BsonDocument metadata, byte[] entrySnapshot)
    {
        if (_settings.LegacySerialization)
        {
            return new SelectedSnapshot(
                new SnapshotMetadata(
                    metadata[PersistenceIdKey].AsString,
                    metadata[SequenceNrKey].AsInt64,
                    new DateTime(metadata[TimestampKey].AsInt64)),
                BsonSerializer.Deserialize<GridFsPayloadEnvelope>(entrySnapshot).Payload);
        }

        var ser = _serialization.FindSerializerForType(typeof(Serialization.Snapshot));
        var snapshot = ser.FromBinary<Serialization.Snapshot>(entrySnapshot);
        return new SelectedSnapshot(
            new SnapshotMetadata(
                metadata[PersistenceIdKey].AsString, 
                metadata[SequenceNrKey].AsInt64,
                new DateTime(metadata[TimestampKey].AsInt64)), 
            snapshot.Data);
    }
}