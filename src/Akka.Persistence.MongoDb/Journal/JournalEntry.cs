//-----------------------------------------------------------------------
// <copyright file="JournalEntry.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

﻿using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

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
    }


    public class JournalCommit
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("Entries")]
        public JournalEntry[] Entries { get; set; }

    }
}
