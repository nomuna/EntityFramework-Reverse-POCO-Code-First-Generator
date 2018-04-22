using System.Data.Common;

namespace Generator.SchemaReaders
{
    public static class SchemaReaderFactory
    {
        public static SchemaReader CreateSchemaReader(DbProviderFactory factory, GeneratedTextTransformation outer)
        {
            switch (Settings.DatabaseType)
            {
                case DatabaseType.SqlServer:
                    return new SqlServerSchemaReader(factory, outer);

                case DatabaseType.SqlCe:
                    return new SqlServerCeSchemaReader(factory, outer);

                case DatabaseType.MySql:
                    return new MySqlSchemaReader(factory, outer);

                case DatabaseType.PostgreSQL:
                    return new PostgreSqlSchemaReader(factory, outer);

                case DatabaseType.Oracle:
                    return new OracleSchemaReader(factory, outer);

                default:
                    return null;
            }
        }
    }
}