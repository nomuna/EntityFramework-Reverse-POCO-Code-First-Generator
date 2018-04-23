using System.Data.Common;

namespace Generator.DatabaseReaders
{
    public static class DatabaseReaderFactory
    {
        public static DatabaseReader Create(DbProviderFactory factory, GeneratedTextTransformation outer)
        {
            switch (Settings.DatabaseType)
            {
                case DatabaseType.SqlServer:
                    return new SqlServerDatabaseReader(factory, outer);

                case DatabaseType.SqlCe:
                    return new SqlServerCeDatabaseReader(factory, outer);

                case DatabaseType.MySql:
                    return new MySqlDatabaseReader(factory, outer);

                case DatabaseType.PostgreSQL:
                    return new PostgreSqlDatabaseReader(factory, outer);

                case DatabaseType.Oracle:
                    return new OracleDatabaseReader(factory, outer);

                default:
                    return null;
            }
        }
    }
}