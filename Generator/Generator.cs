using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using Generator.SchemaReaders;

namespace Generator
{
    public class Generator
    {
        private readonly GeneratedTextTransformation _outer;
        private DbProviderFactory _factory;
        private SchemaReader _schemaReader;
        public Tables Tables { get; private set; }
        public List<StoredProcedure> StoredProcs { get; private set; }

        public Generator(GeneratedTextTransformation outer)
        {
            _outer = outer;
            _factory = null;
            _schemaReader = null;
        }

        public void Init()
        {
            _factory = DbProviderFactories.GetFactory(Settings.ProviderName);
            _schemaReader = GetSchemaReader();
            _schemaReader.Init();
        }

        private SchemaReader GetSchemaReader()
        {
            if (_factory == null)
            {
                _outer.WriteLine("Database factory is null, cannot continue");
                return null;
            }

            switch (Settings.DatabaseType)
            {
                case DatabaseType.SqlServer:
                    return new SqlServerSchemaReader(_factory, _outer);

                case DatabaseType.SqlCe:
                    return new SqlServerCeSchemaReader(_factory, _outer);

                case DatabaseType.MySql:
                    return new MySqlSchemaReader(_factory, _outer);

                case DatabaseType.PostgreSQL:
                    return new PostgreSqlSchemaReader(_factory, _outer);

                case DatabaseType.Oracle:
                    return new OracleSchemaReader(_factory, _outer);

                default:
                    _outer.WriteLine("Cannot create a schema reader due to unknown database type.");
                    return null;
            }
        }

        public void LoadTables()
        {
            Tables = new Tables();

            if (_factory == null || _schemaReader == null ||
                !(Settings.ElementsToGenerate.HasFlag(Elements.Poco) ||
                  Settings.ElementsToGenerate.HasFlag(Elements.Context) ||
                  Settings.ElementsToGenerate.HasFlag(Elements.Interface) ||
                  Settings.ElementsToGenerate.HasFlag(Elements.PocoConfiguration)))
                return;

            try
            {
                Tables = _schemaReader.ReadSchema();
                var fkList = _schemaReader.ReadForeignKeys();
                _schemaReader.IdentifyForeignKeys(fkList, Tables);

                foreach (var t in Tables)
                {
                    if (Settings.UseDataAnnotations)
                        t.SetupDataAnnotations();
                    t.Suffix = Settings.TableSuffix;
                }

                // Work out if there are any foreign key relationship naming clashes
                _schemaReader.ProcessForeignKeys(fkList, Tables, true);
                if (Settings.UseMappingTables)
                    Tables.IdentifyMappingTables(fkList, true);

                // Now we know our foreign key relationships and have worked out if there are any name clashes,
                // re-map again with intelligently named relationships.
                Tables.ResetNavigationProperties();

                _schemaReader.ProcessForeignKeys(fkList, Tables, false);
                if (Settings.UseMappingTables)
                    Tables.IdentifyMappingTables(fkList, false);
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

            if (_factory == null || _schemaReader == null || !Settings.IncludeStoredProcedures || !_schemaReader.CanReadStoredProcedures())
                return ;

            try
            {
                var storedProcs = _schemaReader.ReadStoredProcs();

                using (var sqlConnection = new SqlConnection(Settings.ConnectionString))
                {
                    foreach (var proc in storedProcs)
                        _schemaReader.ReadStoredProcReturnObject(sqlConnection, proc);
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
    }
}