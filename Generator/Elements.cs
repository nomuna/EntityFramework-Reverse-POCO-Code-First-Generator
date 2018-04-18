using System;

namespace Generator
{
    // Settings to allow selective code generation
    [Flags]
    public enum Elements
    {
        None = 0,
        Poco = 1,
        Context = 2,
        Interface = 4,
        PocoConfiguration = 8
    };
}