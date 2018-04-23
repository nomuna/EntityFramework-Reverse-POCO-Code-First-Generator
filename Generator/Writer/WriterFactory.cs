using System;

namespace Generator.Writer
{
    public static class WriterFactory
    {
        public static Writer Create(GeneratedTextTransformation outer)
        {
            switch (Settings.GeneratorType)
            {
                case GeneratorType.Ef6:
                    return new WriterEf6(outer);

                case GeneratorType.EfCore:
                    return new WriterEfCore(outer);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
