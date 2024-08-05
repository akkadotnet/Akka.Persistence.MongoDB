using System;
using System.Text;
using Akka.Configuration;
using Akka.Hosting;
using Akka.Persistence.Hosting;
using Akka.Persistence.MongoDb.Snapshot;

#nullable enable
namespace Akka.Persistence.MongoDb.Hosting;

public class MongoDbGridFsSnapshotOptions : MongoDbSnapshotOptions
{
    public MongoDbGridFsSnapshotOptions() : this(true)
    {
    }

    public MongoDbGridFsSnapshotOptions(bool isDefault, string identifier = "mongodb") : base(isDefault, identifier)
    {
    }

    public override Type Type { get; } = typeof(MongoDbGridFsSnapshotStore);
}