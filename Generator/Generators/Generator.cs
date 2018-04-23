using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using Generator.DatabaseReaders;

namespace Generator.Generators
{
    public abstract class Generator
    {
        protected abstract void SetupEntity(Column c);
        protected abstract void SetupConfig(Column c);

        private readonly GeneratedTextTransformation _outer;
        private DbProviderFactory _factory;
        protected DatabaseReader DatabaseReader;
        public Tables Tables { get; private set; }
        public List<StoredProcedure> StoredProcs { get; private set; }


        protected Generator(GeneratedTextTransformation outer)
        {
            _outer = outer;
            _factory = null;
            DatabaseReader = null;
        }

        public void Init()
        {
            DatabaseReader = null;
            _factory = DbProviderFactories.GetFactory(Settings.ProviderName);
            if (_factory == null)
            {
                _outer.WriteLine("Database factory is null, cannot continue");
                return;
            }

            DatabaseReader = DatabaseReaderFactory.Create(_factory, _outer);
            if (DatabaseReader == null)
                _outer.WriteLine("Cannot create a schema reader due to unknown database type.");
            else
                DatabaseReader.Init();
        }

        public void LoadTables()
        {
            Tables = new Tables();

            if (_factory == null || DatabaseReader == null ||
                !(Settings.ElementsToGenerate.HasFlag(Elements.Poco) ||
                  Settings.ElementsToGenerate.HasFlag(Elements.Context) ||
                  Settings.ElementsToGenerate.HasFlag(Elements.Interface) ||
                  Settings.ElementsToGenerate.HasFlag(Elements.PocoConfiguration)))
                return;

            try
            {
                Tables = DatabaseReader.ReadSchema();
                foreach (var t in Tables)
                {
                    t.SetPrimaryKeys();

                    foreach (var c in t.Columns)
                        Settings.UpdateColumn(c, t);

                    t.Columns.ForEach(c => SetupEntityAndConfig(c));

                    if (Settings.UseDataAnnotations)
                        t.SetupDataAnnotations(DatabaseReader.IncludeSchema);

                    t.Suffix = Settings.TableSuffix;
                }

                var fkList = DatabaseReader.ReadForeignKeys();
                IdentifyForeignKeys(fkList);

                // Work out if there are any foreign key relationship naming clashes
                ProcessForeignKeys(fkList, Tables, true);
                if (Settings.UseMappingTables)
                    Tables.IdentifyMappingTables(fkList, true, DatabaseReader.IncludeSchema);

                // Now we know our foreign key relationships and have worked out if there are any name clashes,
                // re-map again with intelligently named relationships.
                Tables.ResetNavigationProperties();

                ProcessForeignKeys(fkList, Tables, false);
                if (Settings.UseMappingTables)
                    Tables.IdentifyMappingTables(fkList, false, DatabaseReader.IncludeSchema);
            }
            catch (Exception x)
            {
                var error = FormatError(x);
                _outer.Warning(string.Format("Failed to read database schema - {0}", error));
                _outer.WriteLine(string.Empty);
                _outer.WriteLine("// -----------------------------------------------------------------------------------------");
                _outer.WriteLine("// Failed to read database schema in LoadTables() - {0}", error);
                _outer.WriteLine("// -----------------------------------------------------------------------------------------");
                _outer.WriteLine(string.Empty);
            }
        }

        public static string FormatError(Exception ex)
        {
            return ex.Message.Replace("\r\n", "\n").Replace("\n", " ");
        }

        public void LoadStoredProcs()
        {
            StoredProcs = new List<StoredProcedure>();

            if (_factory == null || DatabaseReader == null || !Settings.IncludeStoredProcedures || !DatabaseReader.CanReadStoredProcedures())
                return;

            try
            {
                var storedProcs = DatabaseReader.ReadStoredProcs();

                using (var sqlConnection = new SqlConnection(Settings.ConnectionString))
                {
                    foreach (var proc in storedProcs)
                        DatabaseReader.ReadStoredProcReturnObject(sqlConnection, proc);
                }

                // Remove stored procs where the return model type contains spaces and cannot be mapped
                // Also need to remove any TVF functions with parameters that are non scalar types, such as DataTable
                StoredProcs = new List<StoredProcedure>();
                foreach (var sp in storedProcs)
                {
                    if (!sp.ReturnModels.Any())
                    {
                        StoredProcs.Add(sp);
                        continue;
                    }

                    if (sp.ReturnModels.Any(returnColumns => returnColumns.Any(c => c.ColumnName.Contains(" "))))
                        continue;

                    if (sp.IsTVF && sp.Parameters.Any(c => c.PropertyType == "System.Data.DataTable"))
                        continue;

                    StoredProcs.Add(sp);
                }
            }
            catch (Exception x)
            {
                var error = FormatError(x);
                _outer.Warning(string.Format("Failed to read database schema for stored procedures - {0}", error));
                _outer.WriteLine(string.Empty);
                _outer.WriteLine("// -----------------------------------------------------------------------------------------");
                _outer.WriteLine("// Failed to read database schema for stored procedures - {0}", error);
                _outer.WriteLine("// -----------------------------------------------------------------------------------------");
                _outer.WriteLine(string.Empty);
            }
        }

