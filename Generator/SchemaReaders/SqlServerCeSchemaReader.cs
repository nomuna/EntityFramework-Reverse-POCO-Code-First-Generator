using System.Data.Common;

namespace Generator.SchemaReaders
{
    public class SqlServerCeSchemaReader : SchemaReader
    {
        public SqlServerCeSchemaReader(DbProviderFactory factory, GeneratedTextTransformation outer)
            : base(factory, outer)
        {
        }

        protected override string TableSQL()
        {
            return @"
SELECT  '' AS SchemaName,
    c.TABLE_NAME AS TableName,
    'BASE TABLE' AS TableType,
    c.ORDINAL_POSITION AS Ordinal,
    c.COLUMN_NAME AS ColumnName,
    CAST(CASE WHEN c.IS_NULLABLE = N'YES' THEN 1
                ELSE 0
            END AS BIT) AS IsNullable,
    CASE WHEN c.DATA_TYPE = N'rowversion' THEN 'timestamp'
            ELSE c.DATA_TYPE
    END AS TypeName,
    CASE WHEN c.CHARACTER_MAXIMUM_LENGTH IS NOT NULL THEN c.CHARACTER_MAXIMUM_LENGTH
            ELSE 0
    END AS MaxLength,
    CASE WHEN c.NUMERIC_PRECISION IS NOT NULL THEN c.NUMERIC_PRECISION
            ELSE 0
    END AS Precision,
    c.COLUMN_DEFAULT AS [Default],
    CASE WHEN c.DATA_TYPE = N'datetime' THEN 0
            ELSE 0
    END AS DateTimePrecision,
    CASE WHEN c.DATA_TYPE = N'datetime' THEN 0
            WHEN c.NUMERIC_SCALE IS NOT NULL THEN c.NUMERIC_SCALE
            ELSE 0
    END AS Scale,
    CAST(CASE WHEN c.AUTOINC_INCREMENT > 0 THEN 1
                ELSE 0
            END AS BIT) AS IsIdentity,
    CAST(CASE WHEN c.DATA_TYPE = N'rowversion' THEN 1
                ELSE 0
            END AS BIT) AS IsStoreGenerated,
    CAST(CASE WHEN u.TABLE_NAME IS NULL THEN 0
                ELSE 1
            END AS BIT) AS PrimaryKey,
    0 AS PrimaryKeyOrdinal,
    0 as IsForeignKey
FROM    INFORMATION_SCHEMA.COLUMNS c
    INNER JOIN INFORMATION_SCHEMA.TABLES t
        ON c.TABLE_NAME = t.TABLE_NAME
    LEFT JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS cons
        ON cons.TABLE_NAME = c.TABLE_NAME
    LEFT JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS u
        ON cons.CONSTRAINT_NAME = u.CONSTRAINT_NAME
            AND u.TABLE_NAME = c.TABLE_NAME
            AND u.COLUMN_NAME = c.COLUMN_NAME
WHERE   t.TABLE_TYPE <> N'SYSTEM TABLE'
    AND cons.CONSTRAINT_TYPE = 'PRIMARY KEY'
ORDER BY c.TABLE_NAME,
    c.COLUMN_NAME,
    c.ORDINAL_POSITION";
        }

        protected override string ForeignKeySQL()
        {
            return @"
SELECT DISTINCT
    FK.TABLE_NAME AS FK_Table,
    FK.COLUMN_NAME AS FK_Column,
    PK.TABLE_NAME AS PK_Table,
    PK.COLUMN_NAME AS PK_Column,
    FK.CONSTRAINT_NAME AS Constraint_Name,
    '' AS fkSchema,
    '' AS pkSchema,
    PT.COLUMN_NAME AS primarykey,
    FK.ORDINAL_POSITION,
    CASE WHEN C.DELETE_RULE = 'CASCADE' THEN 1 ELSE 0 END AS CascadeOnDelete,
    CAST(0 AS BIT) AS IsNotEnforced
FROM    INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS AS C
    INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS FK
        ON FK.CONSTRAINT_NAME = C.CONSTRAINT_NAME
    INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS PK
        ON PK.CONSTRAINT_NAME = C.UNIQUE_CONSTRAINT_NAME
            AND PK.ORDINAL_POSITION = FK.ORDINAL_POSITION
    INNER JOIN (
                SELECT  i1.TABLE_NAME,
                        i2.COLUMN_NAME
                FROM    INFORMATION_SCHEMA.TABLE_CONSTRAINTS i1
                        INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE i2
                            ON i1.CONSTRAINT_NAME = i2.CONSTRAINT_NAME
                WHERE   i1.CONSTRAINT_TYPE = 'PRIMARY KEY'
                ) PT
        ON PT.TABLE_NAME = PK.TABLE_NAME
WHERE   PT.COLUMN_NAME = PK.COLUMN_NAME
ORDER BY FK.TABLE_NAME, FK.COLUMN_NAME";
        }

