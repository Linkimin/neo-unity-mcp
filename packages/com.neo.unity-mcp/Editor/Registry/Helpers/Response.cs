// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

namespace Neo.UnityMcp.Registry
{
    // Standard structured return type for tool providers (drop-in Funplay contract:
    // lowercase members are serialized verbatim into the tool result payload).
    // Ported as part of Task 3; consumed by the execution core in Task 4.
    public sealed class Response
    {
        public bool success { get; set; }
        public string message { get; set; }
        public object data { get; set; }

        public static Response Success(string message = "OK", object data = null)
        {
            return new Response
            {
                success = true,
                message = message ?? string.Empty,
                data = data
            };
        }

        public static Response Error(string message, object data = null)
        {
            return new Response
            {
                success = false,
                message = message ?? string.Empty,
                data = data
            };
        }
    }
}
