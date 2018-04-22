using System;

namespace Generator.Generators
{
    public static class GeneratorFactory
    {
        public static Generator CreateGenerator(GeneratedTextTransformation outer)
        {
            switch (Settings.GeneratorType)
            {
                case GeneratorType.Ef6:
                    return new GeneratorEf6(outer);

                case GeneratorType.EfCore:
                    return new GeneratorEfCore(outer);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}