using MongoDB.Driver;

namespace Akka.Persistence.MongoDb.Auto
{
    public class SequenceRepository
    {
        protected readonly IMongoDatabase _database;
        protected readonly IMongoCollection<AutoSequence> _collection;

        public SequenceRepository(IMongoDatabase database)
        {
            _database = database;
            _collection = _database.GetCollection<AutoSequence>(typeof(AutoSequence).Name);
        }

        public long GetSequenceValue(string sequenceName)
        {
            var filter = Builders<AutoSequence>.Filter.Eq(s => s.SequenceName, sequenceName);
            var update = Builders<AutoSequence>.Update.Inc(s => s.SequenceValue, 1);

            var result = _collection.FindOneAndUpdate(filter, update, new FindOneAndUpdateOptions<AutoSequence, AutoSequence> { IsUpsert = true, ReturnDocument = ReturnDocument.After });

            return result.SequenceValue;
        }
    }
}
