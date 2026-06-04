// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Neo.UnityMcp.Registry
{
    internal sealed class FunctionExecutionController
    {
        private readonly FunctionInvokerController _invoker;

        public FunctionExecutionController(FunctionInvokerController invoker)
        {
            _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
        }

        public Task<object> ExecuteAsync(string toolName, JObject arguments)
        {
            return _invoker.InvokeAsync(toolName, arguments);
        }
    }
}
