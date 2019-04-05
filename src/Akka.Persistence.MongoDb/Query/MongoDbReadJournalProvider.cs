using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Query;

namespace Akka.Persistence.MongoDb.Query
{
    public class MongoDbReadJournalProvider : IReadJournalProvider
    {
        private readonly ExtendedActorSystem _system;
        private readonly Config _config;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="system">instance of actor system at which read journal should be started</param>
        /// <param name="config"></param>
        public MongoDbReadJournalProvider(ExtendedActorSystem system, Config config)
        {
            _system = system;
            _config = config;
        }

        /// <summary>
        /// Returns instance of EventStoreReadJournal
        /// </summary>
        /// <returns></returns>
        public IReadJournal GetReadJournal()
        {
            return new MongoDbReadJournal(_system, _config);
        }
    }
}
