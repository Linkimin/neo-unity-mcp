// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;

namespace Neo.UnityMcp.Registry
{
    internal sealed class ToolInvocationException : Exception
    {
        public ToolInvocationException(string message)
            : base(message)
        {
        }
    }
}
