using System.Data.Common;

namespace Scratch.SchemaReaders
{
    // Used for both SQL Server and SQL Azure
    public class SqlServerSchemaReader : SchemaReader
    {
        private string _sqlDatabaseEdition, _sqlDatabaseEngineEdition;

        public SqlServerSchemaReader(DbConnection connection, DbProviderFactory factory, GeneratedTextTransformation outer)
            : base(connection, factory, outer)
        {
            _sqlDatabaseEdition = null;
            _sqlDatabaseEngineEdition = null;
        }

        protected override string TableSQL()
        {
            return @"
SET NOCOUNT ON;
IF OBJECT_ID('tempdb..#Columns')     IS NOT NULL DROP TABLE #Columns;
IF OBJECT_ID('tempdb..#PrimaryKeys') IS NOT NULL DROP TABLE #PrimaryKeys;
IF OBJECT_ID('tempdb..#ForeignKeys') IS NOT NULL DROP TABLE #ForeignKeys;

SELECT  C.TABLE_SCHEMA,
        C.TABLE_NAME,
        C.COLUMN_NAME,
        C.ORDINAL_POSITION,
        C.COLUMN_DEFAULT,
        C.IS_NULLABLE,
        C.DATA_TYPE,
        C.CHARACTER_MAXIMUM_LENGTH,
        C.NUMERIC_PRECISION,
        C.NUMERIC_SCALE,
        C.DATETIME_PRECISION
INTO    #Columns
FROM    INFORMATION_SCHEMA.COLUMNS C
WHERE   C.TABLE_NAME NOT IN ('EdmMetadata', '__MigrationHistory')
        AND C.TABLE_NAME NOT LIKE 'sysdiagram%';

CREATE NONCLUSTERED INDEX IX_EfPoco_Columns
ON dbo.#Columns (TABLE_NAME)
INCLUDE (TABLE_SCHEMA,COLUMN_NAME,ORDINAL_POSITION,COLUMN_DEFAULT,IS_NULLABLE,DATA_TYPE,CHARACTER_MAXIMUM_LENGTH,NUMERIC_PRECISION,NUMERIC_SCALE,DATETIME_PRECISION);

SELECT  u.TABLE_SCHEMA,
        u.TABLE_NAME,
        u.COLUMN_NAME,
        u.ORDINAL_POSITION
INTO    #PrimaryKeys
FROM    INFORMATION_SCHEMA.KEY_COLUMN_USAGE u
        INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            ON u.TABLE_SCHEMA COLLATE DATABASE_DEFAULT = tc.CONSTRAINT_SCHEMA COLLATE DATABASE_DEFAULT
                AND u.TABLE_NAME = tc.TABLE_NAME
                AND u.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
WHERE   CONSTRAINT_TYPE = 'PRIMARY KEY';

SELECT DISTINCT
        u.TABLE_SCHEMA,
        u.TABLE_NAME,
        u.COLUMN_NAME
INTO    #ForeignKeys
FROM    INFORMATION_SCHEMA.KEY_COLUMN_USAGE u
        INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            ON u.TABLE_SCHEMA COLLATE DATABASE_DEFAULT = tc.CONSTRAINT_SCHEMA COLLATE DATABASE_DEFAULT
                AND u.TABLE_NAME = tc.TABLE_NAME
                AND u.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
WHERE   CONSTRAINT_TYPE = 'FOREIGN KEY';

SELECT  c.TABLE_SCHEMA AS SchemaName,
        c.TABLE_NAME AS TableName,
        t.TABLE_TYPE AS TableType,
        c.ORDINAL_POSITION AS Ordinal,
        c.COLUMN_NAME AS ColumnName,
        CAST(CASE WHEN IS_NULLABLE = 'YES' THEN 1
                    ELSE 0
                END AS BIT) AS IsNullable,
        DATA_TYPE AS TypeName,
        ISNULL(CHARACTER_MAXIMUM_LENGTH, 0) AS MaxLength,
        CAST(ISNULL(NUMERIC_PRECISION, 0) AS INT) AS Precision,
        ISNULL(COLUMN_DEFAULT, '') AS [Default],
        CAST(ISNULL(DATETIME_PRECISION, 0) AS INT) AS DateTimePrecision,
        ISNULL(NUMERIC_SCALE, 0) AS Scale,
        CAST(COLUMNPROPERTY(OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)), c.COLUMN_NAME, 'IsIdentity') AS BIT) AS IsIdentity,
        CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)), c.COLUMN_NAME, 'IsIdentity') = 1 THEN 1
                    WHEN COLUMNPROPERTY(OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)), c.COLUMN_NAME, 'IsComputed') = 1 THEN 1
                    WHEN DATA_TYPE = 'TIMESTAMP' THEN 1
                    WHEN DATA_TYPE = 'UNIQUEIDENTIFIER'
                        AND LOWER(ISNULL(COLUMN_DEFAULT, '')) LIKE '%newsequentialid%' THEN 1
                    ELSE 0
                END AS BIT) AS IsStoreGenerated,
        CAST(CASE WHEN pk.ORDINAL_POSITION IS NULL THEN 0
                    ELSE 1
                END AS BIT) AS PrimaryKey,
        ISNULL(pk.ORDINAL_POSITION, 0) PrimaryKeyOrdinal,
        CAST(CASE WHEN fk.COLUMN_NAME IS NULL THEN 0
                    ELSE 1
                END AS BIT) AS IsForeignKey
