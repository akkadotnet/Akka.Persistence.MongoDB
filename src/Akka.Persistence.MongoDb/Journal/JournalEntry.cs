//-----------------------------------------------------------------------
// <copyright file="JournalEntry.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace Akka.Persistence.MongoDb.Journal
{
    /// <summary>
    /// Class used for storing intermediate result of the <see cref="IPersistentRepresentation"/>
    /// as BsonDocument into the MongoDB-Collection
    /// </summary>
    public class JournalEntry
    {
        [BsonId]
        public string Id { get; set; }

        [BsonElement("PersistenceId")]
        public string PersistenceId { get; set; }

        [BsonElement("SequenceNr")]
        public long SequenceNr { get; set; }

        [BsonElement("IsDeleted")]
        public bool IsDeleted { get; set; }

        [BsonElement("Payload")]
        public object Payload { get; set; }

        [BsonElement("Manifest")]
        public string Manifest { get; set; }

        [BsonElement("Ordering")]
        public BsonTimestamp Ordering { get; set; }

        [BsonElement("Timestamp")]
        public BsonTimestamp Timestamp { get; set; }

        [BsonElement("Tags")]
        public ICollection<string> Tags { get; set; } = new HashSet<string>();
      
        [BsonElement("SerializerId")]
        public int? SerializerId { get; set; }
    }
}
/*
  {configuration.OrderingColumnName} INTEGER PRIMARY KEY NOT NULL,
                    {configuration.PersistenceIdColumnName} VARCHAR(255) NOT NULL,
                    {configuration.SequenceNrColumnName} INTEGER(8) NOT NULL,
                    {configuration.IsDeletedColumnName} INTEGER(1) NOT NULL,
                    {configuration.ManifestColumnName} VARCHAR(255) NULL,
                    {configuration.TimestampColumnName} INTEGER NOT NULL,
                    {configuration.PayloadColumnName} BLOB NOT NULL,
                    {configuration.TagsColumnName} VARCHAR(2000) NULL,
                    {configuration.SerializerIdColumnName} INTEGER(4),
 */
