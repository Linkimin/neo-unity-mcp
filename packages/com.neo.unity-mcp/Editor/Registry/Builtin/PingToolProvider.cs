// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

namespace Neo.UnityMcp.Registry.Builtin
{
    [NeoToolProvider("Diagnostics")]
    internal static class PingToolProvider
    {
        [NeoTool("ping", "Returns pong.")]
        [ReadOnlyTool]
        public static string Ping()
        {
            return "pong";
        }
    }
}
