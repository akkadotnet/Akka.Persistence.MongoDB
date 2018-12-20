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
        ICurrentPersistenceIdsQuery,
        IEventsByPersistenceIdQuery,
        ICurrentEventsByPersistenceIdQuery
    {
        /// <summary>
        /// HOCON identifier
        /// </summary>
        public const string Identifier = "akka.persistence.query.mongodb";

        private readonly TimeSpan _refreshInterval;
        private readonly string _writeJournalPluginId;
        private readonly int _maxBufferSize;
        
        /// <inheritdoc />
        public MongoDbReadJournal(ExtendedActorSystem system, Config config)
        {
            _refreshInterval = config.GetTimeSpan("refresh-interval");
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

        /// <summary>
        /// <see cref="EventsByPersistenceId"/> is used for retrieving events for a specific
        /// <see cref="PersistentActor"/> identified by <see cref="Eventsourced.PersistenceId"/>.
        /// <para>
        /// You can retrieve a subset of all events by specifying <paramref name="fromSequenceNr"/> and <paramref name="toSequenceNr"/>
        /// or use `0L` and <see cref="long.MaxValue"/> respectively to retrieve all events. Note that
        /// the corresponding sequence number of each event is provided in the
        /// <see cref="EventEnvelope"/>, which makes it possible to resume the
        /// stream at a later point from a given sequence number.
        /// </para>
        /// The returned event stream is ordered by sequence number, i.e. the same order as the
        /// <see cref="PersistentActor"/> persisted the events. The same prefix of stream elements (in same order)
        ///  are returned for multiple executions of the query, except for when events have been deleted.
        /// <para>
        /// The stream is not completed when it reaches the end of the currently stored events,
        /// but it continues to push new events when new events are persisted.
        /// Corresponding query that is completed when it reaches the end of the currently
        /// stored events is provided by <see cref="CurrentEventsByPersistenceId"/>.
        /// </para>
        /// The SQLite write journal is notifying the query side as soon as events are persisted, but for
        /// efficiency reasons the query side retrieves the events in batches that sometimes can
        /// be delayed up to the configured `refresh-interval`.
        /// <para></para>
        /// The stream is completed with failure if there is a failure in executing the query in the
        /// backend journal.
        /// </summary>
        public Source<EventEnvelope, NotUsed> EventsByPersistenceId(string persistenceId, long fromSequenceNr, long toSequenceNr) =>
                Source.ActorPublisher<EventEnvelope>(EventsByPersistenceIdPublisher.Props(persistenceId, fromSequenceNr, toSequenceNr, _refreshInterval, _maxBufferSize, _writeJournalPluginId))
                    .MapMaterializedValue(_ => NotUsed.Instance)
                    .Named("EventsByPersistenceId-" + persistenceId) as Source<EventEnvelope, NotUsed>;

        /// <summary>
        /// Same type of query as <see cref="EventsByPersistenceId"/> but the event stream
        /// is completed immediately when it reaches the end of the "result set". Events that are
        /// stored after the query is completed are not included in the event stream.
        /// </summary>
        public Source<EventEnvelope, NotUsed> CurrentEventsByPersistenceId(string persistenceId, long fromSequenceNr, long toSequenceNr) =>
                Source.ActorPublisher<EventEnvelope>(EventsByPersistenceIdPublisher.Props(persistenceId, fromSequenceNr, toSequenceNr, null, _maxBufferSize, _writeJournalPluginId))
                    .MapMaterializedValue(_ => NotUsed.Instance)
                    .Named("CurrentEventsByPersistenceId-" + persistenceId) as Source<EventEnvelope, NotUsed>;

    }
}
