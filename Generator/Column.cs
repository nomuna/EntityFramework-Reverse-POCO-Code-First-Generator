using System;
using System.Collections.Generic;
using System.Linq;

namespace Generator
{
    public class Column
    {
        public string Name; // Raw name of the column as obtained from the database
        public string NameHumanCase; // Name adjusted for C# output
        public string DisplayName; // Name used in the data annotation [Display(Name = "<DisplayName> goes here")]
        public bool OverrideModifier = false; // Adds 'override' to the property declaration

        public int DateTimePrecision;
        public string Default;
        public int MaxLength;
        public int Precision;
        public string SqlPropertyType;
        public string PropertyType;
        public int Scale;
        public int Ordinal;
        public int PrimaryKeyOrdinal;
        public string ExtendedProperty;
        public string SummaryComments;
        public string UniqueIndexName;

        public bool IsIdentity;
        public bool IsNullable;
        public bool IsPrimaryKey;
        public bool IsUniqueConstraint;
        public bool IsUnique;
        public bool IsStoreGenerated;
        public bool IsRowVersion;
        public bool IsConcurrencyToken; //  Manually set via callback
        public bool IsFixedLength;
        public bool IsUnicode;
        public bool IsMaxLength;
        public bool Hidden;
        public bool IsForeignKey;

        public string Config;
        public List<string> ConfigFk = new List<string>();
        public string Entity;
        public List<PropertyAndComments> EntityFk = new List<PropertyAndComments>();

        public List<string> DataAnnotations;
        public List<Index> Indexes = new List<Index>();

        public Table ParentTable;

        public static readonly List<string> NotNullable = new List<string>
        {
            "string",
            "byte[]",
            "datatable",
            "system.data.datatable",
            "object",
            "microsoft.sqlserver.types.sqlgeography",
            "microsoft.sqlserver.types.sqlgeometry",
            "system.data.entity.spatial.dbgeography",
            "system.data.entity.spatial.dbgeometry",
            "system.data.entity.hierarchy.hierarchyid"
        };

        public void ResetNavigationProperties()
        {
            ConfigFk = new List<string>();
            EntityFk = new List<PropertyAndComments>();
        }

        public bool IsColumnNullable()
        {
            return IsNullable && !NotNullable.Contains(PropertyType.ToLower());
        }

        public bool IsComputed()
        {
            return IsStoreGenerated && !IsIdentity;
        }