FROM    #Columns c
        LEFT OUTER JOIN #PrimaryKeys pk
            ON c.TABLE_SCHEMA = pk.TABLE_SCHEMA
                AND c.TABLE_NAME = pk.TABLE_NAME
                AND c.COLUMN_NAME = pk.COLUMN_NAME
        LEFT OUTER JOIN #ForeignKeys fk
            ON c.TABLE_SCHEMA = fk.TABLE_SCHEMA
                AND c.TABLE_NAME = fk.TABLE_NAME
                AND c.COLUMN_NAME = fk.COLUMN_NAME
        INNER JOIN INFORMATION_SCHEMA.TABLES t
            ON c.TABLE_SCHEMA COLLATE DATABASE_DEFAULT = t.TABLE_SCHEMA COLLATE DATABASE_DEFAULT
                AND c.TABLE_NAME COLLATE DATABASE_DEFAULT = t.TABLE_NAME COLLATE DATABASE_DEFAULT
WHERE   c.TABLE_NAME NOT IN ('EdmMetadata', '__MigrationHistory')
        AND c.TABLE_NAME NOT LIKE 'sysdiagram%' ";
        }

        protected override string ForeignKeySQL()
        {
            return @"
SELECT  fkData.FK_Table,
        fkData.FK_Column,
        fkData.PK_Table,
        fkData.PK_Column,
        fkData.Constraint_Name,
        fkData.fkSchema,
        fkData.pkSchema,
        fkData.primarykey,
        fkData.ORDINAL_POSITION,
        fkData.CascadeOnDelete,
        fkData.IsNotEnforced
FROM    (SELECT FK.name AS FK_Table,
                FkCol.name AS FK_Column,
                PK.name AS PK_Table,
                PkCol.name AS PK_Column,
                OBJECT_NAME(f.object_id) AS Constraint_Name,
                SCHEMA_NAME(FK.schema_id) AS fkSchema,
                SCHEMA_NAME(PK.schema_id) AS pkSchema,
                PkCol.name AS primarykey,
                k.constraint_column_id AS ORDINAL_POSITION,
                CASE WHEN f.delete_referential_action = 1 THEN 1
                        ELSE 0
                END AS CascadeOnDelete,
                f.is_disabled AS IsNotEnforced,
                ROW_NUMBER() OVER (PARTITION BY FK.name, FkCol.name, PK.name, PkCol.name, SCHEMA_NAME(FK.schema_id), SCHEMA_NAME(PK.schema_id) ORDER BY f.object_id) AS n
            FROM   sys.objects AS PK
                INNER JOIN sys.foreign_keys AS f
                    INNER JOIN sys.foreign_key_columns AS k
                        ON k.constraint_object_id = f.object_id
                    INNER JOIN sys.indexes AS i
                        ON f.referenced_object_id = i.object_id
                            AND f.key_index_id = i.index_id
                    ON PK.object_id = f.referenced_object_id
                INNER JOIN sys.objects AS FK
                    ON f.parent_object_id = FK.object_id
                INNER JOIN sys.columns AS PkCol
                    ON f.referenced_object_id = PkCol.object_id
                        AND k.referenced_column_id = PkCol.column_id
                INNER JOIN sys.columns AS FkCol
                    ON f.parent_object_id = FkCol.object_id
                        AND k.parent_column_id = FkCol.column_id) fkData
WHERE   fkData.n = 1 -- Remove duplicate FK's";
        }

        protected override string ExtendedPropertySQL()
        {
            if (IsAzure())
                return null;

            return @"
SELECT  s.name AS [schema],
    t.name AS [table],
    c.name AS [column],
    value AS [property]
FROM    sys.extended_properties AS ep
    INNER JOIN sys.tables AS t
        ON ep.major_id = t.object_id
    INNER JOIN sys.schemas AS s
        ON s.schema_id = t.schema_id
    LEFT JOIN sys.columns AS c
        ON ep.major_id = c.object_id
            AND ep.minor_id = c.column_id
WHERE   class = 1
ORDER BY t.name";
        }

        protected override string DoesExtendedPropertyTableExistSQL()
        {
            return null;
        }

        protected override string IndexSQL()
        {
            return @"
SELECT  SCHEMA_NAME(t.schema_id) AS TableSchema,
    t.name AS TableName,
    ind.name AS IndexName,
    ic.key_ordinal AS KeyOrdinal,
    col.name AS ColumnName,
    ind.is_unique AS IsUnique,
    ind.is_primary_key AS IsPrimaryKey,
    ind.is_unique_constraint AS IsUniqueConstraint,
    CASE WHEN ind.[type] = 1 AND ind.is_primary_key = 1 THEN 1 ELSE 0 END AS IsClustered,
    (
        SELECT COUNT(1)
        FROM   sys.index_columns i
        WHERE  i.object_id = ind.object_id
            AND i.index_id = ind.index_id
    ) AS ColumnCount
FROM    sys.tables t
    INNER JOIN sys.indexes ind
        ON ind.object_id = t.object_id
    INNER JOIN sys.index_columns ic
        ON ind.object_id = ic.object_id
            AND ind.index_id = ic.index_id
    INNER JOIN sys.columns col
        ON ic.object_id = col.object_id
            AND ic.column_id = col.column_id
WHERE   t.is_ms_shipped = 0
    AND ind.ignore_dup_key = 0
    AND ic.key_ordinal > 0
    AND t.name NOT LIKE 'sysdiagram%'";
        }

        protected override string StoredProcedureSQL()
        {
            if (IsAzure())
                return @"
SELECT  R.SPECIFIC_SCHEMA,
        R.SPECIFIC_NAME,
        R.ROUTINE_TYPE,
        P.ORDINAL_POSITION,
        P.PARAMETER_MODE,
        P.PARAMETER_NAME,
        P.DATA_TYPE,
        ISNULL(P.CHARACTER_MAXIMUM_LENGTH, 0) AS CHARACTER_MAXIMUM_LENGTH,
        ISNULL(P.NUMERIC_PRECISION, 0) AS NUMERIC_PRECISION,
        ISNULL(P.NUMERIC_SCALE, 0) AS NUMERIC_SCALE,
        ISNULL(P.DATETIME_PRECISION, 0) AS DATETIME_PRECISION,
        P.USER_DEFINED_TYPE_SCHEMA + '.' + P.USER_DEFINED_TYPE_NAME AS USER_DEFINED_TYPE
FROM    INFORMATION_SCHEMA.ROUTINES R
        LEFT OUTER JOIN INFORMATION_SCHEMA.PARAMETERS P
            ON P.SPECIFIC_SCHEMA = R.SPECIFIC_SCHEMA
                AND P.SPECIFIC_NAME = R.SPECIFIC_NAME
WHERE   R.ROUTINE_TYPE = 'PROCEDURE'
        AND (
                P.IS_RESULT = 'NO'
                OR P.IS_RESULT IS NULL
            )
        AND R.SPECIFIC_SCHEMA + R.SPECIFIC_NAME IN (
            SELECT  SCHEMA_NAME(sp.schema_id) + sp.name
            FROM    sys.all_objects AS sp
                    LEFT OUTER JOIN sys.all_sql_modules AS sm
                        ON sm.object_id = sp.object_id
            WHERE   sp.type = 'P'
                    AND sp.is_ms_shipped = 0)
UNION ALL
SELECT  R.SPECIFIC_SCHEMA,
        R.SPECIFIC_NAME,
        R.ROUTINE_TYPE,
        P.ORDINAL_POSITION,
        P.PARAMETER_MODE,
        P.PARAMETER_NAME,
        P.DATA_TYPE,
        ISNULL(P.CHARACTER_MAXIMUM_LENGTH, 0) AS CHARACTER_MAXIMUM_LENGTH,
        ISNULL(P.NUMERIC_PRECISION, 0) AS NUMERIC_PRECISION,
        ISNULL(P.NUMERIC_SCALE, 0) AS NUMERIC_SCALE,
        ISNULL(P.DATETIME_PRECISION, 0) AS DATETIME_PRECISION,
        P.USER_DEFINED_TYPE_SCHEMA + '.' + P.USER_DEFINED_TYPE_NAME AS USER_DEFINED_TYPE
FROM    INFORMATION_SCHEMA.ROUTINES R
        LEFT OUTER JOIN INFORMATION_SCHEMA.PARAMETERS P
            ON P.SPECIFIC_SCHEMA = R.SPECIFIC_SCHEMA
                AND P.SPECIFIC_NAME = R.SPECIFIC_NAME
WHERE   R.ROUTINE_TYPE = 'FUNCTION'
        AND R.DATA_TYPE = 'TABLE'
ORDER BY R.SPECIFIC_SCHEMA,
        R.SPECIFIC_NAME,
        P.ORDINAL_POSITION";

            return @"
SELECT  R.SPECIFIC_SCHEMA,
        R.SPECIFIC_NAME,
        R.ROUTINE_TYPE,
        P.ORDINAL_POSITION,
        P.PARAMETER_MODE,
        P.PARAMETER_NAME,
        P.DATA_TYPE,
        ISNULL(P.CHARACTER_MAXIMUM_LENGTH, 0) AS CHARACTER_MAXIMUM_LENGTH,
        ISNULL(P.NUMERIC_PRECISION, 0) AS NUMERIC_PRECISION,
        ISNULL(P.NUMERIC_SCALE, 0) AS NUMERIC_SCALE,
        ISNULL(P.DATETIME_PRECISION, 0) AS DATETIME_PRECISION,
        P.USER_DEFINED_TYPE_SCHEMA + '.' + P.USER_DEFINED_TYPE_NAME AS USER_DEFINED_TYPE
FROM    INFORMATION_SCHEMA.ROUTINES R
        LEFT OUTER JOIN INFORMATION_SCHEMA.PARAMETERS P
            ON P.SPECIFIC_SCHEMA = R.SPECIFIC_SCHEMA
                AND P.SPECIFIC_NAME = R.SPECIFIC_NAME
WHERE   R.ROUTINE_TYPE = 'PROCEDURE'
        AND (
                P.IS_RESULT = 'NO'
                OR P.IS_RESULT IS NULL
            )
        AND R.SPECIFIC_SCHEMA + R.SPECIFIC_NAME IN (
            SELECT  SCHEMA_NAME(sp.schema_id) + sp.name
            FROM    sys.all_objects AS sp
                    LEFT OUTER JOIN sys.all_sql_modules AS sm
                        ON sm.object_id = sp.object_id
            WHERE   sp.type = 'P'
                    AND (CAST(CASE WHEN sp.is_ms_shipped = 1 THEN 1
                                    WHEN (
                                            SELECT major_id
                                            FROM   sys.extended_properties
                                            WHERE  major_id = sp.object_id
                                                AND minor_id = 0
                                                AND class = 1
                                                AND name = N'microsoft_database_tools_support'
                                        ) IS NOT NULL THEN 1
                                    ELSE 0
                                END AS BIT) = 0))

UNION ALL
SELECT  R.SPECIFIC_SCHEMA,
        R.SPECIFIC_NAME,
        R.ROUTINE_TYPE,
        P.ORDINAL_POSITION,
        P.PARAMETER_MODE,
        P.PARAMETER_NAME,
        P.DATA_TYPE,
        ISNULL(P.CHARACTER_MAXIMUM_LENGTH, 0) AS CHARACTER_MAXIMUM_LENGTH,
        ISNULL(P.NUMERIC_PRECISION, 0) AS NUMERIC_PRECISION,
        ISNULL(P.NUMERIC_SCALE, 0) AS NUMERIC_SCALE,
        ISNULL(P.DATETIME_PRECISION, 0) AS DATETIME_PRECISION,
        P.USER_DEFINED_TYPE_SCHEMA + '.' + P.USER_DEFINED_TYPE_NAME AS USER_DEFINED_TYPE
FROM    INFORMATION_SCHEMA.ROUTINES R
        LEFT OUTER JOIN INFORMATION_SCHEMA.PARAMETERS P
            ON P.SPECIFIC_SCHEMA = R.SPECIFIC_SCHEMA
                AND P.SPECIFIC_NAME = R.SPECIFIC_NAME
WHERE   R.ROUTINE_TYPE = 'FUNCTION'
        AND R.DATA_TYPE = 'TABLE'";
        }

        protected override string SynonymTableSQLSetup()
        {
            return @"
SET NOCOUNT ON;
IF OBJECT_ID('tempdb..#SynonymDetails') IS NOT NULL DROP TABLE #SynonymDetails;
IF OBJECT_ID('tempdb..#SynonymTargets') IS NOT NULL DROP TABLE #SynonymTargets;

-- Synonyms
-- Create the #SynonymDetails temp table structure for later use
SELECT TOP (0)
        sc.name AS SchemaName,
        sn.name AS TableName,
        'SN' AS TableType,
        COLUMNPROPERTY(c.object_id, c.name, 'ordinal') AS Ordinal,
        c.name AS ColumnName,
        c.is_nullable AS IsNullable,
        ISNULL(TYPE_NAME(c.system_type_id), t.name) AS TypeName,
        ISNULL(COLUMNPROPERTY(c.object_id, c.name, 'charmaxlen'), 0) AS MaxLength,
        CAST(ISNULL(CONVERT(TINYINT, CASE WHEN c.system_type_id IN (48, 52, 56, 59, 60, 62, 106, 108, 122, 127) THEN c.precision
                                        END), 0) AS INT) AS Precision,
        ISNULL(CONVERT(NVARCHAR(4000), OBJECT_DEFINITION(c.default_object_id)), '') AS [Default],
        CAST(ISNULL(CONVERT(SMALLINT, CASE WHEN c.system_type_id IN (40, 41, 42, 43, 58, 61) THEN ODBCSCALE(c.system_type_id, c.scale)
                                        END), 0) AS INT) AS DateTimePrecision,
        ISNULL(CONVERT(INT, CASE WHEN c.system_type_id IN (40, 41, 42, 43, 58, 61) THEN NULL
                                    ELSE ODBCSCALE(c.system_type_id, c.scale)
                            END), 0) AS Scale,
        CAST(COLUMNPROPERTY(OBJECT_ID(sn.base_object_name), c.NAME, 'IsIdentity') AS BIT) AS IsIdentity,
        CAST(CASE WHEN COLUMNPROPERTY(OBJECT_ID(QUOTENAME(sc.NAME) + '.' + QUOTENAME(o.NAME)), c.NAME, 'IsIdentity') = 1 THEN 1
                    WHEN COLUMNPROPERTY(OBJECT_ID(QUOTENAME(sc.NAME) + '.' + QUOTENAME(o.NAME)), c.NAME, 'IsComputed') = 1 THEN 1
                    WHEN COLUMNPROPERTY(OBJECT_ID(QUOTENAME(sc.NAME) + '.' + QUOTENAME(o.NAME)), c.NAME, 'GeneratedAlwaysType') > 0 THEN 1
                    WHEN ISNULL(TYPE_NAME(c.system_type_id), t.NAME) = 'TIMESTAMP' THEN 1
                    WHEN ISNULL(TYPE_NAME(c.system_type_id), t.NAME) = 'UNIQUEIDENTIFIER'
                        AND LOWER(ISNULL(CONVERT(NVARCHAR(4000), OBJECT_DEFINITION(c.default_object_id)), '')) LIKE '%newsequentialid%' THEN 1
                    ELSE 0
                END AS BIT) AS IsStoreGenerated,
        CAST(CASE WHEN pk.ORDINAL_POSITION IS NULL THEN 0
                    ELSE 1
                END AS BIT) AS PrimaryKey,
        ISNULL(pk.ORDINAL_POSITION, 0) PrimaryKeyOrdinal,
        CAST(CASE WHEN fk.COLUMN_NAME IS NULL THEN 0
                    ELSE 1
                END AS BIT) AS IsForeignKey
INTO    #SynonymDetails
FROM    sys.synonyms sn
        INNER JOIN sys.COLUMNS c
            ON c.[object_id] = OBJECT_ID(sn.base_object_name)
        INNER JOIN sys.schemas sc
            ON sc.[schema_id] = sn.[schema_id]
        LEFT JOIN sys.types t
            ON c.user_type_id = t.user_type_id
        INNER JOIN sys.objects o
            ON c.[object_id] = o.[object_id]
        LEFT OUTER JOIN (SELECT u.TABLE_SCHEMA,
                                u.TABLE_NAME,
                                u.COLUMN_NAME,
                                u.ORDINAL_POSITION
                            FROM   INFORMATION_SCHEMA.KEY_COLUMN_USAGE u
                                INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                                    ON u.TABLE_SCHEMA = tc.CONSTRAINT_SCHEMA
                                        AND u.TABLE_NAME = tc.TABLE_NAME
                                        AND u.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                            WHERE  CONSTRAINT_TYPE = 'PRIMARY KEY') pk
            ON sc.NAME = pk.TABLE_SCHEMA
                AND sn.name = pk.TABLE_NAME
                AND c.name = pk.COLUMN_NAME
        LEFT OUTER JOIN (SELECT DISTINCT
                                u.TABLE_SCHEMA,
                                u.TABLE_NAME,
                                u.COLUMN_NAME
                            FROM   INFORMATION_SCHEMA.KEY_COLUMN_USAGE u
                                INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                                    ON u.TABLE_SCHEMA = tc.CONSTRAINT_SCHEMA
                                        AND u.TABLE_NAME = tc.TABLE_NAME
                                        AND u.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                            WHERE  CONSTRAINT_TYPE = 'FOREIGN KEY') fk
            ON sc.NAME = fk.TABLE_SCHEMA
                AND sn.name = fk.TABLE_NAME
                AND c.name = fk.COLUMN_NAME;

DECLARE @synonymDetailsQueryTemplate nvarchar(max) = 'USE [@synonmymDatabaseName];
INSERT INTO #SynonymDetails (SchemaName, TableName, TableType, Ordinal, ColumnName, IsNullable, TypeName, [MaxLength], [Precision],
                            [Default], DateTimePrecision, Scale, IsIdentity, IsStoreGenerated, PrimaryKey, PrimaryKeyOrdinal, IsForeignKey)
