using System.Data.Common;

namespace Generator.SchemaReaders
{
    public class PostgreSqlSchemaReader : SchemaReader
    {
        public PostgreSqlSchemaReader(DbProviderFactory factory, GeneratedTextTransformation outer)
            : base(factory, outer)
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

        protected override string ExtendedPropertySQL(DbConnection conn)
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

        public override bool CanReadStoredProcedures()
        {
            return false;
        }

        protected override string StoredProcedureSQL(DbConnection conn)
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

        public override void Init()
        {
            throw new System.NotImplementedException();
        }
    }
}