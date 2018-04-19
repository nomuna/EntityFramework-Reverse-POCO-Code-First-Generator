using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Generator.Writer
{
    public abstract class Writer
    {
        private readonly GeneratedTextTransformation _outer;
        private readonly Dictionary<string, bool> _supportedFrameworkVersionCache;
        private readonly Version _version;
        private readonly string _codeGeneratedAttribute;

        protected Writer(GeneratedTextTransformation outer)
        {
            _outer = outer;
            _supportedFrameworkVersionCache = new Dictionary<string, bool>();
            _version = new Version(3, 0, 0);
            _codeGeneratedAttribute = string.Format("[System.CodeDom.Compiler.GeneratedCode(\"EF.Reverse.POCO.Generator\", \"{0}\")]", _version);
        }

        public void something()
        {

        }

        public bool IsSupportedFrameworkVersion(string frameworkVersion)
        {
            if (_supportedFrameworkVersionCache.ContainsKey(frameworkVersion))
                return _supportedFrameworkVersionCache[frameworkVersion];

            var nfi = CultureInfo.InvariantCulture.NumberFormat;
            var isSupported = float.Parse(frameworkVersion, nfi);
            var result = isSupported <= Settings.TargetFrameworkVersion;
            _supportedFrameworkVersionCache.Add(frameworkVersion, result);
            return result;
        }

        #region Callbacks

        // Callbacks **********************************************************************************************************************
        // This method will be called right before we write the POCO header.
        public void WritePocoClassAttributes(Table t)
        {
            if (Settings.UseDataAnnotations)
            {
                foreach (var dataAnnotation in t.DataAnnotations)
                {
                    _outer.WriteLine("    [" + dataAnnotation + "]");
                }
            }

            // Example:
            // if(t.ClassName.StartsWith("Order"))
            //     WriteLine("    [SomeAttribute]");
        }

        // This method will be called right before we write the POCO header.
        public void WritePocoClassExtendedComments(Table t)
        {
            if (Settings.IncludeExtendedPropertyComments != CommentsStyle.None && t.ExtendedProperty.Any())
            {
                var lines = t.ExtendedProperty
                    .SelectMany(x => x.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                    .ToList();

                _outer.WriteLine("    ///<summary>");
                foreach (var line in lines)
                {
                    _outer.WriteLine("    /// {0}", System.Security.SecurityElement.Escape(line));
                }
                _outer.WriteLine("    ///</summary>");
            }
        }

        // Writes optional base classes
        public string WritePocoBaseClasses(Table t)
        {
            //if (t.ClassName == "User")
            //    return ": IdentityUser<int, CustomUserLogin, CustomUserRole, CustomUserClaim>";

            // Or use the maker class to dynamically build more complex definitions
            /* Example:
            var r = new BaseClassMaker("POCO.Sample.Data.MetaModelObject");
            r.AddInterface("POCO.Sample.Data.IObjectWithTableName");
            r.AddInterface("POCO.Sample.Data.IObjectWithId",
                t.Columns.Any(x => x.IsPrimaryKey && !x.IsNullable && x.NameHumanCase.Equals("Id", StringComparison.InvariantCultureIgnoreCase) && x.PropertyType == "long"));
            r.AddInterface("POCO.Sample.Data.IObjectWithUserId",
                t.Columns.Any(x => !x.IsPrimaryKey && !x.IsNullable && x.NameHumanCase.Equals("UserId", StringComparison.InvariantCultureIgnoreCase) && x.PropertyType == "long"));
            return r.ToString();
            */

            return "";
        }

        // Writes any boilerplate stuff inside the POCO class
        public void WritePocoBaseClassBody(Table t)
        {
            // Do nothing by default
            // Example:
            // WriteLine("        // " + t.ClassName);
        }

        public string WritePocoColumn(Column c)
        {
            var commentWritten = false;
            if ((Settings.IncludeExtendedPropertyComments == CommentsStyle.InSummaryBlock ||
                 Settings.IncludeComments == CommentsStyle.InSummaryBlock) &&
                !string.IsNullOrEmpty(c.SummaryComments))
            {
                _outer.WriteLine(string.Empty);
                _outer.WriteLine("        ///<summary>");
                _outer.WriteLine("        /// {0}", System.Security.SecurityElement.Escape(c.SummaryComments));
                _outer.WriteLine("        ///</summary>");
                commentWritten = true;
            }
            if (Settings.UseDataAnnotations)
            {
                if (c.Ordinal > 1 && !commentWritten)
                    _outer.WriteLine(string.Empty);    // Leave a blank line before the next property

                foreach (var dataAnnotation in c.DataAnnotations)
                {
                    _outer.WriteLine("        [" + dataAnnotation + "]");
                }
            }

            // Example of adding a [Required] data annotation attribute to all non-null fields
            //if (!c.IsNullable)
            //    return "        [System.ComponentModel.DataAnnotations.Required] " + c.Entity;

            return "        " + c.Entity;
        }

        #endregion

    }
}