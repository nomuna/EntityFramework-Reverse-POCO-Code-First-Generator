namespace Generator.Writer
{
    public class WriterEf6 : Writer
    {
        public WriterEf6(GeneratedTextTransformation outer)
            : base(outer)
        {
        }

        public override void Init()
        {
            base.Init();

            if (Settings.ElementsToGenerate.HasFlag(Elements.Poco) || Settings.ElementsToGenerate.HasFlag(Elements.PocoConfiguration))
            {
                if (Settings.UseDataAnnotations)
                {
                    _headerInfo.AppendLine("using System.ComponentModel.DataAnnotations");
                    _headerInfo.AppendLine("using System.ComponentModel.DataAnnotations.Schema");
                    _headerInfo.AppendLine(string.Empty);
                }
            }

            _headerInfo.AppendLine("namespace " + Settings.Namespace);
            _headerInfo.AppendLine("{");
        }

        public override void Interface()
        {
        }

        public override void DatabaseContext()
        {
        }

        public override void DatabaseContextFactory()
        {
        }

        public override void FakeContextFactory()
        {
        }

        public override void PocoClasses()
        {
        }

        public override void PocoConfiguration()
        {
        }

        public override void StoredProcedureReturnModels()
        {
        }

        public override void Header()
        {
            base.Header();
        }

        public override void Footer()
        {
            base.Footer();
        }
    }
}