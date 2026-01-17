using System.Data.Common;
using Aquamancy.IData;
using MySqlConnector;

namespace Aquamancy.Data
{
    public class MariaDbConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public MariaDbConnectionFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public DbConnection CreateConnection()
        {
            return new MySqlConnection(_connectionString);
        }
    }
}