        protected override string ExtendedPropertySQL(DbConnection conn)
        {
            return @"
SELECT  '' AS [schema],
    [ObjectName] AS [column],
    [ParentName] AS [table],
    [Value] AS [property]
FROM    [__ExtendedProperties]";
        }

        protected override string DoesExtendedPropertyTableExistSQL()
        {
            return @"
SELECT  1
FROM    INFORMATION_SCHEMA.TABLES
WHERE   TABLE_NAME = '__ExtendedProperties'";
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
            var sysType = "VarChar";
            switch (sqlType)
            {
                case "hierarchyid":
                    sysType = "VarChar";
                    break;

                case "bigint":
                    sysType = "BigInt";
                    break;

                case "binary":
                    sysType = "Binary";
                    break;

                case "bit":
                    sysType = "Bit";
                    break;

                case "char":
                    sysType = "Char";
                    break;

                case "datetime":
                    sysType = "DateTime";
                    break;

                case "decimal":
                    sysType = "Decimal";
                    break;

                case "float":
                    sysType = "Float";
                    break;

                case "image":
                    sysType = "Image";
                    break;

                case "int":
                    sysType = "Int";
                    break;

                case "money":
                    sysType = "Money";
                    break;

                case "nchar":
                    sysType = "NChar";
                    break;

                case "ntext":
                    sysType = "NText";
                    break;

                case "nvarchar":
                    sysType = "NVarChar";
                    break;

                case "real":
                    sysType = "Real";
                    break;

                case "uniqueidentifier":
                    sysType = "UniqueIdentifier";
                    break;

                case "smalldatetime":
                    sysType = "SmallDateTime";
                    break;

                case "smallint":
                    sysType = "SmallInt";
                    break;

                case "smallmoney":
                    sysType = "SmallMoney";
                    break;

                case "text":
                    sysType = "Text";
                    break;

                case "timestamp":
                    sysType = "Timestamp";
                    break;

                case "tinyint":
                    sysType = "TinyInt";
                    break;

                case "varbinary":
                    sysType = "VarBinary";
                    break;

                case "varchar":
                    sysType = "VarChar";
                    break;

                case "variant":
                    sysType = "Variant";
                    break;

                case "xml":
                    sysType = "Xml";
                    break;

                case "udt":
                    sysType = "Udt";
                    break;

                case "table type":
                case "structured":
                    sysType = "Structured";
                    break;

                case "date":
                    sysType = "Date";
                    break;

                case "time":
                    sysType = "Time";
                    break;

                case "datetime2":
                    sysType = "DateTime2";
                    break;

                case "datetimeoffset":
                    sysType = "DateTimeOffset";
                    break;
            }
            return sysType;
        }

        protected override string GetPropertyType(string dbType)
        {
            var sysType = "string";
            switch (dbType)
            {
                case "hierarchyid":
                    sysType = "System.Data.Entity.Hierarchy.HierarchyId";
                    break;
                case "bigint":
                    sysType = "long";
                    break;
                case "smallint":
                    sysType = "short";
                    break;
                case "int":
                    sysType = "int";
                    break;
                case "uniqueidentifier":
                    sysType = "System.Guid";
                    break;
                case "smalldatetime":
                case "datetime":
                case "datetime2":
                case "date":
                    sysType = "System.DateTime";
                    break;
                case "datetimeoffset":
                    sysType = "System.DateTimeOffset";
                    break;
                case "table type":
                    sysType = "System.Data.DataTable";
                    break;
                case "time":
                    sysType = "System.TimeSpan";
                    break;
                case "float":
                    sysType = "double";
                    break;
                case "real":
                    sysType = "float";
                    break;
                case "numeric":
                case "smallmoney":
                case "decimal":
                case "money":
                    sysType = "decimal";
                    break;
                case "tinyint":
                    sysType = "byte";
                    break;
                case "bit":
                    sysType = "bool";
                    break;
                case "image":
                case "binary":
                case "varbinary":
                case "varbinary(max)":
                case "timestamp":
                    sysType = "byte[]";
                    break;
                case "geography":
                    sysType = Settings.DisableGeographyTypes ? string.Empty : "System.Data.Entity.Spatial.DbGeography";
                    break;
                case "geometry":
                    sysType = Settings.DisableGeographyTypes ? string.Empty : "System.Data.Entity.Spatial.DbGeometry";
                    break;
            }
            return sysType;
        }

        public override void Init()
        {
            Settings.IsSqlCe = true;
            Settings.PrependSchemaName = false;
        }
    }
}