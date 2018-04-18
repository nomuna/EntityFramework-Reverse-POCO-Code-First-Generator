using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        private void SetupEntity()
        {
            var comments = string.Empty;
            if (Settings.IncludeComments != CommentsStyle.None)
            {
                comments = Name;
                if (IsPrimaryKey)
                {
                    if (IsUniqueConstraint)
                        comments += " (Primary key via unique index " + UniqueIndexName + ")";
                    else
                        comments += " (Primary key)";
                }

                if (MaxLength > 0)
                    comments += string.Format(" (length: {0})", MaxLength);
            }

            var inlineComments = Settings.IncludeComments == CommentsStyle.AtEndOfField ? " // " + comments : string.Empty;

            SummaryComments = string.Empty;
            if (Settings.IncludeComments == CommentsStyle.InSummaryBlock && !string.IsNullOrEmpty(comments))
            {
                SummaryComments = comments;
            }
            if (Settings.IncludeExtendedPropertyComments == CommentsStyle.InSummaryBlock &&
                !string.IsNullOrEmpty(ExtendedProperty))
            {
                if (string.IsNullOrEmpty(SummaryComments))
                    SummaryComments = ExtendedProperty;
                else
                    SummaryComments += ". " + ExtendedProperty;
            }

            if (Settings.IncludeExtendedPropertyComments == CommentsStyle.AtEndOfField && !string.IsNullOrEmpty(ExtendedProperty))
            {
                if (string.IsNullOrEmpty(inlineComments))
                    inlineComments = " // " + ExtendedProperty;
                else
                    inlineComments += ". " + ExtendedProperty;
            }

            var initialization = Settings.UsePropertyInitializers
                ? (string.IsNullOrWhiteSpace(Default) ? string.Empty : string.Format(" = {0};", Default))
                : string.Empty;

            Entity = string.Format("public {0}{1} {2} {{ get; {3}set; }}{4}{5}", (OverrideModifier ? "override " : string.Empty),
                WrapIfNullable(PropertyType), NameHumanCase,
                Settings.UsePrivateSetterForComputedColumns && IsComputed() ? "private " : string.Empty, initialization,
                inlineComments);
        }

        private string WrapIfNullable(string propType)
        {
            if (!IsColumnNullable())
                return propType;

            return string.Format(Settings.NullableShortHand ? "{0}?" : "System.Nullable<{0}>", propType);
        }

        public bool IsColumnNullable()
        {
            return IsNullable && !NotNullable.Contains(PropertyType.ToLower());
        }

        private bool IsComputed()
        {
            return IsStoreGenerated && !IsIdentity;
        }

        private void SetupConfig()
        {
            DataAnnotations = new List<string>();
            string databaseGeneratedOption = null;
            var schemaReference = Settings.UseDataAnnotations ? string.Empty : "System.ComponentModel.DataAnnotations.Schema.";

            bool isNewSequentialId = !string.IsNullOrEmpty(Default) && Default.ToLower().Contains("newsequentialid");

            if (IsIdentity || isNewSequentialId)
            {
                if (Settings.UseDataAnnotations || isNewSequentialId)
                    DataAnnotations.Add("DatabaseGenerated(DatabaseGeneratedOption.Identity)");
                else
                    databaseGeneratedOption = string.Format(".HasDatabaseGeneratedOption({0}DatabaseGeneratedOption.Identity)", schemaReference);
            }
            else if (IsComputed())
            {
                if (Settings.UseDataAnnotations)
                    DataAnnotations.Add("DatabaseGenerated(DatabaseGeneratedOption.Computed)");
                else
                    databaseGeneratedOption = string.Format(".HasDatabaseGeneratedOption({0}DatabaseGeneratedOption.Computed)", schemaReference);
            }
            else if (IsPrimaryKey)
            {
                if (Settings.UseDataAnnotations)
                    DataAnnotations.Add("DatabaseGenerated(DatabaseGeneratedOption.None)");
                else
                    databaseGeneratedOption = string.Format(".HasDatabaseGeneratedOption({0}DatabaseGeneratedOption.None)", schemaReference);
            }

            var sb = new StringBuilder();

            if (Settings.UseDataAnnotations)
                DataAnnotations.Add(string.Format("Column(@\"{0}\", Order = {1}, TypeName = \"{2}\")", Name, Ordinal, SqlPropertyType));
            else
                sb.AppendFormat(".HasColumnName(@\"{0}\").HasColumnType(\"{1}\")", Name, SqlPropertyType);

            if (Settings.UseDataAnnotations && Indexes.Any())
            {
                foreach (var index in Indexes)
                {
                    DataAnnotations.Add(string.Format("Index(@\"{0}\", {1}, IsUnique = {2}, IsClustered = {3})",
                        index.IndexName,
                        index.KeyOrdinal,
                        index.IsUnique ? "true" : "false",
                        index.IsClustered ? "true" : "false"));
                }
            }

            if (IsNullable)
            {
                sb.Append(".IsOptional()");
            }
            else
            {
                if (Settings.UseDataAnnotations)
                {
                    if (!IsComputed())
                        DataAnnotations.Add("Required");
                }
                else
                    sb.Append(".IsRequired()");
            }

            if (IsFixedLength || IsRowVersion)
            {
                sb.Append(".IsFixedLength()");
                // DataAnnotations.Add("????");
            }

            if (!IsUnicode)
            {
                sb.Append(".IsUnicode(false)");
                // DataAnnotations.Add("????");
            }

            if (!IsMaxLength && MaxLength > 0)
            {
                var doNotSpecifySize = (Settings.IsSqlCe && MaxLength > 4000); // Issue #179

                if (Settings.UseDataAnnotations)
                {
                    DataAnnotations.Add(doNotSpecifySize ? "MaxLength" : string.Format("MaxLength({0})", MaxLength));

                    if (PropertyType.Equals("string", StringComparison.InvariantCultureIgnoreCase))
                        DataAnnotations.Add(string.Format("StringLength({0})", MaxLength));
                }
                else
                {
                    if (doNotSpecifySize)
                        sb.Append(".HasMaxLength(null)");
                    else
                        sb.AppendFormat(".HasMaxLength({0})", MaxLength);
                }
            }

            if (IsMaxLength)
            {
                if (Settings.UseDataAnnotations)
                    DataAnnotations.Add("MaxLength");
                else
                    sb.Append(".IsMaxLength()");
            }

            if ((Precision > 0 || Scale > 0) && PropertyType == "decimal")
            {
                sb.AppendFormat(".HasPrecision({0},{1})", Precision, Scale);
                // DataAnnotations.Add("????");
            }

            if (IsRowVersion)
            {
                if (Settings.UseDataAnnotations)
                    DataAnnotations.Add("Timestamp");
                else
                    sb.Append(".IsRowVersion()");
            }

            if (IsConcurrencyToken)
            {
                sb.Append(".IsConcurrencyToken()");
                // DataAnnotations.Add("????");
            }

            if (databaseGeneratedOption != null)
                sb.Append(databaseGeneratedOption);

            var config = sb.ToString();
            if (!string.IsNullOrEmpty(config))
                Config = string.Format("Property(x => x.{0}){1};", NameHumanCase, config);

            if (!Settings.UseDataAnnotations)
                return; // Only data annotations below this point

            if (IsPrimaryKey)
                DataAnnotations.Add("Key");

            string value;
            if (Settings.ColumnNameToDataAnnotation.TryGetValue(NameHumanCase.ToLowerInvariant(), out value))
                DataAnnotations.Add(value);

            DataAnnotations.Add(string.Format("Display(Name = \"{0}\")", DisplayName));
        }

        public void SetupEntityAndConfig()
        {
            SetupEntity();
            SetupConfig();
        }

        public void CleanUpDefault()
        {
            if (String.IsNullOrWhiteSpace(Default))
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

            string lower = Default.ToLower();
            string lowerPropertyType = PropertyType.ToLower();

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

            if (String.IsNullOrWhiteSpace(Default))
            {
                Default = string.Empty;
                return;
            }

            // Validate default
            switch (lowerPropertyType)
            {
                case "long":
                    long l;
                    if (!Int64.TryParse(Default, out l))
                        Default = string.Empty;
                    break;

                case "short":
                    short s;
                    if (!Int16.TryParse(Default, out s))
                        Default = string.Empty;
                    break;

                case "int":
                    int i;
                    if (!Int32.TryParse(Default, out i))
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
                    if (!Double.TryParse(Default, out d))
                        Default = string.Empty;
                    if (Default.ToLowerInvariant().EndsWith("."))
                        Default += "0";
                    break;

                case "float":
                    float f;
                    if (!Single.TryParse(Default, out f))
                        Default = string.Empty;
                    if (!Default.ToLowerInvariant().EndsWith("f"))
                        Default += "f";
                    break;

                case "decimal":
                    decimal dec;
                    if (!Decimal.TryParse(Default, out dec))
                        Default = string.Empty;
                    else
                        Default += "m";
                    break;

                case "byte":
                    byte b;
                    if (!Byte.TryParse(Default, out b))
                        Default = string.Empty;
                    break;

                case "bool":
                    bool x;
                    if (!Boolean.TryParse(Default, out x))
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
    }
}