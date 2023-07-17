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

    public MongoDbJournalOptions(bool isDefault, string identifier = "mongodb") : base(isDefault)
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
    public string Collection { get; set; } = "EventJournal";

    /// <summary>
    /// Name of the collection for the event journal metadata
    /// </summary>
    public string MetadataCollection { get; set; } = "Metadata";

    /// <summary>
    /// Transaction
    /// </summary>
    public bool UseWriteTransaction { get; set; } = false;

    /// <summary>
    /// When true, enables BSON serialization (which breaks features like Akka.Cluster.Sharding, AtLeastOnceDelivery, and so on.)
    /// </summary>
    public bool LegacySerialization { get; set; } = false;

    /// <summary>
    /// Timeout for individual database operations.
    /// </summary>
    /// <remarks>
    /// Defaults to 10s.
    /// </remarks>
    public TimeSpan CallTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public override string Identifier { get; set; }
    protected override Config InternalDefaultConfig { get; } = Default;

    protected override StringBuilder Build(StringBuilder sb)
    {
        sb.AppendLine($"connection-string = {ConnectionString.ToHocon()}");
        sb.AppendLine($"use-write-transaction = {(UseWriteTransaction ? "on" : "off")}");
        sb.AppendLine($"auto-initialize = {(AutoInitialize ? "on" : "off")}");
        sb.AppendLine($"collection = {Collection.ToHocon()}");
        sb.AppendLine($"metadata-collection = {MetadataCollection.ToHocon()}");
        sb.AppendLine($"legacy-serialization = {(LegacySerialization ? "on" : "off")}");
        sb.AppendLine($"call-timeout = {CallTimeout.ToHocon()}");

        return base.Build(sb);
    }
}