        public void CleanUpDefault()
        {
            if (string.IsNullOrWhiteSpace(Default))
            {
                Default = string.Empty;
                return;
            }

            // Remove outer brackets
            while (Default.First() == '(' && Default.Last() == ')' && Default.Length > 2)
            {
                Default = Default.Substring(1, Default.Length - 2);
            }

            // Remove unicode prefix
            if (IsUnicode && Default.StartsWith("N") &&
                !Default.Equals("NULL", StringComparison.InvariantCultureIgnoreCase))
                Default = Default.Substring(1, Default.Length - 1);

            if (Default.First() == '\'' && Default.Last() == '\'' && Default.Length >= 2)
                Default = string.Format("\"{0}\"", Default.Substring(1, Default.Length - 2));

            var lower = Default.ToLower();
            var lowerPropertyType = PropertyType.ToLower();

            // Cleanup default
            switch (lowerPropertyType)
            {
                case "bool":
                    Default = (Default == "0" || lower == "\"false\"" || lower == "false") ? "false" : "true";
                    break;

                case "string":
                case "datetime":
                case "datetime2":
                case "system.datetime":
                case "timespan":
                case "system.timespan":
                case "datetimeoffset":
                case "system.datetimeoffset":
                    if (Default.First() != '"')
                        Default = string.Format("\"{0}\"", Default);
                    if (Default.Contains('\\') || Default.Contains('\r') || Default.Contains('\n'))
                        Default = "@" + Default;
                    else
                        Default = string.Format("\"{0}\"",
                            Default.Substring(1, Default.Length - 2)
                                .Replace("\"", "\\\"")); // #281 Default values must be escaped if contain double quotes
                    break;

                case "long":
                case "short":
                case "int":
                case "double":
                case "float":
                case "decimal":
                case "byte":
                case "guid":
                case "system.guid":
                    if (Default.First() == '\"' && Default.Last() == '\"' && Default.Length > 2)
                        Default = Default.Substring(1, Default.Length - 2);
                    break;

                case "byte[]":
                case "system.data.entity.spatial.dbgeography":
                case "system.data.entity.spatial.dbgeometry":
                    Default = string.Empty;
                    break;
            }

            // Ignore defaults we cannot interpret (we would need SQL to C# compiler)
            if (lower.StartsWith("create default"))
            {
                Default = string.Empty;
                return;
            }

            if (string.IsNullOrWhiteSpace(Default))
            {
                Default = string.Empty;
                return;
            }

            // Validate default
            switch (lowerPropertyType)
            {
                case "long":
                    long l;
                    if (!long.TryParse(Default, out l))
                        Default = string.Empty;
                    break;

                case "short":
                    short s;
                    if (!short.TryParse(Default, out s))
                        Default = string.Empty;
                    break;

                case "int":
                    int i;
                    if (!int.TryParse(Default, out i))
                        Default = string.Empty;
                    break;

                case "datetime":
                case "datetime2":
                case "system.datetime":
                    DateTime dt;
                    if (!DateTime.TryParse(Default, out dt))
                        Default = (lower.Contains("getdate()") || lower.Contains("sysdatetime"))
                            ? "System.DateTime.Now"
                            : (lower.Contains("getutcdate()") || lower.Contains("sysutcdatetime"))
                                ? "System.DateTime.UtcNow"
                                : string.Empty;
                    else
                        Default = string.Format("System.DateTime.Parse({0})", Default);
                    break;

                case "datetimeoffset":
                case "system.datetimeoffset":
                    DateTimeOffset dto;
                    if (!DateTimeOffset.TryParse(Default, out dto))
                        Default = lower.Contains("sysdatetimeoffset")
                            ? "System.DateTimeOffset.Now"
                            : lower.Contains("sysutcdatetime")
                                ? "System.DateTimeOffset.UtcNow"
                                : string.Empty;
                    else
                        Default = string.Format("System.DateTimeOffset.Parse({0})", Default);
                    break;

                case "timespan":
                case "system.timespan":
                    TimeSpan ts;
                    Default = TimeSpan.TryParse(Default, out ts)
                        ? string.Format("System.TimeSpan.Parse({0})", Default)
                        : string.Empty;
                    break;

                case "double":
                    double d;
                    if (!double.TryParse(Default, out d))
                        Default = string.Empty;
                    if (Default.ToLowerInvariant().EndsWith("."))
                        Default += "0";
                    break;

                case "float":
                    float f;
                    if (!float.TryParse(Default, out f))
                        Default = string.Empty;
                    if (!Default.ToLowerInvariant().EndsWith("f"))
                        Default += "f";
                    break;

                case "decimal":
                    decimal dec;
                    if (!decimal.TryParse(Default, out dec))
                        Default = string.Empty;
                    else
                        Default += "m";
                    break;

                case "byte":
                    byte b;
                    if (!byte.TryParse(Default, out b))
                        Default = string.Empty;
                    break;

                case "bool":
                    bool x;
                    if (!bool.TryParse(Default, out x))
                        Default = string.Empty;
                    break;

                case "string":
                    if (lower.Contains("newid()") || lower.Contains("newsequentialid()"))
                        Default = "System.Guid.NewGuid().ToString()";
                    if (lower.StartsWith("space("))
                        Default = "\"\"";
                    if (lower == "null")
                        Default = string.Empty;
                    break;

                case "guid":
                case "system.guid":
                    if (lower.Contains("newid()") || lower.Contains("newsequentialid()"))
                        Default = "System.Guid.NewGuid()";
                    else if (lower.Contains("null"))
                        Default = "null";
                    else
                        Default = string.Format("System.Guid.Parse(\"{0}\")", Default);
                    break;
            }
        }

        public string WrapIfNullable()
        {
            if (!IsColumnNullable())
                return PropertyType;

            return string.Format(Settings.NullableShortHand ? "{0}?" : "System.Nullable<{0}>", PropertyType);
        }
    }
}