        public void ProcessForeignKeys(List<ForeignKey> fkList, Tables tables, bool checkForFkNameClashes)
        {
            var constraints = fkList.Select(x => x.FkSchema + "." + x.ConstraintName).Distinct();
            foreach (var constraint in constraints)
            {
                var foreignKeys = fkList
                    .Where(x => string.Format("{0}.{1}", x.FkSchema, x.ConstraintName)
                        .Equals(constraint, StringComparison.InvariantCultureIgnoreCase))
                    .ToList();

                var foreignKey = foreignKeys.First();
                var fkTable = tables.GetTable(foreignKey.FkTableName, foreignKey.FkSchema);
                if (fkTable == null || fkTable.IsMapping || !fkTable.HasForeignKey)
                    continue;

                var pkTable = tables.GetTable(foreignKey.PkTableName, foreignKey.PkSchema);
                if (pkTable == null || pkTable.IsMapping)
                    continue;

                var fkCols = foreignKeys.Select(x => new
                    {
                        fk = x,
                        col = fkTable.Columns.Find(n =>
                            string.Equals(n.Name, x.FkColumn, StringComparison.InvariantCultureIgnoreCase))
                    })
                    .Where(x => x.col != null)
                    .OrderBy(o => o.fk.Ordinal)
                    .ToList();

                if (!fkCols.Any())
                    continue;

                //if(EF6)
                {
                    // Check FK has same number of columns as the primary key it points to
                    var pks = pkTable.PrimaryKeys.OrderBy(x => x.PropertyType).ThenBy(y => y.Name).ToArray();
                    var cols = fkCols.Select(x => x.col).OrderBy(x => x.PropertyType).ThenBy(y => y.Name).ToArray();
                    if (pks.Length != cols.Length)
                        continue;

                    // EF6 - Cannot have a FK to a non-primary key
                    if (pks.Where((pk, ef6Check) => pk.PropertyType != cols[ef6Check].PropertyType).Any())
                        continue;
                }

                var pkCols = foreignKeys.Select(x => pkTable.Columns.Find(n => string.Equals(n.Name, x.PkColumn, StringComparison.InvariantCultureIgnoreCase)))
                    .Where(x => x != null)
                    .OrderBy(o => o.Ordinal)
                    .ToList();

                if (!pkCols.Any())
                    continue;

                var relationship = CalcRelationship(pkTable, fkTable, fkCols.Select(c => c.col).ToList(), pkCols);
                if (relationship == Relationship.DoNotUse)
                    continue;

                if (fkCols.All(x => !x.col.IsNullable && !x.col.Hidden) && pkCols.All(x => x.IsPrimaryKey || x.IsUnique))
                {
                    foreach (var fk in fkCols)
                        fk.fk.IncludeRequiredAttribute = true;
                }

                var fkCol = fkCols.First();
                var pkCol = pkCols.First();

                foreignKey = Settings.ForeignKeyProcessing(foreignKeys, fkTable, pkTable, fkCols.Any(x => x.col.IsNullable));

                var pkTableHumanCaseWithSuffix = foreignKey.PkTableHumanCase(pkTable.Suffix);
                var pkTableHumanCase = foreignKey.PkTableHumanCase(null);
                var pkPropName = fkTable.GetUniqueColumnName(pkTableHumanCase, foreignKey, checkForFkNameClashes, true, Relationship.ManyToOne);
                var fkMakePropNameSingular = (relationship == Relationship.OneToOne);
                var fkPropName = pkTable.GetUniqueColumnName(fkTable.NameHumanCase, foreignKey, checkForFkNameClashes, fkMakePropNameSingular, Relationship.OneToMany);

                var dataAnnotation = string.Empty;
                if (Settings.UseDataAnnotations)
                {
                    dataAnnotation = string.Format("[ForeignKey(\"{0}\"){1}] ",
                        string.Join(", ", fkCols.Select(x => x.col.NameHumanCase).Distinct().ToArray()),
                        foreignKey.IncludeRequiredAttribute ? ", Required" : string.Empty
                    );

                    if (!checkForFkNameClashes &&
                        relationship == Relationship.OneToOne &&
                        foreignKey.IncludeReverseNavigation &&
                        fkCols.All(x => x.col.IsPrimaryKey))
                    {
                        var principalEndAttribute = string.Format("ForeignKey(\"{0}\")", pkPropName);
                        foreach (var fk in fkCols)
                        {
                            if (!fk.col.DataAnnotations.Contains(principalEndAttribute))
                                fk.col.DataAnnotations.Add(principalEndAttribute);
                        }
                    }
                }

                var fkd = new PropertyAndComments
                {
                    AdditionalDataAnnotations = Settings.ForeignKeyAnnotationsProcessing(fkTable, pkTable, pkPropName),

                    Definition = string.Format("{0}public {1}{2} {3} {4}{5}", dataAnnotation,
                        Table.GetLazyLoadingMarker(),
                        pkTableHumanCaseWithSuffix,
                        pkPropName,
                        "{ get; set; }",
                        Settings.IncludeComments != CommentsStyle.None ? " // " + foreignKey.ConstraintName : string.Empty),

                    Comments = string.Format("Parent {0} pointed by [{1}].({2}) ({3})",
                        pkTableHumanCase,
                        fkTable.Name,
                        string.Join(", ", fkCols.Select(x => "[" + x.col.NameHumanCase + "]").Distinct().ToArray()),
                        foreignKey.ConstraintName)
                };
                fkCol.col.EntityFk.Add(fkd);

                string manyToManyMapping, mapKey;
                if (foreignKeys.Count > 1)
                {
                    manyToManyMapping = string.Format("c => new {{ {0} }}",
                        string.Join(", ", fkCols.Select(x => "c." + x.col.NameHumanCase).Distinct().ToArray()));

                    mapKey = string.Format("{0}",
                        string.Join(",", fkCols.Select(x => "\"" + x.col.Name + "\"").Distinct().ToArray()));
                }
                else
                {
                    manyToManyMapping = string.Format("c => c.{0}", fkCol.col.NameHumanCase);
                    mapKey = string.Format("\"{0}\"", fkCol.col.Name);
                }

                if (!Settings.UseDataAnnotations)
                {
                    fkCol.col.ConfigFk.Add(string.Format("{0};{1}",
                        GetRelationship(relationship, fkCol.col, pkCol, pkPropName, fkPropName, manyToManyMapping, mapKey,
                            foreignKey.CascadeOnDelete, foreignKey.IncludeReverseNavigation, foreignKey.IsNotEnforced),
                        Settings.IncludeComments != CommentsStyle.None
                            ? " // " + foreignKey.ConstraintName
                            : string.Empty));
                }

                if (foreignKey.IncludeReverseNavigation)
                    pkTable.AddReverseNavigation(relationship, pkTableHumanCase, fkTable, fkPropName,
                        string.Format("{0}.{1}", fkTable.Name, foreignKey.ConstraintName), foreignKeys);
            }
        }