SELECT st.SynonymSchemaName AS SchemaName,
    st.SynonymName AS TableName,
    ''SN'' AS TableType,
    COLUMNPROPERTY(c.object_id, c.name, ''ordinal'') AS Ordinal,
    c.name AS ColumnName,
    c.is_nullable AS IsNullable,
    ISNULL(TYPE_NAME(c.system_type_id), t.name) AS TypeName,
    ISNULL(COLUMNPROPERTY(c.object_id, c.name, ''charmaxlen''), 0) AS [MaxLength],
    CAST(ISNULL(CONVERT(TINYINT, CASE WHEN c.system_type_id IN (48, 52, 56, 59, 60, 62, 106, 108, 122, 127) THEN c.precision
                                    END), 0) AS INT) AS [Precision],
    ISNULL(CONVERT(NVARCHAR(4000), OBJECT_DEFINITION(c.default_object_id)), '''') AS [Default],
    CAST(ISNULL(CONVERT(SMALLINT, CASE WHEN c.system_type_id IN (40, 41, 42, 43, 58, 61) THEN ODBCSCALE(c.system_type_id, c.scale)
                                    END), 0) AS INT) AS DateTimePrecision,
    ISNULL(CONVERT(INT, CASE WHEN c.system_type_id IN (40, 41, 42, 43, 58, 61) THEN NULL
                                ELSE ODBCSCALE(c.system_type_id, c.scale)
                        END), 0) AS Scale,
    CAST(COLUMNPROPERTY(st.base_object_id, c.name, ''IsIdentity'') AS BIT) AS IsIdentity,
    CAST(CASE WHEN COLUMNPROPERTY(st.base_object_id, c.NAME, ''IsIdentity'') = 1 THEN 1
                WHEN COLUMNPROPERTY(st.base_object_id, c.NAME, ''IsComputed'') = 1 THEN 1
                WHEN ISNULL(TYPE_NAME(c.system_type_id), t.NAME) = ''TIMESTAMP'' THEN 1
                WHEN ISNULL(TYPE_NAME(c.system_type_id), t.NAME) = ''UNIQUEIDENTIFIER''
                    AND LOWER(ISNULL(CONVERT(NVARCHAR(4000), OBJECT_DEFINITION(c.default_object_id)), '''')) LIKE ''%newsequentialid%'' THEN 1
                ELSE 0
            END AS BIT) AS IsStoreGenerated,
    CAST(CASE WHEN pk.ORDINAL_POSITION IS NULL THEN 0
                ELSE 1
            END AS BIT) AS PrimaryKey,
    ISNULL(pk.ORDINAL_POSITION, 0) PrimaryKeyOrdinal,
    CAST(CASE WHEN fk.COLUMN_NAME IS NULL THEN 0
                ELSE 1
            END AS BIT) AS IsForeignKey
