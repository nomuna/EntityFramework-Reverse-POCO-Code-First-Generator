using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Generator.Generators
{
    public class GeneratorEf6 : Generator
    {
        public GeneratorEf6(GeneratedTextTransformation outer) : base(outer)
        {
        }

        protected override void SetupEntity(Column c)
        {
            var comments = string.Empty;
            if (Settings.IncludeComments != CommentsStyle.None)
            {
                comments = c.Name;
                if (c.IsPrimaryKey)
                {
                    if (c.IsUniqueConstraint)
                        comments += " (Primary key via unique index " + c.UniqueIndexName + ")";
                    else
                        comments += " (Primary key)";
                }

                if (c.MaxLength > 0)
                    comments += string.Format(" (length: {0})", c.MaxLength);
            }

            var inlineComments = Settings.IncludeComments == CommentsStyle.AtEndOfField ? " // " + comments : string.Empty;

            c.SummaryComments = string.Empty;
            if (Settings.IncludeComments == CommentsStyle.InSummaryBlock && !string.IsNullOrEmpty(comments))
            {
                c.SummaryComments = comments;
            }
            if (Settings.IncludeExtendedPropertyComments == CommentsStyle.InSummaryBlock &&
                !string.IsNullOrEmpty(c.ExtendedProperty))
            {
                if (string.IsNullOrEmpty(c.SummaryComments))
                    c.SummaryComments = c.ExtendedProperty;
                else
                    c.SummaryComments += ". " + c.ExtendedProperty;
            }

            if (Settings.IncludeExtendedPropertyComments == CommentsStyle.AtEndOfField && !string.IsNullOrEmpty(c.ExtendedProperty))
            {
                if (string.IsNullOrEmpty(inlineComments))
                    inlineComments = " // " + c.ExtendedProperty;
                else
                    inlineComments += ". " + c.ExtendedProperty;
            }

            var initialization = Settings.UsePropertyInitializers
                ? (string.IsNullOrWhiteSpace(c.Default) ? string.Empty : string.Format(" = {0};", c.Default))
                : string.Empty;

            c.Entity = string.Format("public {0}{1} {2} {{ get; {3}set; }}{4}{5}", (c.OverrideModifier ? "override " : string.Empty),
                c.WrapIfNullable(), c.NameHumanCase,
                Settings.UsePrivateSetterForComputedColumns && c.IsComputed() ? "private " : string.Empty, initialization,
                inlineComments);
        }

        protected override void SetupConfig(Column c)
        {
            c.DataAnnotations = new List<string>();
            string databaseGeneratedOption = null;
            var schemaReference = Settings.UseDataAnnotations ? string.Empty : "System.ComponentModel.DataAnnotations.Schema.";

            bool isNewSequentialId = !string.IsNullOrEmpty(c.Default) && c.Default.ToLower().Contains("newsequentialid");

            if (c.IsIdentity || isNewSequentialId)
            {
                if (Settings.UseDataAnnotations || isNewSequentialId)
                    c.DataAnnotations.Add("DatabaseGenerated(DatabaseGeneratedOption.Identity)");
                else
                    databaseGeneratedOption = string.Format(".HasDatabaseGeneratedOption({0}DatabaseGeneratedOption.Identity)", schemaReference);
            }
            else if (c.IsComputed())
            {
                if (Settings.UseDataAnnotations)
                    c.DataAnnotations.Add("DatabaseGenerated(DatabaseGeneratedOption.Computed)");
                else
                    databaseGeneratedOption = string.Format(".HasDatabaseGeneratedOption({0}DatabaseGeneratedOption.Computed)", schemaReference);
            }
            else if (c.IsPrimaryKey)
            {
                if (Settings.UseDataAnnotations)
                    c.DataAnnotations.Add("DatabaseGenerated(DatabaseGeneratedOption.None)");
                else
                    databaseGeneratedOption = string.Format(".HasDatabaseGeneratedOption({0}DatabaseGeneratedOption.None)", schemaReference);
            }

            var sb = new StringBuilder();

            if (Settings.UseDataAnnotations)
                c.DataAnnotations.Add(string.Format("Column(@\"{0}\", Order = {1}, TypeName = \"{2}\")", c.Name, c.Ordinal, c.SqlPropertyType));
            else
                sb.AppendFormat(".HasColumnName(@\"{0}\").HasColumnType(\"{1}\")", c.Name, c.SqlPropertyType);

            if (Settings.UseDataAnnotations && c.Indexes.Any())
            {
                foreach (var index in c.Indexes)
                {
                    c.DataAnnotations.Add(string.Format("Index(@\"{0}\", {1}, IsUnique = {2}, IsClustered = {3})",
                        index.IndexName,
                        index.KeyOrdinal,
                        index.IsUnique ? "true" : "false",
                        index.IsClustered ? "true" : "false"));
                }
            }

            if (c.IsNullable)
            {
                sb.Append(".IsOptional()");
            }
            else
            {
                if (Settings.UseDataAnnotations)
                {
                    if (!c.IsComputed())
                        c.DataAnnotations.Add("Required");
                }
                else
                    sb.Append(".IsRequired()");
            }

            if (c.IsFixedLength || c.IsRowVersion)
            {
                sb.Append(".IsFixedLength()");
                // DataAnnotations.Add("????");
            }

            if (!c.IsUnicode)
            {
                sb.Append(".IsUnicode(false)");
                // DataAnnotations.Add("????");
            }

            if (!c.IsMaxLength && c.MaxLength > 0)
            {
                var doNotSpecifySize = (_schemaReader.DoNotSpecifySizeForMaxLength && c.MaxLength > 4000); // Issue #179

                if (Settings.UseDataAnnotations)
                {
                    c.DataAnnotations.Add(doNotSpecifySize ? "MaxLength" : string.Format("MaxLength({0})", c.MaxLength));

                    if (c.PropertyType.Equals("string", StringComparison.InvariantCultureIgnoreCase))
                        c.DataAnnotations.Add(string.Format("StringLength({0})", c.MaxLength));
                }
                else
                {
                    if (doNotSpecifySize)
                        sb.Append(".HasMaxLength(null)");
                    else
                        sb.AppendFormat(".HasMaxLength({0})", c.MaxLength);
                }
            }

            if (c.IsMaxLength)
            {
                if (Settings.UseDataAnnotations)
                    c.DataAnnotations.Add("MaxLength");
                else
                    sb.Append(".IsMaxLength()");
            }

            if ((c.Precision > 0 || c.Scale > 0) && c.PropertyType == "decimal")
            {
                sb.AppendFormat(".HasPrecision({0},{1})", c.Precision, c.Scale);
                // DataAnnotations.Add("????");
            }

            if (c.IsRowVersion)
            {
                if (Settings.UseDataAnnotations)
                    c.DataAnnotations.Add("Timestamp");
                else
                    sb.Append(".IsRowVersion()");
            }

            if (c.IsConcurrencyToken)
            {
                sb.Append(".IsConcurrencyToken()");
                // DataAnnotations.Add("????");
            }

            if (databaseGeneratedOption != null)
                sb.Append(databaseGeneratedOption);

            var config = sb.ToString();
            if (!string.IsNullOrEmpty(config))
                c.Config = string.Format("Property(x => x.{0}){1};", c.NameHumanCase, config);

            if (!Settings.UseDataAnnotations)
                return; // Only data annotations below this point

            if (c.IsPrimaryKey)
                c.DataAnnotations.Add("Key");

            string value;
            if (Settings.ColumnNameToDataAnnotation.TryGetValue(c.NameHumanCase.ToLowerInvariant(), out value))
                c.DataAnnotations.Add(value);

            c.DataAnnotations.Add(string.Format("Display(Name = \"{0}\")", c.DisplayName));
        }
    }
}