        private void IdentifyForeignKeys(List<ForeignKey> fkList)
        {
            foreach (var foreignKey in fkList)
            {
                var fkTable = Tables.GetTable(foreignKey.FkTableName, foreignKey.FkSchema);
                if (fkTable == null)
                    continue; // Could be filtered out

                var pkTable = Tables.GetTable(foreignKey.PkTableName, foreignKey.PkSchema);
                if (pkTable == null)
                    continue; // Could be filtered out

                var fkCol = fkTable.Columns.Find(n => string.Equals(n.Name, foreignKey.FkColumn, StringComparison.InvariantCultureIgnoreCase));
                if (fkCol == null)
                    continue; // Could not find fk column

                var pkCol = pkTable.Columns.Find(n => string.Equals(n.Name, foreignKey.PkColumn, StringComparison.InvariantCultureIgnoreCase));
                if (pkCol == null)
                    continue; // Could not find pk column

                fkTable.HasForeignKey = true;
            }
        }

        private static string GetRelationship(Relationship relationship, Column fkCol, Column pkCol, string pkPropName,
            string fkPropName, string manyToManyMapping, string mapKey, bool cascadeOnDelete, bool includeReverseNavigation,
            bool isNotEnforced)
        {
            return string.Format("Has{0}(a => a.{1}){2}{3}",
                GetHasMethod(relationship, fkCol, pkCol, isNotEnforced),
                pkPropName,
                GetWithMethod(relationship, fkCol, fkPropName, manyToManyMapping, mapKey, includeReverseNavigation),
                cascadeOnDelete ? string.Empty : ".WillCascadeOnDelete(false)");
        }