FROM    #SynonymTargets st
    INNER JOIN sys.columns c
        ON c.[object_id] = st.base_object_id
    LEFT JOIN sys.types t
        ON c.user_type_id = t.user_type_id
    INNER JOIN sys.objects o
        ON c.[object_id] = o.[object_id]
    LEFT OUTER JOIN (
                        SELECT u.TABLE_SCHEMA,
                            u.TABLE_NAME,
                            u.COLUMN_NAME,
                            u.ORDINAL_POSITION
                        FROM   INFORMATION_SCHEMA.KEY_COLUMN_USAGE u
                            INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                                ON u.TABLE_SCHEMA = tc.CONSTRAINT_SCHEMA
                                    AND u.TABLE_NAME = tc.TABLE_NAME
                                    AND u.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                        WHERE  CONSTRAINT_TYPE = ''PRIMARY KEY''
                    ) pk
        ON st.SchemaName = pk.TABLE_SCHEMA
            AND st.ObjectName = pk.TABLE_NAME
            AND c.name = pk.COLUMN_NAME
    LEFT OUTER JOIN (
                        SELECT DISTINCT
                            u.TABLE_SCHEMA,
                            u.TABLE_NAME,
                            u.COLUMN_NAME
                        FROM   INFORMATION_SCHEMA.KEY_COLUMN_USAGE u
                            INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                                ON u.TABLE_SCHEMA = tc.CONSTRAINT_SCHEMA
                                    AND u.TABLE_NAME = tc.TABLE_NAME
                                    AND u.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                        WHERE  CONSTRAINT_TYPE = ''FOREIGN KEY''
                    ) fk
        ON st.SchemaName = fk.TABLE_SCHEMA
            AND st.ObjectName = fk.TABLE_NAME
            AND c.name = fk.COLUMN_NAME
