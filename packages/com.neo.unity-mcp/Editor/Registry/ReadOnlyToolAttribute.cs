// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;

namespace Neo.UnityMcp.Registry
{
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class ReadOnlyToolAttribute : Attribute
    {
    }
}
