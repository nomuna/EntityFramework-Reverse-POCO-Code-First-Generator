using System;

namespace Generator
{
    [Flags]
    public enum CommentsStyle
    {
        None,
        InSummaryBlock,
        AtEndOfField
    };
}