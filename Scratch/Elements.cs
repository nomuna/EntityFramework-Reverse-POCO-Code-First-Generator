using System;

namespace Scratch
{
    // Settings to allow selective code generation
    [Flags]
    public enum Elements
    {
        None = 0,
        Poco = 1,
        Context = 2,
        UnitOfWork = 4,
        PocoConfiguration = 8
    };
}