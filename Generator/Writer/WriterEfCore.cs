namespace Generator.Writer
{
    public class WriterEfCore : Writer
    {
        public WriterEfCore(GeneratedTextTransformation outer)
            : base(outer)
        {
        }

        public override void Init()
        {
            base.Init();

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