WHERE st.DatabaseName = @synonmymDatabaseName;
'

-- Pull details about the synonym target from each database being referenced
SELECT  sc.name AS SynonymSchemaName,
        sn.name AS SynonymName,
        sn.object_id,
        sn.base_object_name,
        OBJECT_ID(sn.base_object_name) AS base_object_id,
        PARSENAME(sn.base_object_name, 1) AS ObjectName,
        ISNULL(PARSENAME(sn.base_object_name, 2), sc.name) AS SchemaName,
        ISNULL(PARSENAME(sn.base_object_name, 3), DB_NAME()) AS DatabaseName,
        PARSENAME(sn.base_object_name, 4) AS ServerName
INTO    #SynonymTargets
FROM    sys.synonyms sn
        INNER JOIN sys.schemas sc
            ON sc.schema_id = sn.schema_id
WHERE   ISNULL(PARSENAME(sn.base_object_name, 4), @@SERVERNAME) = @@SERVERNAME; -- Only populate info from current server

-- Loop through synonyms and populate #SynonymDetails
DECLARE @synonmymDatabaseName sysname = (SELECT TOP (1) DatabaseName FROM #SynonymTargets)
DECLARE @synonmymDetailsSelect nvarchar(max)
WHILE (@synonmymDatabaseName IS NOT NULL)
BEGIN
SET @synonmymDetailsSelect = REPLACE(@synonymDetailsQueryTemplate, '[@synonmymDatabaseName]', '[' + @synonmymDatabaseName + ']')
--SELECT @synonmymDetailsSelect
EXEC sp_executesql @stmt=@synonmymDetailsSelect, @params=N'@synonmymDatabaseName sysname', @synonmymDatabaseName=@synonmymDatabaseName
DELETE FROM #SynonymTargets WHERE DatabaseName = @synonmymDatabaseName
SET @synonmymDatabaseName = (SELECT TOP (1) DatabaseName FROM #SynonymTargets)
END
SET NOCOUNT OFF;
";
        }

        protected override string SynonymTableSQL()
        {
            return @"
UNION
-- Synonyms
SELECT SchemaName, TableName, TableType, Ordinal, ColumnName, IsNullable, TypeName, [MaxLength], [Precision],
    [Default], DateTimePrecision, Scale, IsIdentity, IsStoreGenerated, PrimaryKey, PrimaryKeyOrdinal, IsForeignKey FROM #SynonymDetails";
        }

        protected override string SynonymForeignKeySQLSetup()
        {
            return @"
SET NOCOUNT ON;
IF OBJECT_ID('tempdb..#SynonymFkDetails') IS NOT NULL DROP TABLE #SynonymFkDetails;
IF OBJECT_ID('tempdb..#SynonymTargets') IS NOT NULL DROP TABLE #SynonymTargets;

-- Create the #SynonymFkDetails temp table structure for later use
SELECT  FK.name AS FK_Table,
        FkCol.name AS FK_Column,
        PK.name AS PK_Table,
        PkCol.name AS PK_Column,
        OBJECT_NAME(f.object_id) AS Constraint_Name,
        SCHEMA_NAME(FK.schema_id) AS fkSchema,
        SCHEMA_NAME(PK.schema_id) AS pkSchema,
        PkCol.name AS primarykey,
        k.constraint_column_id AS ORDINAL_POSITION,
        CASE WHEN f.delete_referential_action = 1 THEN 1 ELSE 0 END as CascadeOnDelete,
        f.is_disabled AS IsNotEnforced
INTO	#SynonymFkDetails
FROM    sys.objects AS PK
        INNER JOIN sys.foreign_keys AS f
            INNER JOIN sys.foreign_key_columns AS k
                ON k.constraint_object_id = f.object_id
            INNER JOIN sys.indexes AS i
                ON f.referenced_object_id = i.object_id
                    AND f.key_index_id = i.index_id
            ON PK.object_id = f.referenced_object_id
        INNER JOIN sys.objects AS FK
            ON f.parent_object_id = FK.object_id
        INNER JOIN sys.columns AS PkCol
            ON f.referenced_object_id = PkCol.object_id
                AND k.referenced_column_id = PkCol.column_id
        INNER JOIN sys.columns AS FkCol
            ON f.parent_object_id = FkCol.object_id
                AND k.parent_column_id = FkCol.column_id
ORDER BY FK_Table, FK_Column

-- Get all databases referenced by synonyms.
SELECT DISTINCT PARSENAME(sn.base_object_name, 3) AS DatabaseName
INTO #SynonymTargets
FROM sys.synonyms sn
WHERE PARSENAME(sn.base_object_name, 3) != DB_NAME()
AND ISNULL(PARSENAME(sn.base_object_name, 4), @@SERVERNAME) = @@SERVERNAME -- Only populate info from current server
ORDER BY PARSENAME(sn.base_object_name, 3)

-- Create a query to execute for each referenced database
DECLARE @synonymFkDetailsQuery nvarchar(max) =
'
INSERT INTO #SynonymFkDetails (FK_Table, FK_Column, PK_Table, PK_Column, Constraint_Name, fkSchema, pkSchema, primarykey, ORDINAL_POSITION,
                                CascadeOnDelete, IsNotEnforced)
SELECT  FK.name AS FK_Table,
        FkCol.name AS FK_Column,
        PK.name AS PK_Table,
        PkCol.name AS PK_Column,
        OBJECT_NAME(f.object_id) AS Constraint_Name,
        SCHEMA_NAME(FK.schema_id) AS fkSchema,
        SCHEMA_NAME(PK.schema_id) AS pkSchema,
        PkCol.name AS primarykey,
        k.constraint_column_id AS ORDINAL_POSITION,
        CASE WHEN f.delete_referential_action = 1 THEN 1 ELSE 0 END as CascadeOnDelete,
        f.is_disabled AS IsNotEnforced
FROM    sys.objects AS PK
        INNER JOIN sys.foreign_keys AS f
            INNER JOIN sys.foreign_key_columns AS k
                ON k.constraint_object_id = f.object_id
            INNER JOIN sys.indexes AS i
                ON f.referenced_object_id = i.object_id
                    AND f.key_index_id = i.index_id
            ON PK.object_id = f.referenced_object_id
        INNER JOIN sys.objects AS FK
            ON f.parent_object_id = FK.object_id
        INNER JOIN sys.columns AS PkCol
            ON f.referenced_object_id = PkCol.object_id
                AND k.referenced_column_id = PkCol.column_id
        INNER JOIN sys.columns AS FkCol
            ON f.parent_object_id = FkCol.object_id
                AND k.parent_column_id = FkCol.column_id
ORDER BY FK_Table, FK_Column;
'

-- Loop through referenced databases and populate #SynonymFkDetails
DECLARE @synonmymDatabaseName sysname = (SELECT TOP (1) DatabaseName FROM #SynonymTargets)
DECLARE @synonymFkDetailsQueryWithDb nvarchar(max)
WHILE (@synonmymDatabaseName IS NOT NULL)
BEGIN
	SET @synonymFkDetailsQueryWithDb = 'USE [' + @synonmymDatabaseName + '] ' + @synonymFkDetailsQuery
    EXEC sp_executesql @stmt=@synonymFkDetailsQueryWithDb
    DELETE FROM #SynonymTargets WHERE DatabaseName = @synonmymDatabaseName
    SET @synonmymDatabaseName = (SELECT TOP (1) DatabaseName FROM #SynonymTargets)
END

SET NOCOUNT OFF;
";
        }

        protected override string SynonymForeignKeySQL()
        {
            return @"
UNION
-- Synonyms
SELECT FK_Table, FK_Column, PK_Table, PK_Column, Constraint_Name, fkSchema, pkSchema, primarykey, ORDINAL_POSITION,
        CascadeOnDelete, IsNotEnforced FROM #SynonymFkDetails";
        }

        protected override string SynonymStoredProcedureSQLSetup()
        {
            return @"
SET NOCOUNT ON;
IF OBJECT_ID('tempdb..#SynonymStoredProcedureDetails') IS NOT NULL DROP TABLE #SynonymStoredProcedureDetails;
IF OBJECT_ID('tempdb..#SynonymTargets') IS NOT NULL DROP TABLE #SynonymTargets;

-- Create the ##SynonymStoredProcedureDetails temp table structure for later use
SELECT  TOP (0) R.SPECIFIC_SCHEMA,
        R.SPECIFIC_NAME,
        R.ROUTINE_TYPE,
        P.ORDINAL_POSITION,
        P.PARAMETER_MODE,
        P.PARAMETER_NAME,
        P.DATA_TYPE,
        ISNULL(P.CHARACTER_MAXIMUM_LENGTH, 0) AS CHARACTER_MAXIMUM_LENGTH,
        ISNULL(P.NUMERIC_PRECISION, 0) AS NUMERIC_PRECISION,
        ISNULL(P.NUMERIC_SCALE, 0) AS NUMERIC_SCALE,
        ISNULL(P.DATETIME_PRECISION, 0) AS DATETIME_PRECISION,
        P.USER_DEFINED_TYPE_SCHEMA + '.' + P.USER_DEFINED_TYPE_NAME AS USER_DEFINED_TYPE
INTO	#SynonymStoredProcedureDetails
FROM    INFORMATION_SCHEMA.ROUTINES R
        LEFT OUTER JOIN INFORMATION_SCHEMA.PARAMETERS P
            ON P.SPECIFIC_SCHEMA = R.SPECIFIC_SCHEMA
                AND P.SPECIFIC_NAME = R.SPECIFIC_NAME
WHERE   R.ROUTINE_TYPE = 'PROCEDURE'
        AND (
                P.IS_RESULT = 'NO'
                OR P.IS_RESULT IS NULL
            )
        AND R.SPECIFIC_SCHEMA + R.SPECIFIC_NAME IN (
            SELECT  SCHEMA_NAME(sp.schema_id) + sp.name
            FROM    sys.all_objects AS sp
                    LEFT OUTER JOIN sys.all_sql_modules AS sm
                        ON sm.object_id = sp.object_id
            WHERE   sp.type = 'P'
                    AND (CAST(CASE WHEN sp.is_ms_shipped = 1 THEN 1
                                    WHEN (
                                            SELECT major_id
                                            FROM   sys.extended_properties
                                            WHERE  major_id = sp.object_id
                                                AND minor_id = 0
                                                AND class = 1
                                                AND name = N'microsoft_database_tools_support'
                                        ) IS NOT NULL THEN 1
                                    ELSE 0
                                END AS BIT) = 0))

-- Get all databases referenced by synonyms.
SELECT DISTINCT PARSENAME(sn.base_object_name, 3) AS DatabaseName
INTO #SynonymTargets
FROM sys.synonyms sn
WHERE PARSENAME(sn.base_object_name, 3) != DB_NAME()
AND ISNULL(PARSENAME(sn.base_object_name, 4), @@SERVERNAME) = @@SERVERNAME -- Only populate info from current server
ORDER BY PARSENAME(sn.base_object_name, 3)

-- Create a query to execute for each referenced database
DECLARE @synonymStoredProcedureDetailsQuery nvarchar(max) =
'
INSERT INTO #SynonymStoredProcedureDetails (SPECIFIC_SCHEMA, SPECIFIC_NAME, ROUTINE_TYPE, ORDINAL_POSITION, PARAMETER_MODE, PARAMETER_NAME, DATA_TYPE
											, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE, DATETIME_PRECISION, USER_DEFINED_TYPE)
SELECT  R.SPECIFIC_SCHEMA,
        R.SPECIFIC_NAME,
        R.ROUTINE_TYPE,
        P.ORDINAL_POSITION,
        P.PARAMETER_MODE,
        P.PARAMETER_NAME,
        P.DATA_TYPE,
        ISNULL(P.CHARACTER_MAXIMUM_LENGTH, 0) AS CHARACTER_MAXIMUM_LENGTH,
        ISNULL(P.NUMERIC_PRECISION, 0) AS NUMERIC_PRECISION,
        ISNULL(P.NUMERIC_SCALE, 0) AS NUMERIC_SCALE,
        ISNULL(P.DATETIME_PRECISION, 0) AS DATETIME_PRECISION,
        P.USER_DEFINED_TYPE_SCHEMA + ''.'' + P.USER_DEFINED_TYPE_NAME AS USER_DEFINED_TYPE
FROM    INFORMATION_SCHEMA.ROUTINES R
        LEFT OUTER JOIN INFORMATION_SCHEMA.PARAMETERS P
            ON P.SPECIFIC_SCHEMA = R.SPECIFIC_SCHEMA
                AND P.SPECIFIC_NAME = R.SPECIFIC_NAME
WHERE   R.ROUTINE_TYPE = ''PROCEDURE''
        AND (
                P.IS_RESULT = ''NO''
                OR P.IS_RESULT IS NULL
            )
        AND R.SPECIFIC_SCHEMA + R.SPECIFIC_NAME IN (
            SELECT  SCHEMA_NAME(sp.schema_id) + sp.name
            FROM    sys.all_objects AS sp
                    LEFT OUTER JOIN sys.all_sql_modules AS sm
                        ON sm.object_id = sp.object_id
            WHERE   sp.type = ''P''
                    AND (CAST(CASE WHEN sp.is_ms_shipped = 1 THEN 1
                                    WHEN (
                                            SELECT major_id
                                            FROM   sys.extended_properties
                                            WHERE  major_id = sp.object_id
                                                AND minor_id = 0
                                                AND class = 1
                                                AND name = N''microsoft_database_tools_support''
                                        ) IS NOT NULL THEN 1
                                    ELSE 0
                                END AS BIT) = 0))

UNION ALL
SELECT  R.SPECIFIC_SCHEMA,
        R.SPECIFIC_NAME,
        R.ROUTINE_TYPE,
        P.ORDINAL_POSITION,
        P.PARAMETER_MODE,
        P.PARAMETER_NAME,
        P.DATA_TYPE,
        ISNULL(P.CHARACTER_MAXIMUM_LENGTH, 0) AS CHARACTER_MAXIMUM_LENGTH,
        ISNULL(P.NUMERIC_PRECISION, 0) AS NUMERIC_PRECISION,
        ISNULL(P.NUMERIC_SCALE, 0) AS NUMERIC_SCALE,
        ISNULL(P.DATETIME_PRECISION, 0) AS DATETIME_PRECISION,
        P.USER_DEFINED_TYPE_SCHEMA + ''.'' + P.USER_DEFINED_TYPE_NAME AS USER_DEFINED_TYPE
FROM    INFORMATION_SCHEMA.ROUTINES R
        LEFT OUTER JOIN INFORMATION_SCHEMA.PARAMETERS P
            ON P.SPECIFIC_SCHEMA = R.SPECIFIC_SCHEMA
                AND P.SPECIFIC_NAME = R.SPECIFIC_NAME
WHERE   R.ROUTINE_TYPE = ''FUNCTION''
        AND R.DATA_TYPE = ''TABLE''
ORDER BY R.SPECIFIC_SCHEMA,
        R.SPECIFIC_NAME,
        P.ORDINAL_POSITION
'

-- Loop through referenced databases and populate #SynonymStoredProcedureDetails
DECLARE @synonmymDatabaseName sysname = (SELECT TOP (1) DatabaseName FROM #SynonymTargets)
DECLARE @synonymStoredProcedureDetailsQueryWithDb nvarchar(max)
WHILE (@synonmymDatabaseName IS NOT NULL)
BEGIN
	SET @synonymStoredProcedureDetailsQueryWithDb = 'USE [' + @synonmymDatabaseName + '] ' + @synonymStoredProcedureDetailsQuery
    EXEC sp_executesql @stmt=@synonymStoredProcedureDetailsQueryWithDb
    DELETE FROM #SynonymTargets WHERE DatabaseName = @synonmymDatabaseName
    SET @synonmymDatabaseName = (SELECT TOP (1) DatabaseName FROM #SynonymTargets)
END

SET NOCOUNT OFF;
";
        }

        protected override string SynonymStoredProcedureSQL()
        {
            return @"
UNION
-- Synonyms
SELECT SPECIFIC_SCHEMA, SPECIFIC_NAME, ROUTINE_TYPE, ORDINAL_POSITION, PARAMETER_MODE, PARAMETER_NAME, DATA_TYPE
	, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE, DATETIME_PRECISION, USER_DEFINED_TYPE FROM #SynonymStoredProcedureDetails";
        }

        protected override string SpecialQueryFlags()
        {
            if (Settings.IncludeQueryTraceOn9481Flag)
                return @"
OPTION (QUERYTRACEON 9481)";

            return string.Empty;
        }

        private bool IsAzure()
        {
            if (string.IsNullOrEmpty(_sqlDatabaseEdition))
            {
                var cmd = GetCmd();
                if (cmd == null)
                    return false;

                cmd.CommandText = @"
SELECT  SERVERPROPERTY('Edition') AS Edition,
    CASE SERVERPROPERTY('EngineEdition')
        WHEN 1 THEN 'Personal'
        WHEN 2 THEN 'Standard'
        WHEN 3 THEN 'Enterprise'
        WHEN 4 THEN 'Express'
        WHEN 5 THEN 'Azure'
        ELSE 'Unknown'
    END AS EngineEdition";

                cmd.CommandTimeout = Settings.CommandTimeout;

                using (var rdr = cmd.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        _sqlDatabaseEdition = rdr["Edition"].ToString();
                        _sqlDatabaseEngineEdition = rdr["EngineEdition"].ToString();

                        if (Settings.IncludeConnectionSettingComments)
                        {
                            WriteLine("// Database Edition       : " + _sqlDatabaseEdition);
                            WriteLine("// Database Engine Edition: " + _sqlDatabaseEngineEdition);
                            WriteLine("");
                        }
                    }
                }
            }

            return _sqlDatabaseEngineEdition == "Azure";
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
                    sysType = Settings.DisableGeographyTypes ? "" : "System.Data.Entity.Spatial.DbGeography";
                    break;
                case "geometry":
                    sysType = Settings.DisableGeographyTypes ? "" : "System.Data.Entity.Spatial.DbGeometry";
                    break;
            }
            return sysType;
        }
    }
}