// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;

namespace Neo.UnityMcp.Registry
{
    // Raised when a tool method itself fails at runtime (as opposed to a
    // protocol-level problem like an unknown tool or invalid arguments).
    // The protocol layer maps this to a tool result with isError=true,
    // not to a JSON-RPC error — matching MCP tools/call semantics.
    internal sealed class ToolExecutionException : Exception
    {
        public ToolExecutionException(string message)
            : base(message)
        {
        }
    }
}
