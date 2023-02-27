
namespace Akka.Persistence.MongoDb.Tests
{
    internal class MongoDbConnectionString
    {
        public string ConnectionString(DatabaseFixture databaseFixture, int id)
        {
            var s = databaseFixture.ConnectionString.Split('?');
            var connectionString = s[0] + $"{id}?" + s[1];
            return connectionString;
        }
        public string ConnectionString(string cString)
        {
            var s = cString.Split('?');
            var connectionString = s[0] + $"akkanet?" + s[1];
            return connectionString;
        }
    }
}
