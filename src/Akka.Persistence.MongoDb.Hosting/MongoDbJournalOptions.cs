using System;
using System.Text;
using Akka.Configuration;
using Akka.Hosting;
using Akka.Persistence.Hosting;

#nullable enable
namespace Akka.Persistence.MongoDb.Hosting;

public class MongoDbJournalOptions : JournalOptions
{
    private static readonly Config Default = MongoDbPersistence.DefaultConfiguration()
        .GetConfig(MongoDbJournalSettings.JournalConfigPath);

    public MongoDbJournalOptions() : this(true)
    {
    }

    public MongoDbJournalOptions(bool isDefaultPlugin, string identifier = "mongodb") : base(isDefaultPlugin)
    {
        Identifier = identifier;
        AutoInitialize = true;
    }

    /// <summary>
    /// Connection string used to access the MongoDb, also specifies the database.
    /// </summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>
    /// Name of the collection for the event journal or snapshots
    /// </summary>
    public string? Collection { get; set; }

    /// <summary>
    /// Name of the collection for the event journal metadata
    /// </summary>
    public string? MetadataCollection { get; set; }

    /// <summary>
    /// Transaction
    /// </summary>
    public bool? UseWriteTransaction { get; set; }

    /// <summary>
    /// When true, enables BSON serialization (which breaks features like Akka.Cluster.Sharding, AtLeastOnceDelivery, and so on.)
    /// </summary>
    public bool? LegacySerialization { get; set; }

    /// <summary>
    /// Timeout for individual database operations.
    /// </summary>
    /// <remarks>
    /// Defaults to 10s.
    /// </remarks>
    public TimeSpan? CallTimeout { get; set; }

    public override string Identifier { get; set; }
    protected override Config InternalDefaultConfig { get; } = Default;

    protected override StringBuilder Build(StringBuilder sb)
    {
        sb.AppendLine($"connection-string = {ConnectionString.ToHocon()}");
        
        if(Collection is not null)
            sb.AppendLine($"collection = {Collection.ToHocon()}");
        
        if(MetadataCollection is not null)
            sb.AppendLine($"metadata-collection = {MetadataCollection.ToHocon()}");
        
        if(CallTimeout is not null)
            sb.AppendLine($"call-timeout = {CallTimeout.ToHocon()}");
        
        if(UseWriteTransaction is not null)
            sb.AppendLine($"use-write-transaction = {UseWriteTransaction.ToHocon()}");
        
        if(LegacySerialization is not null)
            sb.AppendLine($"legacy-serialization = {LegacySerialization.ToHocon()}");

        return base.Build(sb);
    }
}