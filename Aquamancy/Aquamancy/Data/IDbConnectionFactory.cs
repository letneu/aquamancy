using System.Data.Common;

namespace Aquamancy.Data
{
    public interface IDbConnectionFactory
    {
        DbConnection CreateConnection();
    }
}
