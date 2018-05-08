using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Generator.DatabaseReaders
{
    public abstract class DatabaseReader
    {
        protected abstract string TableSQL();
        protected abstract string ForeignKeySQL();
        protected abstract string ExtendedPropertySQL(DbConnection conn);
        protected abstract string DoesExtendedPropertyTableExistSQL();
        protected abstract string IndexSQL();
        public abstract bool CanReadStoredProcedures();
        protected abstract string StoredProcedureSQL(DbConnection conn);

        // Synonym
        protected abstract string SynonymTableSQLSetup();
        protected abstract string SynonymTableSQL();
        protected abstract string SynonymForeignKeySQLSetup();
        protected abstract string SynonymForeignKeySQL();
        protected abstract string SynonymStoredProcedureSQLSetup();
        protected abstract string SynonymStoredProcedureSQL();

        // Database specific flags
        protected abstract string SpecialQueryFlags();

        // Type converters
        protected abstract string GetStoredProcedureParameterDbType(string sqlType);
        protected abstract string GetPropertyType(string dbType);

        // Any special setup required
        public abstract void Init();


        private readonly DbProviderFactory _factory;
        private readonly GeneratedTextTransformation _generatedTextTransformation;
        public bool IncludeSchema { get; protected set; }
        public bool DoNotSpecifySizeForMaxLength { get; protected set; }

        private static readonly Regex ReservedColumnNames = new Regex("^(event|Equals|GetHashCode|GetType|ToString|repo|Save|IsNew|Insert|Update|Delete|Exists|SingleOrDefault|Single|First|FirstOrDefault|Fetch|Page|Query)$", RegexOptions.Compiled);

        public static readonly List<string> ReservedKeywords = new List<string>
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
            "char", "checked", "class", "const", "continue", "decimal", "default",
            "delegate", "do", "double", "else", "enum", "event", "explicit",
            "extern", "false", "finally", "fixed", "float", "for", "foreach",
            "goto", "if", "implicit", "in", "int", "interface", "internal", "is",
            "lock", "long", "namespace", "new", "null", "object", "operator", "out",
            "override", "params", "private", "protected", "public", "readonly",
            "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc",
            "static", "string", "struct", "switch", "this", "throw", "true", "try",
            "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
            "virtual", "volatile", "void", "while"
        };

        protected DatabaseReader(DbProviderFactory factory, GeneratedTextTransformation generatedTextTransformation)
        {
            _factory = factory;
            _generatedTextTransformation = generatedTextTransformation;
            IncludeSchema = true;
            DoNotSpecifySizeForMaxLength = false;
        }

        protected DbCommand GetCmd(DbConnection connection)
        {
            if (connection == null)
                return null;

            var cmd = _factory.CreateCommand();
            if (cmd == null)
                return null;

            cmd.Connection = connection;
            cmd.CommandTimeout = Settings.CommandTimeout;

            return cmd;
        }

        public Tables ReadSchema()
        {
            var result = new Tables();
            using (var conn = _factory.CreateConnection())
            {
                if (conn == null)
                    return result;

                conn.ConnectionString = Settings.ConnectionString;
                conn.Open();

                var cmd = GetCmd(conn);
                if (cmd == null)
                    return result;

                if (Settings.IncludeSynonyms)
                    cmd.CommandText = SynonymTableSQLSetup() + TableSQL() + SynonymTableSQL() + SpecialQueryFlags();
                else
                    cmd.CommandText = TableSQL() + SpecialQueryFlags();

                using (var rdr = cmd.ExecuteReader())
                {
                    var lastTable = string.Empty;
                    Table table = null;
                    while (rdr.Read())
                    {
                        var schema = rdr["SchemaName"].ToString().Trim();
                        if (IsFilterExcluded(Settings.SchemaFilterExclude, Settings.SchemaFilterInclude, schema))
                            continue;

                        var tableName = rdr["TableName"].ToString().Trim();
                        if (IsFilterExcluded(Settings.TableFilterExclude, Settings.TableFilterInclude, tableName))
                            continue;

                        if (lastTable != tableName || table == null)
                        {
                            // The data from the database is not sorted
                            table = result.Find(x => x.Name == tableName && x.Schema == schema);
                            if (table == null)
                            {
                                table = new Table
                                {
                                    Name = tableName,
                                    Schema = schema,
                                    IsView = string.Compare(rdr["TableType"].ToString().Trim(), "View", StringComparison.OrdinalIgnoreCase) == 0,

                                    // Will be set later
                                    HasForeignKey = false,
                                    HasNullableColumns = false
                                };

                                if (!Settings.IncludeViews && table.IsView)
                                    continue;

                                tableName = Settings.TableRename(tableName, schema, table.IsView);
                                if (IsFilterExcluded(Settings.TableFilterExclude, null, tableName)) // Retest exclusion filter after table rename
                                    continue;

                                // Handle table names with underscores - singularise just the last word
                                table.ClassName = Inflector.MakeSingular(CleanUp(tableName));
                                var titleCase = (Settings.UsePascalCase ? Inflector.ToTitleCase(table.ClassName) : table.ClassName)
                                    .Replace(" ", string.Empty)
                                    .Replace("$", string.Empty)
                                    .Replace(".", string.Empty);

                                table.NameHumanCase = titleCase;

                                if (Settings.PrependSchemaName && string.Compare(table.Schema, "dbo", StringComparison.OrdinalIgnoreCase) != 0)
                                    table.NameHumanCase = table.Schema + "_" + table.NameHumanCase;

                                // Check for table or C# name clashes
                                if (ReservedKeywords.Contains(table.NameHumanCase) ||
                                    (Settings.UsePascalCase && result.Find(x => x.NameHumanCase == table.NameHumanCase) != null))
                                {
                                    table.NameHumanCase += "1";
                                }

                                if (!Settings.TableFilter(table))
                                    continue;

                                result.Add(table);
                            }
                        }

                        var col = CreateColumn(rdr, table);
                        if (col != null)
                            table.Columns.Add(col);
                    }
                }

                // Check for property name clashes in columns
                foreach (var c in result.SelectMany(tbl => tbl.Columns.Where(c => tbl.Columns.FindAll(x => x.NameHumanCase == c.NameHumanCase).Count > 1)))
                {
                    var n = 1;
                    var original = c.NameHumanCase;
                    c.NameHumanCase = original + n++;

                    // Check if the above resolved the name clash, if not, use next value
                    while (c.ParentTable.Columns.Count(c2 => c2.NameHumanCase == c.NameHumanCase) > 1)
                        c.NameHumanCase = original + n++;
                }

                if (Settings.IncludeExtendedPropertyComments != CommentsStyle.None)
                    ReadExtendedProperties(conn, result);

                ReadIndexes(conn, result);

                conn.Close();
            }

            return result;
        }

        public List<ForeignKey> ReadForeignKeys()
        {
            var result = new List<ForeignKey>();
            using (var conn = _factory.CreateConnection())
            {
                if (conn == null)
                    return result;

                conn.ConnectionString = Settings.ConnectionString;
                conn.Open();

                var cmd = GetCmd(conn);
                if (cmd == null)
                    return result;

                if (Settings.IncludeSynonyms)
                    cmd.CommandText = SynonymForeignKeySQLSetup() + ForeignKeySQL() + SynonymForeignKeySQL() + SpecialQueryFlags();
                else
                    cmd.CommandText = ForeignKeySQL() + SpecialQueryFlags();

                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        var fkTableName = rdr["FK_Table"].ToString();
                        var fkSchema = rdr["fkSchema"].ToString();
                        var pkTableName = rdr["PK_Table"].ToString();
                        var pkSchema = rdr["pkSchema"].ToString();
                        var fkColumn = rdr["FK_Column"].ToString();
                        var pkColumn = rdr["PK_Column"].ToString();
                        var constraintName = rdr["Constraint_Name"].ToString();
                        var ordinal = (int) rdr["ORDINAL_POSITION"];
                        var cascadeOnDelete = ((int) rdr["CascadeOnDelete"]) == 1;
                        var isNotEnforced = (bool) rdr["IsNotEnforced"];

                        var fkTableNameFiltered = Settings.TableRename(fkTableName, fkSchema, false);
                        var pkTableNameFiltered = Settings.TableRename(pkTableName, pkSchema, false);

                        var fk = new ForeignKey(fkTableName, fkSchema, pkTableName, pkSchema, fkColumn, pkColumn,
                            constraintName, fkTableNameFiltered, pkTableNameFiltered, ordinal, cascadeOnDelete,
                            isNotEnforced);

                        var filteredFk = Settings.ForeignKeyFilter(fk);
                        if (filteredFk != null)
                            result.Add(filteredFk);
                    }
                }
            }

            return result;
        }

        // When a table has no primary keys, all the NOT NULL columns are set as being the primary key.
        // This function reads the unique indexes for a table, and correctly sets the columns being used as primary keys.
        private void ReadIndexes(DbConnection conn, Tables tables)
        {
            var cmd = GetCmd(conn);
            if (cmd == null)
                return;

            cmd.CommandText = IndexSQL() + SpecialQueryFlags();

            var list = new List<Index>();
            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    var index = new Index
                    {
                        Schema = rdr["TableSchema"].ToString().Trim(),
                        TableName = rdr["TableName"].ToString().Trim(),
                        IndexName = rdr["IndexName"].ToString().Trim(),
                        KeyOrdinal = (byte) rdr["KeyOrdinal"],
                        ColumnName = rdr["ColumnName"].ToString().Trim(),
                        ColumnCount = (int) rdr["ColumnCount"],
                        IsUnique = (bool) rdr["IsUnique"],
                        IsPrimaryKey = (bool) rdr["IsPrimaryKey"],
                        IsUniqueConstraint = (bool) rdr["IsUniqueConstraint"],
                        IsClustered = ((int) rdr["IsClustered"]) == 1
                    };

                    list.Add(index);
                }
            }

            Table t = null;
            var indexTables = list
                .Select(x => new { x.Schema, x.TableName })
                .Distinct();

            foreach (var indexTable in indexTables)
            {
                // Lookup table
                if (t == null || t.Name != indexTable.TableName || t.Schema != indexTable.Schema)
                    t = tables.Find(x => x.Name == indexTable.TableName && x.Schema == indexTable.Schema);

                if (t == null)
                    continue;

                // Find indexes for table
                var indexes =
                    list.Where(x => x.Schema == indexTable.Schema && x.TableName == indexTable.TableName)
                        .OrderBy(o => o.ColumnCount)
                        .ThenBy(o => o.KeyOrdinal)
                        .ToList();

                // Set index on column
                foreach (var index in indexes)
                {
                    var col = t.Columns.Find(x => x.Name == index.ColumnName);
                    if (col == null)
                        continue;

                    col.Indexes.Add(index);

                    col.IsPrimaryKey = col.IsPrimaryKey || index.IsPrimaryKey;
                    col.IsUniqueConstraint = col.IsUniqueConstraint || (index.IsUniqueConstraint && index.ColumnCount == 1);
                    col.IsUnique = col.IsUnique || (index.IsUnique && index.ColumnCount == 1);
                }

                // Check if table has any primary keys
                if (t.PrimaryKeys.Any())
                    continue; // Already has a primary key, ignore this unique index / constraint

                // Find unique indexes for table
                var uniqueIndexKeys = indexes
                    .Where(x => x.IsUnique || x.IsPrimaryKey || x.IsUniqueConstraint)
                    .OrderBy(o => o.ColumnCount)
                    .ThenBy(o => o.KeyOrdinal);

                // Process only the first index with the lowest unique column count
                string indexName = null;
                foreach (var key in uniqueIndexKeys)
                {
                    if (indexName == null)
                        indexName = key.IndexName;

                    if (indexName != key.IndexName)
                        break; // First unique index with lowest column count has been processed, exit.

                    var col = t.Columns.Find(x => x.Name == key.ColumnName);
                    if (col != null && !col.IsNullable && !col.Hidden && !col.IsPrimaryKey)
                    {
                        col.IsPrimaryKey = true;
                        col.IsUniqueConstraint = true;
                        col.IsUnique = true;
                        col.UniqueIndexName = indexName;
                    }
                }
            }
        }

        private void ReadExtendedProperties(DbConnection conn, Tables tables)
        {
            var cmd = GetCmd(conn);
            if (cmd == null)
                return;

            var extendedPropertySQL = ExtendedPropertySQL(conn);
            if (string.IsNullOrEmpty(extendedPropertySQL))
                return;

            // Check if any SQL is returned. If so, run it. (Specific to SqlCE)
            var doesExtendedPropertyTableExistSQL = DoesExtendedPropertyTableExistSQL();
            if (!string.IsNullOrEmpty(doesExtendedPropertyTableExistSQL))
            {
                cmd.CommandText = doesExtendedPropertyTableExistSQL;
                var obj = cmd.ExecuteScalar();
                if (obj == null)
                    return; // No extended properties table
            }

            cmd.CommandText = extendedPropertySQL + SpecialQueryFlags();

            var commentsInSummaryBlock = Settings.IncludeExtendedPropertyComments == CommentsStyle.InSummaryBlock;
            var multiLine = new Regex("[\r\n]+", RegexOptions.Compiled);
            var whitespace = new Regex("\\s+", RegexOptions.Compiled);

            using (var rdr = cmd.ExecuteReader())
            {
                Table t = null;
                while (rdr.Read())
                {
                    var extendedProperty = rdr["property"].ToString().Trim();
                    if (string.IsNullOrEmpty(extendedProperty))
                        continue;

                    var schema = rdr["schema"].ToString().Trim();
                    var tableName = rdr["table"].ToString().Trim();
                    if (t == null || t.Name != tableName || t.Schema != schema)
                        t = tables.Find(x => x.Name == tableName && x.Schema == schema);

                    if (t == null)
                        continue;

                    var column = rdr["column"].ToString().Trim();
                    if (string.IsNullOrEmpty(column))
                    {
                        // Table level extended comment
                        t.ExtendedProperty.Add(multiLine.Replace(extendedProperty, "\r\n    /// "));
                        continue;
                    }

                    // Column level extended comment
                    var col = t.Columns.Find(x => x.Name == column);
                    if (col == null)
                        continue;

                    if (commentsInSummaryBlock)
                        col.ExtendedProperty = multiLine.Replace(extendedProperty, "\r\n        /// ");
                    else
                        col.ExtendedProperty = whitespace.Replace(multiLine.Replace(extendedProperty, " "), " ");
                }
            }
        }

        public List<StoredProcedure> ReadStoredProcs()
        {
            var result = new List<StoredProcedure>();

            using (var conn = _factory.CreateConnection())
            {
                if (conn == null)
                    return result;

                conn.ConnectionString = Settings.ConnectionString;
                conn.Open();

                var storedProcedureSQL = StoredProcedureSQL(conn);
                if (string.IsNullOrEmpty(storedProcedureSQL))
                    return result;

                var cmd = GetCmd(conn);
                if (cmd == null)
                    return result;

                if (Settings.IncludeSynonyms)
                    cmd.CommandText = SynonymStoredProcedureSQLSetup() + storedProcedureSQL + SynonymStoredProcedureSQL() + SpecialQueryFlags();
                else
                    cmd.CommandText = storedProcedureSQL + SpecialQueryFlags();

                using (var rdr = cmd.ExecuteReader())
                {
                    var lastSp = string.Empty;
                    StoredProcedure sp = null;
                    while (rdr.Read())
                    {
                        var spType = rdr["ROUTINE_TYPE"].ToString().Trim().ToUpper();
                        var isTvf = (spType == "FUNCTION");
                        if (isTvf && !Settings.IncludeTableValuedFunctions)
                            continue;

                        var schema = rdr["SPECIFIC_SCHEMA"].ToString().Trim();
                        if (IsFilterExcluded(Settings.SchemaFilterExclude, Settings.SchemaFilterInclude, schema))
                            continue;

                        var spName = rdr["SPECIFIC_NAME"].ToString().Trim();
                        var fullname = string.Format("{0}.{1}", schema, spName);

                        if (Settings.StoredProcedureFilterExclude != null &&
                            (Settings.StoredProcedureFilterExclude.IsMatch(spName) ||
                             Settings.StoredProcedureFilterExclude.IsMatch(fullname)))
                            continue;

                        if (lastSp != fullname || sp == null)
                        {
                            lastSp = fullname;

                            sp = new StoredProcedure
                            {
                                IsTVF = isTvf,
                                Name = spName,
                                NameHumanCase = (Settings.UsePascalCase ? Inflector.ToTitleCase(spName) : spName)
                                    .Replace(" ", string.Empty)
                                    .Replace("$", string.Empty),
                                Schema = schema
                            };

                            sp.NameHumanCase = CleanUp(sp.NameHumanCase);
                            if ((string.Compare(schema, "dbo", StringComparison.OrdinalIgnoreCase) != 0) && Settings.PrependSchemaName)
                                sp.NameHumanCase = schema + "_" + sp.NameHumanCase;

                            sp.NameHumanCase = Settings.StoredProcedureRename(sp);
                            fullname = string.Format("{0}.{1}", schema, sp.NameHumanCase);

                            if (Settings.StoredProcedureFilterExclude != null &&
                                (Settings.StoredProcedureFilterExclude.IsMatch(sp.NameHumanCase) ||
                                 Settings.StoredProcedureFilterExclude.IsMatch(fullname)))
                                continue;

                            if (Settings.StoredProcedureFilterInclude != null &&
                                !(Settings.StoredProcedureFilterInclude.IsMatch(sp.NameHumanCase) ||
                                  Settings.StoredProcedureFilterInclude.IsMatch(fullname)))
                                continue;

                            if (!Settings.StoredProcedureFilter(sp))
                                continue;

                            result.Add(sp);
                        }

                        if (rdr["DATA_TYPE"] == null || rdr["DATA_TYPE"] == DBNull.Value)
                            continue;

                        var typename = rdr["DATA_TYPE"].ToString().Trim().ToLower();
                        var scale = (int) rdr["NUMERIC_SCALE"];
                        var precision = (int) ((byte) rdr["NUMERIC_PRECISION"]);
                        var parameterMode = rdr["PARAMETER_MODE"].ToString().Trim().ToUpper();

                        var param = new StoredProcedureParameter
                        {
                            Ordinal = (int) rdr["ORDINAL_POSITION"],
                            Mode = parameterMode == "IN" ? StoredProcedureParameterMode.In : StoredProcedureParameterMode.InOut,
                            Name = rdr["PARAMETER_NAME"].ToString().Trim(),
                            SqlDbType = GetStoredProcedureParameterDbType(typename),
                            PropertyType = GetPropertyType(typename),
                            DateTimePrecision = (short) rdr["DATETIME_PRECISION"],
                            MaxLength = (int) rdr["CHARACTER_MAXIMUM_LENGTH"],
                            Precision = precision,
                            Scale = scale,
                            UserDefinedTypeName = rdr["USER_DEFINED_TYPE"].ToString().Trim()
                        };

                        var clean = CleanUp(param.Name.Replace("@", string.Empty));
                        if (string.IsNullOrEmpty(clean))
                            continue;

                        param.NameHumanCase = Inflector.MakeInitialLower((Settings.UsePascalCase ? Inflector.ToTitleCase(clean) : clean).Replace(" ", string.Empty));

                        if (ReservedKeywords.Contains(param.NameHumanCase))
                            param.NameHumanCase = "@" + param.NameHumanCase;

                        sp.Parameters.Add(param);
                    }
                }
            }
            return result;
        }

        public void ReadStoredProcReturnObject(SqlConnection sqlConnection, StoredProcedure proc)
        {
            //TODO: Change SQL for different databases

            try
            {
                const string structured = "Structured";
                var sb = new StringBuilder();
                sb.AppendLine();
                sb.AppendLine("SET FMTONLY OFF; SET FMTONLY ON;");

                if (proc.IsTVF)
                {
                    foreach (var param in proc.Parameters.Where(x => x.SqlDbType.Equals(structured, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        sb.AppendLine(string.Format("DECLARE {0} {1};", param.Name, param.UserDefinedTypeName));
                    }

                    sb.Append(string.Format("SELECT * FROM [{0}].[{1}](", proc.Schema, proc.Name));
                    foreach (var param in proc.Parameters)
                    {
                        sb.Append(string.Format("{0}, ",
                            param.SqlDbType.Equals(structured, StringComparison.InvariantCultureIgnoreCase)
                                ? param.Name
                                : "default"));
                    }

                    if (proc.Parameters.Count > 0)
                        sb.Length -= 2;

                    sb.AppendLine(");");
                }
                else
                {
                    foreach (var param in proc.Parameters)
                    {
                        sb.AppendLine(string.Format("DECLARE {0} {1};", param.Name,
                            param.SqlDbType.Equals(structured, StringComparison.InvariantCultureIgnoreCase)
                                ? param.UserDefinedTypeName
                                : param.SqlDbType));
                    }

                    sb.Append(string.Format("exec [{0}].[{1}] ", proc.Schema, proc.Name));
                    foreach (var param in proc.Parameters)
                        sb.Append(string.Format("{0}, ", param.Name));

                    if (proc.Parameters.Count > 0)
                        sb.Length -= 2;

                    sb.AppendLine(";");
                }
                sb.AppendLine("SET FMTONLY OFF; SET FMTONLY OFF;");

                var ds = new DataSet();
                using (var sqlAdapter = new SqlDataAdapter(sb.ToString(), sqlConnection))
                {
                    if (sqlConnection.State != ConnectionState.Open)
                        sqlConnection.Open();
                    sqlAdapter.SelectCommand.ExecuteReader(CommandBehavior.SchemaOnly | CommandBehavior.KeyInfo);
                    sqlConnection.Close();
                    sqlAdapter.FillSchema(ds, SchemaType.Source, "MyTable");
                }

                // Tidy up parameters
                foreach (var p in proc.Parameters)
                    p.NameHumanCase = Regex.Replace(p.NameHumanCase, @"[^A-Za-z0-9@\s]*", string.Empty);

                for (var count = 0; count < ds.Tables.Count; count++)
                {
                    proc.ReturnModels.Add(ds.Tables[count].Columns.Cast<DataColumn>().ToList());
                }
            }
            catch (Exception)
            {
                // Stored procedure does not have a return type
            }
        }

        private Column CreateColumn(IDataRecord rdr, Table table)
        {
            if (rdr == null)
                throw new ArgumentNullException("rdr");

            var typename = rdr["TypeName"].ToString().Trim().ToLower();
            var scale = (int)rdr["Scale"];
            var precision = (int)rdr["Precision"];

            var col = new Column
            {
                Name = rdr["ColumnName"].ToString().Trim(),
                SqlPropertyType = typename,
                PropertyType = GetPropertyType(typename),
                MaxLength = (int)rdr["MaxLength"],
                Precision = precision,
                Default = rdr["Default"].ToString().Trim(),
                DateTimePrecision = (int)rdr["DateTimePrecision"],
                Scale = scale,
                Ordinal = (int)rdr["Ordinal"],
                IsIdentity = rdr["IsIdentity"].ToString().Trim().ToLower() == "true",
                IsNullable = rdr["IsNullable"].ToString().Trim().ToLower() == "true",
                IsStoreGenerated = rdr["IsStoreGenerated"].ToString().Trim().ToLower() == "true",
                IsPrimaryKey = rdr["PrimaryKey"].ToString().Trim().ToLower() == "true",
                PrimaryKeyOrdinal = (int)rdr["PrimaryKeyOrdinal"],
                IsForeignKey = rdr["IsForeignKey"].ToString().Trim().ToLower() == "true",
                ParentTable = table
            };

            if (col.MaxLength == -1 &&
                (col.SqlPropertyType.EndsWith("varchar", StringComparison.InvariantCultureIgnoreCase) ||
                 col.SqlPropertyType.EndsWith("varbinary", StringComparison.InvariantCultureIgnoreCase)))
                col.SqlPropertyType += "(max)";

            if (col.IsPrimaryKey && !col.IsIdentity && col.IsStoreGenerated && typename == "uniqueidentifier")
            {
                col.IsStoreGenerated = false;
                col.IsIdentity = true;
            }

            var fullName = string.Format("{0}.{1}.{2}", table.Schema, table.Name, col.Name);
            if (Settings.ColumnFilterExclude != null && !col.IsPrimaryKey &&
                (Settings.ColumnFilterExclude.IsMatch(col.Name) || Settings.ColumnFilterExclude.IsMatch(fullName)))
                col.Hidden = true;

            col.IsFixedLength = (typename == "char" || typename == "nchar");
            col.IsUnicode = !(typename == "char" || typename == "varchar" || typename == "text");
            col.IsMaxLength = (typename == "ntext");

            col.IsRowVersion = col.IsStoreGenerated && !col.IsNullable && typename == "timestamp";
            if (col.IsRowVersion)
                col.MaxLength = 8;

            if (typename == "hierarchyid")
                col.MaxLength = 0;

            col.CleanUpDefault();
            col.NameHumanCase = CleanUp(col.Name);
            col.NameHumanCase = ReservedColumnNames.Replace(col.NameHumanCase, "_$1");

            if (ReservedKeywords.Contains(col.NameHumanCase))
                col.NameHumanCase = "@" + col.NameHumanCase;

            col.DisplayName = ToDisplayName(col.Name);

            var titleCase =
                (Settings.UsePascalCase ? Inflector.ToTitleCase(col.NameHumanCase) : col.NameHumanCase).Replace(" ", string.Empty);
            if (titleCase != string.Empty)
                col.NameHumanCase = titleCase;

            // Make sure property name doesn't clash with class name
            if (col.NameHumanCase == table.NameHumanCase)
                col.NameHumanCase = col.NameHumanCase + "_";

            if (char.IsDigit(col.NameHumanCase[0]))
                col.NameHumanCase = "_" + col.NameHumanCase;

            table.HasNullableColumns = col.IsColumnNullable();

            // If PropertyType is empty, return null. Most likely ignoring a column due to legacy (such as OData not supporting spatial types)
            if (string.IsNullOrEmpty(col.PropertyType))
                return null;

            return col;
        }

        protected void WriteLine(string o)
        {
            _generatedTextTransformation.WriteLine(o);
        }

        private bool IsFilterExcluded(Regex filterExclude, Regex filterInclude, string name)
        {
            if (filterExclude != null && filterExclude.IsMatch(name))
                return true;
            if (filterInclude != null && !filterInclude.IsMatch(name))
                return true;
            if (name.Contains('.')) // EF does not allow tables to contain a period character
                return true;
            return false;
        }

        public static string ToDisplayName(string str)
        {
            if (string.IsNullOrEmpty(str))
                return string.Empty;

            var sb = new StringBuilder();
            str = Regex.Replace(str, @"[^a-zA-Z0-9]", " "); // Anything that is not a letter or digit, convert to a space
            str = Regex.Replace(str, @"[A-Z]{2,}", " $+ "); // Any word that is upper case

            var hasUpperCased = false;
            var lastChar = '\0';
            foreach (var original in str.Trim())
            {
                var c = original;
                if (lastChar == '\0')
                {
                    c = char.ToUpperInvariant(original);
                }
                else
                {
                    var isLetter = char.IsLetter(original);
                    var isDigit = char.IsDigit(original);
                    var isWhiteSpace = !isLetter && !isDigit;

                    // Is this char is different to last time
                    var isDifferent = false;
                    if (isLetter && !char.IsLetter(lastChar))
                        isDifferent = true;
                    else if (isDigit && !char.IsDigit(lastChar))
                        isDifferent = true;
                    else if (char.IsUpper(original) && !char.IsUpper(lastChar))
                        isDifferent = true;

                    if (isDifferent || isWhiteSpace)
                        sb.Append(' '); // Add a space

                    if (hasUpperCased && isLetter)
                        c = char.ToLowerInvariant(original);
                }
                lastChar = original;
                if (!hasUpperCased && char.IsUpper(c))
                    hasUpperCased = true;
                sb.Append(c);
            }
            str = sb.ToString();
            str = Regex.Replace(str, @"\s+", " ").Trim(); // Multiple white space to one space
            str = Regex.Replace(str, @"\bid\b", "ID"); //  Make ID word uppercase
            return str;
        }

        private static readonly Regex RemoveNonAlphanumerics = new Regex(@"[^\w\d\s_-]", RegexOptions.Compiled);

        public static readonly Func<string, string> CleanUp = (str) =>
        {
            // Replace punctuation and symbols in variable names as these are not allowed.
            var len = str.Length;
            if (len == 0)
                return str;

            var sb = new StringBuilder();
            var replacedCharacter = false;
            for (int n = 0; n < len; ++n)
            {
                var c = str[n];
                if (c != '_' && c != '-' && (char.IsSymbol(c) || char.IsPunctuation(c)))
                {
                    int ascii = c;
                    sb.AppendFormat("{0}", ascii);
                    replacedCharacter = true;
                    continue;
                }
                sb.Append(c);
            }
            if (replacedCharacter)
                str = sb.ToString();

            str = RemoveNonAlphanumerics.Replace(str, string.Empty);
            if (char.IsDigit(str[0]))
                str = "C" + str;

            return str;
        };
    }
}