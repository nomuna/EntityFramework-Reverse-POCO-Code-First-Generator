using System;
using System.IO;
using Generator.Pluralization;

namespace Generator
{
    internal static class Program
    {
        private static void Main()
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
                sw.Write(x.Run());
                sw.Close();
            }
        }
    }
}