        // HasOptional
        // HasRequired
        // HasMany
        private static string GetHasMethod(Relationship relationship, Column fkCol, Column pkCol, bool isNotEnforced)
        {
            bool withMany = false;
            switch (relationship)
            {
                case Relationship.ManyToOne:
                case Relationship.ManyToMany:
                    withMany = true;
                    break;
            }

            if (withMany || pkCol.IsPrimaryKey || pkCol.IsUniqueConstraint || pkCol.IsUnique)
                return fkCol.IsNullable || isNotEnforced ? "Optional" : "Required";

            return "Many";
        }

        // WithOptional
        // WithRequired
        // WithMany
        // WithRequiredPrincipal
        // WithRequiredDependent
        private static string GetWithMethod(Relationship relationship, Column fkCol, string fkPropName,
            string manyToManyMapping, string mapKey, bool includeReverseNavigation)
        {
            var withParam = includeReverseNavigation ? string.Format("b => b.{0}", fkPropName) : string.Empty;

            switch (relationship)
            {
                case Relationship.OneToOne:
                    return string.Format(".WithOptional({0})", withParam);

                case Relationship.OneToMany:
                    return string.Format(".WithRequiredDependent({0})", withParam);

                case Relationship.ManyToOne:
                    if (!fkCol.Hidden)
                        return string.Format(".WithMany({0}).HasForeignKey({1})", withParam, manyToManyMapping); // Foreign Key Association
                    return string.Format(".WithMany({0}).Map(c => c.MapKey({1}))", withParam, mapKey); // Independent Association

                case Relationship.ManyToMany:
                    return string.Format(".WithMany({0}).HasForeignKey({1})", withParam, manyToManyMapping);

                default:
                    throw new ArgumentOutOfRangeException("relationship");
            }
        }

        // Calculates the relationship between a child table and it's parent table.
        private static Relationship CalcRelationship(Table parentTable, Table childTable, List<Column> childTableCols, List<Column> parentTableCols)
        {
            if (childTableCols.Count == 1 && parentTableCols.Count == 1)
                return CalcRelationshipSingle(parentTable, childTable, childTableCols.First(), parentTableCols.First());

            // This relationship has multiple composite keys

            // childTable FK columns are exactly the primary key (they are part of primary key, and no other columns are primary keys)
            //TODO: we could also check if they are a unique index
            var childTableColumnsAllPrimaryKeys =
                (childTableCols.Count == childTableCols.Count(x => x.IsPrimaryKey)) &&
                (childTableCols.Count == childTable.PrimaryKeys.Count());

            // parentTable columns are exactly the primary key (they are part of primary key, and no other columns are primary keys)
            //TODO: we could also check if they are a unique index
            var parentTableColumnsAllPrimaryKeys =
                (parentTableCols.Count == parentTableCols.Count(x => x.IsPrimaryKey)) &&
                (parentTableCols.Count == parentTable.PrimaryKeys.Count());

            // childTable FK columns are not only FK but also the whole PK (not only part of PK); parentTable columns are the whole PK (not only part of PK) - so it's 1:1
            if (childTableColumnsAllPrimaryKeys && parentTableColumnsAllPrimaryKeys)
                return Relationship.OneToOne;

            return Relationship.ManyToOne;
        }

        // Calculates the relationship between a child table and it's parent table.
        private static Relationship CalcRelationshipSingle(Table parentTable, Table childTable, Column childTableCol, Column parentTableCol)
        {
            if (!childTableCol.IsPrimaryKey && !childTableCol.IsUniqueConstraint)
                return Relationship.ManyToOne;

            if (!parentTableCol.IsPrimaryKey && !parentTableCol.IsUniqueConstraint)
                return Relationship.ManyToOne;

            if (childTable.PrimaryKeys.Count() != 1)
                return Relationship.ManyToOne;

            if (parentTable.PrimaryKeys.Count() != 1)
                return Relationship.ManyToOne;

            return Relationship.OneToOne;
        }

        public void SetupEntityAndConfig(Column c)
        {
            SetupEntity(c);
            SetupConfig(c);
        }
    }
}