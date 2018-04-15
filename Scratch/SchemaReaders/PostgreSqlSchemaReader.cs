using System.Data.Common;

namespace Scratch.SchemaReaders
{
    public class PostgreSqlSchemaReader : SchemaReader
    {
        public PostgreSqlSchemaReader(DbConnection connection, DbProviderFactory factory, GeneratedTextTransformation generatedTextTransformation)
            : base(connection, factory, generatedTextTransformation)
        {
        }

        protected override string TableSQL()
        {
            return string.Empty;
        }

        protected override string ForeignKeySQL()
        {
            return string.Empty;
        }

        protected override string ExtendedPropertySQL()
        {
            return string.Empty;
        }

        protected override string DoesExtendedPropertyTableExistSQL()
        {
            return string.Empty;
        }

        protected override string IndexSQL()
        {
            return string.Empty;
        }

        protected override string StoredProcedureSQL()
        {
            return string.Empty;
        }

        protected override string SynonymTableSQLSetup()
        {
            return string.Empty;
        }

        protected override string SynonymTableSQL()
        {
            return string.Empty;
        }

        protected override string SynonymForeignKeySQLSetup()
        {
            return string.Empty;
        }

        protected override string SynonymForeignKeySQL()
        {
            return string.Empty;
        }

        protected override string SynonymStoredProcedureSQLSetup()
        {
            return string.Empty;
        }

        protected override string SynonymStoredProcedureSQL()
        {
            return string.Empty;
        }

        protected override string SpecialQueryFlags()
        {
            return string.Empty;
        }

        protected override string GetStoredProcedureParameterDbType(string sqlType)
        {
            return string.Empty;
        }

        protected override string GetPropertyType(string dbType)
        {
            return string.Empty;
        }
    }
}