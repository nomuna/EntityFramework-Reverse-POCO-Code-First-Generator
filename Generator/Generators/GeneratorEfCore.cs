namespace Generator.Generators
{
    public class GeneratorEfCore : Generator
    {
        public GeneratorEfCore(GeneratedTextTransformation outer) : base(outer)
        {
        }

        protected override void SetupEntity(Column c)
        {
            throw new System.NotImplementedException();
        }

        protected override void SetupConfig(Column c)
        {
            throw new System.NotImplementedException();
        }
    }
}