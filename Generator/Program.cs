using System;
using System.IO;
using System.Linq;
using Generator.Pluralization;

namespace Generator
{
    public class Program
    {
        static void Main()
        {
            Settings.TargetFrameworkVersion = 4.5f;
            Settings.ConnectionString = "Data Source=(local);Initial Catalog=Northwind;Integrated Security=True;Application Name=Generator";
            Settings.ProviderName = "System.Data.SqlClient";
            Settings.DatabaseType = DatabaseType.SqlServer;

            // Use this when testing SQL Server Compact 4.0
            //Settings.ConnectionString = @"Data Source=|DataDirectory|\NorthwindSqlCe40.sdf";   // Uses last connection string in config if not specified
            //Settings.ProviderName = "System.Data.SqlServerCe.4.0";
            //Settings.DatabaseType = DatabaseType.SqlCe;


            //Inflector.PluralizationService = null;
            Inflector.PluralizationService = new EnglishPluralizationService();

            var path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            using (var sw = new StreamWriter(Path.Combine(path, "efrpg.txt")))
            {
                var x = new GeneratedTextTransformation();
                x.Init();
                x.ReadSchema();

                // Generate output
                //if (Settings.Tables.Count > 0 || Settings.StoredProcs.Count > 0)
                //{

                //}

                /*foreach (var table in x.Generator.Tables.Where(t => !t.IsMapping))
                {
                    Console.WriteLine(table.NameHumanCase);
                    sw.WriteLine(table.NameHumanCase);

                    foreach (var col in table.Columns)
                    {
                        if (!string.IsNullOrWhiteSpace(col.Entity))
                            Console.WriteLine("  " + col.Entity);
                    }
                    if (table.Columns.Count > 0)
                        Console.WriteLine();

                    foreach (var rp in table.ReverseNavigationProperty)
                    {
                        Console.WriteLine("  " + rp);
                        sw.WriteLine("  " + rp);
                    }
                    if (table.ReverseNavigationProperty.Count > 0)
                        Console.WriteLine();

                    Console.WriteLine("  // Config");
                    foreach (var rc in table.MappingConfiguration)
                    {
                        Console.WriteLine("  " + rc);
                        sw.WriteLine("  " + rc);
                    }
                    if (table.MappingConfiguration.Count > 0)
                        Console.WriteLine();

                    foreach (var col in table.Columns)
                    {
                        if (!string.IsNullOrWhiteSpace(col.Config))
                            Console.WriteLine("  " + col.Config);
                    }
                    if (table.Columns.Count > 0)
                        Console.WriteLine();

                    var fks = table.Columns.Where(col => col.ConfigFk.Any()).ToList();
                    if (fks.Count() > 0)
                        Console.WriteLine("  // FK's");
                    foreach (var col in fks)
                    {
                        Console.WriteLine("  " + col.ConfigFk);
                        sw.WriteLine("  " + col.ConfigFk);
                    }
                    Console.WriteLine();
                    sw.WriteLine();
                }*/
            }
        }
    }
}