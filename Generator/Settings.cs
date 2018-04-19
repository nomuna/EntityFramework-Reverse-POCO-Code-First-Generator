using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Generator
{
    // Settings - edit these in the main <name>.tt file *******************************************************************************
    public static class Settings
    {
        // Main settings **********************************************************************************************************************
        public static DatabaseType DatabaseType = DatabaseType.SqlServer; // SqlCe. Comming soon: Oracle, MySql, PostgreSQL
        public static string ConnectionStringName = "MyDbContext";   // Searches for this connection string in config files listed below in the ConfigFilenameSearchOrder setting
        // ConnectionStringName is the only required setting.
        public static int CommandTimeout = 600; // SQL Command timeout in seconds. 600 is 10 minutes, 0 will wait indefinately. Some databases can be slow retrieving schema information.
        // As an alternative to ConnectionStringName above, which must match your app/web.config connection string name, you can override them below
        public static string ConnectionString; // = "Data Source=(local);Initial Catalog=Northwind;Integrated Security=True;Application Name=EntityFramework Reverse POCO Generator";
        public static string ProviderName; // = "System.Data.SqlClient";

        public static string Namespace; // Override the default namespace here
        public static string DbContextName = "MyDbContext"; // Note: If generating separate files, please give the db context a different name from this tt filename.

        private static string _dbContextInterfaceName;
        public static string DbContextInterfaceName
        {
            get { return _dbContextInterfaceName ?? ("I" + DbContextName); }
            set { _dbContextInterfaceName = value; }
        }

        public static string DbContextInterfaceBaseClasses = "System.IDisposable";    // Specify what the base classes are for your database context interface
        public static string DbContextBaseClass = "System.Data.Entity.DbContext";   // Specify what the base class is for your DbContext. For ASP.NET Identity use "IdentityDbContext<ApplicationUser>"

        private static string _defaultConstructorArgument; // = "EnvironmentConnectionStrings.MyDbContext"; //defaults to "Name=" + ConnectionStringName
        public static string DefaultConstructorArgument
        {
            get { return _defaultConstructorArgument ?? string.Format('"' + "Name={0}" + '"', ConnectionStringName); }
            set { _defaultConstructorArgument = value; }
        }

        public static bool IsSqlCe { get; set; } // todo delete

        public static string ConfigurationClassName = "Configuration"; // Configuration, Mapping, Map, etc. This is appended to the Poco class name to configure the mappings.
        public static string[] FilenameSearchOrder = new[] { "app.config", "web.config" }; // Add more here if required. The config files are searched for in the local project first, then the whole solution second.
        public static bool GenerateSeparateFiles = false;
        public static bool MakeClassesInternal = false;
        public static bool MakeClassesPartial = false;
        public static bool MakeDbContextInterfacePartial = false;
        public static bool UseMappingTables = true; // If true, mapping will be used and no mapping tables will be generated. If false, all tables will be generated.
        public static bool UsePascalCase = true;    // This will rename the generated C# tables & properties to use PascalCase. If false table & property names will be left alone.
        public static bool UseDataAnnotations = false; // If true, will add data annotations to the poco classes.
        public static bool UsePropertyInitializers = false; // Removes POCO constructor and instead uses C# 6 property initialisers to set defaults
        public static bool UseLazyLoading = true; // Marks all navigation properties as virtual or not, to support or disable EF Lazy Loading feature
        public static CommentsStyle IncludeComments = CommentsStyle.AtEndOfField; // Adds comments to the generated code
        public static CommentsStyle IncludeExtendedPropertyComments = CommentsStyle.InSummaryBlock; // Adds extended properties as comments to the generated code
        public static bool IncludeConnectionSettingComments = true; // Add comments describing connection settings used to generate file
        public static bool IncludeViews = true;
        public static bool IncludeSynonyms = true;
        public static bool IncludeStoredProcedures = true;
        public static bool IncludeTableValuedFunctions = false; // If true, you must set IncludeStoredProcedures = true, and install the "EntityFramework.CodeFirstStoreFunctions" Nuget Package.
        public static bool DisableGeographyTypes = false; // Turns off use of System.Data.Entity.Spatial.DbGeography and System.Data.Entity.Spatial.DbGeometry as OData doesn't support entities with geometry/geography types.
        public static string CollectionInterfaceType = "System.Collections.Generic.ICollection"; //  = "System.Collections.Generic.List"; // Determines the declaration type of collections for the Navigation Properties. ICollection is used if not set.
        public static string CollectionType = "System.Collections.Generic.List";  // Determines the type of collection for the Navigation Properties. "ObservableCollection" for example. Add "System.Collections.ObjectModel" to AdditionalNamespaces if setting the CollectionType = "ObservableCollection".
        public static bool NullableShortHand = true; //true => T?, false => Nullable<T>
        public static bool AddIDbContextFactory = true; // Will add a default IDbContextFactory<DbContextName> implementation for easy dependency injection
        public static bool AddUnitTestingDbContext = true; // Will add a FakeDbContext and FakeDbSet for easy unit testing
        public static bool IncludeQueryTraceOn9481Flag = false; // If SqlServer 2014 appears frozen / take a long time when this file is saved, try setting this to true (you will also need elevated privileges).
        public static bool IncludeCodeGeneratedAttribute = true; // If true, will include the GeneratedCode attribute, false to remove it.
        public static bool UsePrivateSetterForComputedColumns = true; // If the columns is computed, use a private setter.
        public static string[] AdditionalNamespaces = new[] { "" };  // To include extra namespaces, include them here. i.e. "Microsoft.AspNet.Identity.EntityFramework"
        public static string[] AdditionalContextInterfaceItems = new[] // To include extra db context interface items, include them here. Also set MakeClassesPartial=true, and implement the partial DbContext class functions.
        {
            ""  //  example: "void SetAutoDetectChangesEnabled(bool flag);"
        };
        // If you need to serialize your entities with the JsonSerializer from Newtonsoft, this would serialize
        // all properties including the Reverse Navigation and Foreign Keys. The simplest way to exclude them is
        // to use the data annotation [JsonIgnore] on reverse navigation and foreign keys.
        // For more control, take a look at ForeignKeyAnnotationsProcessing() further down
        public static string[] AdditionalReverseNavigationsDataAnnotations = new string[] // Data Annotations for all ReverseNavigationProperty.
        {
            // "JsonIgnore" // Also add "Newtonsoft.Json" to the AdditionalNamespaces array above
        };
        public static string[] AdditionalForeignKeysDataAnnotations = new string[] // Data Annotations for all ForeignKeys.
        {
            // "JsonIgnore" // Also add "Newtonsoft.Json" to the AdditionalNamespaces array above
        };
        public static Dictionary<string, string> ColumnNameToDataAnnotation = new Dictionary<string, string>
        {
            // This is used when UseDataAnnotations = true;
            // It is used to set a data annotation on a column based on the columns name.
            // Make sure the column name is lowercase in the following array, regardless of how it is in the database
            // Column name       DataAnnotation to add
            { "email",           "EmailAddress" },
            { "emailaddress",    "EmailAddress" },
            { "creditcard",      "CreditCard" },
            { "url",             "Url" },
            { "phone",           "Phone" },
            { "phonenumber",     "Phone" },
            { "mobile",          "Phone" },
            { "mobilenumber",    "Phone" },
            { "telephone",       "Phone" },
            { "telephonenumber", "Phone" },
            { "password",        "DataType(DataType.Password)" },
            { "username",        "DataType(DataType.Text)" }
        };

        // Migrations *************************************************************************************************************************
        public static string MigrationConfigurationFileName = ""; // null or empty to not create migrations
        public static string MigrationStrategy = "MigrateDatabaseToLatestVersion"; // MigrateDatabaseToLatestVersion, CreateDatabaseIfNotExists or DropCreateDatabaseIfModelChanges
        public static string ContextKey = ""; // Sets the string used to distinguish migrations belonging to this configuration from migrations belonging to other configurations using the same database. This property enables migrations from multiple different models to be applied to applied to a single database.
        public static bool AutomaticMigrationsEnabled = true;
        public static bool AutomaticMigrationDataLossAllowed = true; // if true, can drop fields and lose data during automatic migration

        // Elements to generate ***************************************************************************************************************
        // Add the elements that should be generated when the template is executed.
        // Multiple projects can now be used that separate the different concerns.
        public static Elements ElementsToGenerate = Elements.Poco | Elements.Context | Elements.Interface | Elements.PocoConfiguration;

        // Use these namespaces to specify where the different elements now live. These may even be in different assemblies.
        // Please note this does not create the files in these locations, it only adds a using statement to say where they are.
        // The way to do this is to add the "EntityFramework Reverse POCO Code First Generator" into each of these folders.
        // Then set the .tt to only generate the relevant section you need by setting
        //      ElementsToGenerate = Elements.Poco; in your Entity folder,
        //      ElementsToGenerate = Elements.Context | Elements.Interface; in your Context folder,
        //      ElementsToGenerate = Elements.PocoConfiguration; in your Maps folder.
        //      PocoNamespace = "YourProject.Entities";
        //      ContextNamespace = "YourProject.Context";
        //      InterfaceNamespace = "YourProject.Context";
        //      PocoConfigurationNamespace = "YourProject.Maps";
        // You also need to set the following to the namespace where they now live:
        public static string PocoNamespace = "";
        public static string ContextNamespace = "";
        public static string InterfaceNamespace = "";
        public static string PocoConfigurationNamespace = "";

        // Schema *****************************************************************************************************************************
        // If there are multiple schemas, then the table name is prefixed with the schema, except for dbo.
        // Ie. dbo.hello will be Hello.
        //     abc.hello will be AbcHello.
        public static bool PrependSchemaName = true;   // Control if the schema name is prepended to the table name

        // Table Suffix ***********************************************************************************************************************
        // Prepends the suffix to the generated classes names
        // Ie. If TableSuffix is "Dto" then Order will be OrderDto
        //     If TableSuffix is "Entity" then Order will be OrderEntity
        public static string TableSuffix = null;

        // Filtering **************************************************************************************************************************
        // Use the following table/view name regex filters to include or exclude tables/views
        // Exclude filters are checked first and tables matching filters are removed.
        //  * If left null, none are excluded.
        //  * If not null, any tables matching the regex are excluded.
        // Include filters are checked second.
        //  * If left null, all are included.
        //  * If not null, only the tables matching the regex are included.
        // For clarity: if you want to include all the customer tables, but not the customer billing tables.
        //      TableFilterInclude = new Regex("^[Cc]ustomer.*"); // This includes all the customer and customer billing tables
        //      TableFilterExclude = new Regex(".*[Bb]illing.*"); // This excludes all the billing tables
        //
        // Example:     TableFilterExclude = new Regex(".*auto.*");
        //              TableFilterInclude = new Regex("(.*_FR_.*)|(data_.*)");
        //              TableFilterInclude = new Regex("^table_name1$|^table_name2$|etc");
        //              ColumnFilterExclude = new Regex("^FK_.*$");
        public static Regex SchemaFilterExclude = null;
        public static Regex SchemaFilterInclude = null;
        public static Regex TableFilterExclude = null;
        public static Regex TableFilterInclude = null;
        public static Regex ColumnFilterExclude = null;

        // Filtering of tables using a function. This can be used in conjunction with the Regex's above.
        // Regex are used first to filter the list down, then this function is run last.
        // Return true to include the table, return false to exclude it.
        public static Func<Table, bool> TableFilter = (Table t) =>
        {
            // Example: Exclude any table in dbo schema with "order" in its name.
            //if(t.Schema.Equals("dbo", StringComparison.InvariantCultureIgnoreCase) && t.NameHumanCase.ToLowerInvariant().Contains("order"))
            //    return false;

            return true;
        };

        // Stored Procedures ******************************************************************************************************************
        // Use the following regex filters to include or exclude stored procedures
        public static Regex StoredProcedureFilterExclude = null;
        public static Regex StoredProcedureFilterInclude = null;

        // Filtering of stored procedures using a function. This can be used in conjunction with the Regex's above.
        // Regex are used first to filter the list down, then this function is run last.
        // Return true to include the stored procedure, return false to exclude it.
        public static Func<StoredProcedure, bool> StoredProcedureFilter = (StoredProcedure sp) =>
        {
            // Example: Exclude any stored procedure in dbo schema with "order" in its name.
            //if(sp.Schema.Equals("dbo", StringComparison.InvariantCultureIgnoreCase) && sp.NameHumanCase.ToLowerInvariant().Contains("order"))
            //    return false;

            return true;
        };

        // Table renaming *********************************************************************************************************************
        // Use the following function to rename tables such as tblOrders to Orders, Shipments_AB to Shipments, etc.
        // Example:
        public static Func<string, string, bool, string> TableRename = (string name, string schema, bool isView) =>
        {
            // Example
            //if (name.StartsWith("tbl"))
            //    name = name.Remove(0, 3);
            //name = name.Replace("_AB", "");

            //if(isView)
            //    name = name + "View";

            // If you turn pascal casing off (UsePascalCase = false), and use the pluralisation service, and some of your
            // tables names are all UPPERCASE, some words ending in IES such as CATEGORIES get singularised as CATEGORy.
            // Therefore you can make them lowercase by using the following
            // return Inflector.MakeLowerIfAllCaps(name);

            // If you are using the pluralisation service and you want to rename a table, make sure you rename the table to the plural form.
            // For example, if the table is called Treez (with a z), and your pluralisation entry is
            //     new CustomPluralizationEntry("Tree", "Trees")
            // Use this TableRename function to rename Treez to the plural (not singular) form, Trees:
            // if (name == "Treez") return "Trees";

            return name;
        };

        // Column modification*****************************************************************************************************************
        // Use the following list to replace column byte types with Enums.
        // As long as the type can be mapped to your new type, all is well.
        //Settings.EnumDefinitions.Add(new EnumDefinition { Schema = "dbo", Table = "match_table_name", Column = "match_column_name", EnumType = "name_of_enum" });
        //Settings.EnumDefinitions.Add(new EnumDefinition { Schema = "dbo", Table = "OrderHeader", Column = "OrderStatus", EnumType = "OrderStatusType" }); // This will replace OrderHeader.OrderStatus type to be an OrderStatusType enum
        public static List<EnumDefinition> EnumDefinitions = new List<EnumDefinition>();

        // Use the following function if you need to apply additional modifications to a column
        // eg. normalise names etc.
        public static Func<Column, Table, Column> UpdateColumn = (Column column, Table table) =>
        {
            // Rename column
            //if (column.IsPrimaryKey && column.NameHumanCase == "PkId")
            //    column.NameHumanCase = "Id";

            // .IsConcurrencyToken() must be manually configured. However .IsRowVersion() can be automatically detected.
            //if (table.NameHumanCase.Equals("SomeTable", StringComparison.InvariantCultureIgnoreCase) && column.NameHumanCase.Equals("SomeColumn", StringComparison.InvariantCultureIgnoreCase))
            //    column.IsConcurrencyToken = true;

            // Remove table name from primary key
            //if (column.IsPrimaryKey && column.NameHumanCase.Equals(table.NameHumanCase + "Id", StringComparison.InvariantCultureIgnoreCase))
            //    column.NameHumanCase = "Id";

            // Remove column from poco class as it will be inherited from a base class
            //if (column.IsPrimaryKey && table.NameHumanCase.Equals("SomeTable", StringComparison.InvariantCultureIgnoreCase))
            //    column.Hidden = true;

            // Use the extended properties to perform tasks to column
            //if (column.ExtendedProperty == "HIDE")
            //    column.Hidden = true;

            // Apply the "override" access modifier to a specific column.
            // if (column.NameHumanCase == "id")
            //    column.OverrideModifier = true;
            // This will create: public override long id { get; set; }

            // Perform Enum property type replacement
            var enumDefinition = EnumDefinitions.FirstOrDefault(e =>
                (e.Schema.Equals(table.Schema, StringComparison.InvariantCultureIgnoreCase)) &&
                (e.Table.Equals(table.Name, StringComparison.InvariantCultureIgnoreCase) || e.Table.Equals(table.NameHumanCase, StringComparison.InvariantCultureIgnoreCase)) &&
                (e.Column.Equals(column.Name, StringComparison.InvariantCultureIgnoreCase) || e.Column.Equals(column.NameHumanCase, StringComparison.InvariantCultureIgnoreCase)));

            if (enumDefinition != null)
            {
                column.PropertyType = enumDefinition.EnumType;
                if (!string.IsNullOrEmpty(column.Default))
                    column.Default = "(" + enumDefinition.EnumType + ") " + column.Default;
            }

            return column;
        };

        // StoredProcedure renaming ************************************************************************************************************
        // Use the following function to rename stored procs such as sp_CreateOrderHistory to CreateOrderHistory, my_sp_shipments to Shipments, etc.
        // Example:
        /*Settings.StoredProcedureRename = (sp) =>
        {
            if (sp.NameHumanCase.StartsWith("sp_"))
                return sp.NameHumanCase.Remove(0, 3);
            return sp.NameHumanCase.Replace("my_sp_", "");
        };*/
        public static Func<StoredProcedure, string> StoredProcedureRename = (sp) => sp.NameHumanCase;   // Do nothing by default

        // Use the following function to rename the return model automatically generated for stored procedure.
        // By default it's <proc_name>ReturnModel.
        // Example:
        /*Settings.StoredProcedureReturnModelRename = (name, sp) =>
        {
            if (sp.NameHumanCase.Equals("ComputeValuesForDate", StringComparison.InvariantCultureIgnoreCase))
                return "ValueSet";
            if (sp.NameHumanCase.Equals("SalesByYear", StringComparison.InvariantCultureIgnoreCase))
                return "SalesSet";

            return name;
        };*/
        public static Func<string, StoredProcedure, string> StoredProcedureReturnModelRename = (name, sp) => name; // Do nothing by default

        // StoredProcedure return types *******************************************************************************************************
        // Override generation of return models for stored procedures that return entities.
        // If a stored procedure returns an entity, add it to the list below.
        // This will suppress the generation of the return model, and instead return the entity.
        // Example:                       Proc name      Return this entity type instead
        //StoredProcedureReturnTypes.Add("SalesByYear", "SummaryOfSalesByYear");
        public static Dictionary<string, string> StoredProcedureReturnTypes = new Dictionary<string, string>();

        public static Func<ForeignKey, ForeignKey> ForeignKeyFilter = (ForeignKey fk) =>
        {
            // Return null to exclude this foreign key, or set IncludeReverseNavigation = false
            // to include the foreign key but not generate reverse navigation properties.
            // Example, to exclude all foreign keys for the Categories table, use:
            // if (fk.PkTableName == "Categories")
            //    return null;

            // Example, to exclude reverse navigation properties for tables ending with Type, use:
            // if (fk.PkTableName.EndsWith("Type"))
            //    fk.IncludeReverseNavigation = false;

            return fk;
        };

        public static Func<IList<ForeignKey>, Table, Table, bool, ForeignKey> ForeignKeyProcessing = (foreignKeys, fkTable, pkTable, anyNullableColumnInForeignKey) =>
        {
            var foreignKey = foreignKeys.First();

            // If using data annotations and to include the [Required] attribute in the foreign key, enable the following
            //if (!anyNullableColumnInForeignKey)
            //   foreignKey.IncludeRequiredAttribute = true;

            return foreignKey;
        };

        public static Func<string, ForeignKey, string, Relationship, short, string> ForeignKeyName = (tableName, foreignKey, foreignKeyName, relationship, attempt) =>
        {
            string fkName;

            // 5 Attempts to correctly name the foreign key
            switch (attempt)
            {
                case 1:
                    // Try without appending foreign key name
                    fkName = tableName;
                    break;

                case 2:
                    // Only called if foreign key name ends with "id"
                    // Use foreign key name without "id" at end of string
                    fkName = foreignKeyName.Remove(foreignKeyName.Length - 2, 2);
                    break;

                case 3:
                    // Use foreign key name only
                    fkName = foreignKeyName;
                    break;

                case 4:
                    // Use table name and foreign key name
                    fkName = tableName + "_" + foreignKeyName;
                    break;

                case 5:
                    // Used in for loop 1 to 99 to append a number to the end
                    fkName = tableName;
                    break;

                default:
                    // Give up
                    fkName = tableName;
                    break;
            }

            // Apply custom foreign key renaming rules. Can be useful in applying pluralization.
            // For example:
            /*if (tableName == "Employee" && foreignKey.FkColumn == "ReportsTo")
                return "Manager";

            if (tableName == "Territories" && foreignKey.FkTableName == "EmployeeTerritories")
                return "Locations";

            if (tableName == "Employee" && foreignKey.FkTableName == "Orders" && foreignKey.FkColumn == "EmployeeID")
                return "ContactPerson";
            */

            // FK_TableName_FromThisToParentRelationshipName_FromParentToThisChildsRelationshipName
            // (e.g. FK_CustomerAddress_Customer_Addresses will extract navigation properties "address.Customer" and "customer.Addresses")
            // Feel free to use and change the following
            /*if (foreignKey.ConstraintName.StartsWith("FK_") && foreignKey.ConstraintName.Count(x => x == '_') == 3)
            {
                var parts = foreignKey.ConstraintName.Split('_');
                if (!string.IsNullOrWhiteSpace(parts[2]) && !string.IsNullOrWhiteSpace(parts[3]) && parts[1] == foreignKey.FkTableName)
                {
                    if (relationship == Relationship.OneToMany)
                        fkName = parts[3];
                    else if (relationship == Relationship.ManyToOne)
                        fkName = parts[2];
                }
            }*/

            return fkName;
        };

        public static Func<Table, Table, string, string[]> ForeignKeyAnnotationsProcessing = (Table fkTable, Table pkTable, string propName) =>
        {
            /* Example:
            // Each navigation property that is a reference to User are left intact
            if (pkTable.NameHumanCase.Equals("User") && propName.Equals("User"))
                return null;

            // all the others are marked with this attribute
            return new[] { "System.Runtime.Serialization.IgnoreDataMember" };
            */

            return null;
        };

        // Return true to include this table in the db context
        public static Func<Table, bool> ConfigurationFilter = (Table t) =>
        {
            return true;
        };

        public static string FileExtension = ".cs";
        public static float TargetFrameworkVersion = 4.5f;

        // That's it, nothing else to configure ***********************************************************************************************


        public static string ConfigFilePath;
    };
}