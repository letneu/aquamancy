using System.Data.Common;

namespace Aquamancy.IData
{
    public interface IDbConnectionFactory
    {
        DbConnection CreateConnection();
    }
}
