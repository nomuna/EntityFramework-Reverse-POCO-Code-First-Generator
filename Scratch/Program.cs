using System;
using System.Data.Entity.Infrastructure.Pluralization;
using System.IO;
using System.Linq;

namespace Scratch
{
    public class Program
    {
        static void Main()
        {
            // Use this when testing SQL Server Compact 4.0
            //private const string ProviderName = "System.Data.SqlServerCe.4.0";
            //string ConnectionString = @"Data Source=|DataDirectory|\NorthwindSqlCe40.sdf";   // Uses last connection string in config if not specified


            //GeneratedTextTransformation.Inflector.PluralizationService = new SpanishPluralizationService();
            //GeneratedTextTransformation.Inflector.PluralizationService = null;
            GeneratedTextTransformation.Inflector.PluralizationService = new EnglishPluralizationService();

            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            using (var sw = new StreamWriter(Path.Combine(path, "efrpg.txt")))
            {
                var x = new GeneratedTextTransformation();
                var factory = x.GetDbProviderFactory();
                var tables = x.LoadTables(factory);
                foreach (var table in tables.Where(t => !t.IsMapping))
                {
                    Console.WriteLine(table.NameHumanCase);
                    sw.WriteLine(table.NameHumanCase);

                    foreach (var col in table.Columns)
                    {
                        if (!string.IsNullOrWhiteSpace(col.Entity))
                            Console.WriteLine("  " + col.Entity);

                        if (!string.IsNullOrWhiteSpace(col.EntityFk))
                        {
                            Console.WriteLine("  " + col.EntityFk);
                            sw.WriteLine("  " + col.EntityFk);
                        }
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

                    var fks = table.Columns.Where(col => !string.IsNullOrWhiteSpace(col.ConfigFk)).ToList();
                    if (fks.Count > 0)
                        Console.WriteLine("  // FK's");
                    foreach (var col in fks)
                    {
                        Console.WriteLine("  " + col.ConfigFk);
                        sw.WriteLine("  " + col.ConfigFk);
                    }
                    Console.WriteLine();
                    sw.WriteLine();
                }
            }
        }
    }
}