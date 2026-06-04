// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using Newtonsoft.Json.Linq;

namespace Neo.UnityMcp.Protocol
{
    internal sealed class McpRequest
    {
        public string JsonRpc { get; set; } = "2.0";
        public JToken Id { get; set; }
        public string Method { get; set; }
        public JObject Params { get; set; } = new JObject();
    }

    internal sealed class McpError
    {
        public int Code { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }

    internal sealed class McpResponse
    {
        public string JsonRpc { get; set; } = "2.0";
        public JToken Id { get; set; }
        public object Result { get; set; }
        public McpError Error { get; set; }
    }
}
