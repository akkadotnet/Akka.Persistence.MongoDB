using System;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Journal;
using Akka.Persistence.Query;
using Akka.Streams.Dsl;
using MongoDB.Driver;

namespace Akka.Persistence.MongoDb.Query
{
    public class MongoDbReadJournal : IReadJournal,
        IPersistenceIdsQuery,
        ICurrentPersistenceIdsQuery
    {
        /// <summary>
        /// HOCON identifier
        /// </summary>
        public const string Identifier = "akka.persistence.query.mongodb";

        private readonly string _writeJournalPluginId;
        private readonly int _maxBufferSize;

        /// <inheritdoc />
        public MongoDbReadJournal(ExtendedActorSystem system, Config config)
        {
            _writeJournalPluginId = config.GetString("write-plugin");
            _maxBufferSize = config.GetInt("max-buffer-size");
        }

        /// <summary>
        /// Returns a default query configuration for akka persistence SQLite-based journals and snapshot stores.
        /// </summary>
        /// <returns></returns>
        public static Config DefaultConfiguration()
        {
            return ConfigurationFactory.FromResource<MongoDbReadJournal>(
                "Akka.Persistence.MongoDb.reference.conf");
        }

        /// <summary>
        /// <para>
        /// <see cref="PersistenceIds"/> is used for retrieving all `persistenceIds` of all
        /// persistent actors.
        /// </para>
        /// The returned event stream is unordered and you can expect different order for multiple
        /// executions of the query.
        /// <para>
        /// The stream is not completed when it reaches the end of the currently used `persistenceIds`,
        /// but it continues to push new `persistenceIds` when new persistent actors are created.
        /// Corresponding query that is completed when it reaches the end of the currently
        /// currently used `persistenceIds` is provided by <see cref="CurrentPersistenceIds"/>.
        /// </para>
        /// The SQL write journal is notifying the query side as soon as new `persistenceIds` are
        /// created and there is no periodic polling or batching involved in this query.
        /// <para>
        /// The stream is completed with failure if there is a failure in executing the query in the
        /// backend journal.
        /// </para>
        /// </summary>
        public Source<string, NotUsed> PersistenceIds() =>
            Source.ActorPublisher<string>(AllPersistenceIdsPublisher.Props(true, _writeJournalPluginId))
            .MapMaterializedValue(_ => NotUsed.Instance)
            .Named("AllPersistenceIds") as Source<string, NotUsed>;

        /// <summary>
        /// Same type of query as <see cref="PersistenceIds"/> but the stream
        /// is completed immediately when it reaches the end of the "result set". Persistent
        /// actors that are created after the query is completed are not included in the stream.
        /// </summary>
        public Source<string, NotUsed> CurrentPersistenceIds() =>
            Source.ActorPublisher<string>(AllPersistenceIdsPublisher.Props(false, _writeJournalPluginId))
            .MapMaterializedValue(_ => NotUsed.Instance)
            .Named("CurrentPersistenceIds") as Source<string, NotUsed>;

    }
}
