//-----------------------------------------------------------------------
// <copyright file="MongoDbSnapshotStore.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Configuration;
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
public class MongoDbGridFsSnapshotStore : SnapshotStore
{
    private const string PersistenceIdKey = "_pid";
    private const string SequenceNrKey = "_snr";
    private const string TimestampKey = "_ts";
    
    private readonly MongoDbSnapshotSettings _settings;
    private readonly GridFSBucketOptions _bucketOptions;
    // ReSharper disable InconsistentNaming
    private IMongoDatabase? _mongoDatabase_DoNotUseDirectly;
    // ReSharper enable InconsistentNaming

    /// <summary>
    /// Used to cancel all outstanding commands when the actor is stopped.
    /// </summary>
    private readonly CancellationTokenSource _pendingCommandsCancellation = new();

    private readonly Akka.Serialization.Serialization _serialization;

    public MongoDbGridFsSnapshotStore() : this(MongoDbPersistence.Get(Context.System).SnapshotStoreSettings)
    {
    }

    public MongoDbGridFsSnapshotStore(Config config) : this(new MongoDbSnapshotSettings(config))
    {
    }

    public MongoDbGridFsSnapshotStore(MongoDbSnapshotSettings settings)
    {
        _settings = settings;
        _serialization = Context.System.Serialization;
        _bucketOptions = new GridFSBucketOptions
        {
            BucketName = settings.Collection, 
        };
    }

    private CancellationTokenSource CreatePerCallCts()
    {
        var unitedCts = CancellationTokenSource.CreateLinkedTokenSource(_pendingCommandsCancellation.Token);
        unitedCts.CancelAfter(_settings.CallTimeout);
        return unitedCts;
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
        return new GridFSBucket(GetMongoDb(), _bucketOptions);
    }

    private IMongoCollection<GridFSFileInfo> GetFilesCollection()
    {
        return GetMongoDb().GetCollection<GridFSFileInfo>(_settings.Collection + ".files");
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
        var token = unitedCts.Token;
        
        var filter = CreateRangeFilter(persistenceId, criteria);
        var filesCollection = GetFilesCollection();

        var info = await filesCollection.Find(filter)
            .SortByDescending(x => x.Metadata[SequenceNrKey])
            .Limit(1)
            .FirstOrDefaultAsync(token);
            
        if (info is null)
            return null;

        var bucket = GetGridFSBucket();
        var data = await bucket.DownloadAsBytesAsync(info.Id, cancellationToken: token);
        return ToSelectedSnapshot(info.Metadata, data);
    }

    protected override async Task SaveAsync(SnapshotMetadata metadata, object snapshot)
    {
        using var unitedCts = CreatePerCallCts();
        var token = unitedCts.Token;
        
        var (fileName, option, bytes) = ToSnapshotFileMetadata(metadata, snapshot);

        var filter = Builders<GridFSFileInfo>.Filter.Eq(doc => doc.Filename, fileName);
        var filesCollection = GetFilesCollection();
        var info = await filesCollection.Find(filter)
            .Limit(1)
            .FirstOrDefaultAsync(cancellationToken: token);
        
        var bucket = GetGridFSBucket();
        if (info is not null)
            await bucket.DeleteAsync(info.Id, token);
        
        await bucket.UploadFromBytesAsync(fileName, bytes, option, token);
    }

    protected override async Task DeleteAsync(SnapshotMetadata metadata)
    {
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

        using var unitedCts = CreatePerCallCts();
        await DeleteFileAsync(filter, GetFilesCollection(), GetGridFSBucket(), unitedCts.Token);
    }

    protected override async Task DeleteAsync(string persistenceId, SnapshotSelectionCriteria criteria)
    {
        using var unitedCts = CreatePerCallCts();
        var token = unitedCts.Token;
        
        var filter = CreateRangeFilter(persistenceId, criteria);
        var filesCollection = GetFilesCollection();
        var infos = await filesCollection
            .Find(filter)
            .ToListAsync(cancellationToken: token);
        var tasks = infos.Select(async info =>
        {
            var filesFilter = Builders<GridFSFileInfo>.Filter.Eq(i => i.Filename, info.Filename);
            await DeleteFileAsync(filesFilter, filesCollection, GetGridFSBucket(), token);
        });
        await Task.WhenAll(tasks);
    }

    private static async Task DeleteFileAsync(
        FilterDefinition<GridFSFileInfo> filesFilter,
        IMongoCollection<GridFSFileInfo> filesCollection,
        GridFSBucket bucket,
        CancellationToken token)
    {
        var info = await filesCollection.Find(filesFilter).Limit(1).FirstOrDefaultAsync(token);
        
        if(info is null)
            return;

        await bucket.DeleteAsync(info.Id, token);
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