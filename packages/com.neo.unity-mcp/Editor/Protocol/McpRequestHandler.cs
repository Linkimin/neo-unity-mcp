// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Neo.UnityMcp.Registry;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Neo.UnityMcp.Protocol
{
    internal sealed class McpRequestHandler
    {
        private readonly string _serverName;
        private readonly string _serverVersion;
        private readonly string _projectIdentity;
        private readonly ToolRegistry _toolRegistry;
        private readonly FunctionExecutionController _executionController;

        public McpRequestHandler(string serverName, string serverVersion, string projectIdentity, ToolRegistry toolRegistry = null)
        {
            _serverName = string.IsNullOrWhiteSpace(serverName) ? "Neo Unity MCP Server" : serverName;
            _serverVersion = string.IsNullOrWhiteSpace(serverVersion) ? "0.0.0" : serverVersion;
            _projectIdentity = projectIdentity ?? string.Empty;
            _toolRegistry = toolRegistry ?? ToolRegistry.CreateDefault();
            _executionController = new FunctionExecutionController(new FunctionInvokerController(_toolRegistry));
        }

        public async Task<McpResponse> HandleRequestAsync(McpRequest request, CancellationToken ct)
        {
            try
            {
                if (request == null)
                    return CreateErrorResponse(null, -32600, "Invalid Request");

                if (request.JsonRpc != "2.0")
                    return CreateErrorResponse(request.Id, -32600, "Invalid Request: jsonrpc must be '2.0'");

                if (string.Equals(request.Method, "initialize", StringComparison.Ordinal))
                    return HandleInitialize(request);

                if (string.Equals(request.Method, "notifications/initialized", StringComparison.Ordinal) ||
                    (request.Method != null && request.Method.StartsWith("notifications/", StringComparison.Ordinal)))
                {
                    return null;
                }

                if (string.Equals(request.Method, "tools/list", StringComparison.Ordinal))
                    return HandleToolsList(request);

                if (string.Equals(request.Method, "tools/call", StringComparison.Ordinal))
                    return await HandleToolsCallAsync(request, ct);

                return CreateErrorResponse(request.Id, -32601, $"Method not found: {request.Method}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Neo MCP Server] Error handling request: {ex.Message}");
                return CreateErrorResponse(request?.Id, -32603, "Internal error");
            }
        }

        private McpResponse HandleInitialize(McpRequest request)
        {
            var result = new Dictionary<string, object>
            {
                ["protocolVersion"] = "2024-11-05",
                ["serverInfo"] = new Dictionary<string, object>
                {
                    ["name"] = _serverName,
                    ["version"] = _serverVersion
                },
                ["capabilities"] = new Dictionary<string, object>
                {
                    ["tools"] = new Dictionary<string, object>()
                },
                ["neo"] = new Dictionary<string, object>
                {
                    ["projectIdentity"] = _projectIdentity,
                    ["projectIdentityVersion"] = NeoProjectIdentity.IdentityVersion
                }
            };

            return new McpResponse { Id = request.Id, Result = result };
        }

        private McpResponse HandleToolsList(McpRequest request)
        {
            var tools = new JArray();
            foreach (var tool in _toolRegistry.Tools)
                tools.Add(ToolSchemaBuilder.Build(tool));

            return new McpResponse
            {
                Id = request.Id,
                Result = new JObject { ["tools"] = tools }
            };
        }

        private async Task<McpResponse> HandleToolsCallAsync(McpRequest request, CancellationToken ct)
        {
            var nameToken = request.Params?["name"];
            var toolName = nameToken?.Type == JTokenType.String ? (string)nameToken : null;
            if (string.IsNullOrWhiteSpace(toolName))
                return CreateErrorResponse(request.Id, -32602, "Invalid params: 'name' is required");

            var argumentsToken = request.Params?["arguments"];
            var arguments = new JObject();
            if (argumentsToken != null)
            {
                if (argumentsToken.Type != JTokenType.Object)
                    return CreateErrorResponse(request.Id, -32602, "Invalid params: 'arguments' must be an object");

                arguments = (JObject)argumentsToken;
            }

            try
            {
                ct.ThrowIfCancellationRequested();
                var result = await _executionController.ExecuteAsync(toolName, arguments);
                return new McpResponse
                {
                    Id = request.Id,
                    Result = CreateToolResult(result, false)
                };
            }
            catch (ToolExecutionException ex)
            {
                // Tool body failed: report as a tool result with isError=true (MCP semantics),
                // not as a JSON-RPC protocol error.
                return new McpResponse
                {
                    Id = request.Id,
                    Result = CreateToolResult(ex.Message, true)
                };
            }
            catch (ToolInvocationException ex)
            {
                // Protocol-level problem (unknown tool / invalid or missing arguments).
                return CreateErrorResponse(request.Id, -32602, ex.Message);
            }
        }

        private static JObject CreateToolResult(object result, bool isError)
        {
            var text = result == null ? "OK" : result as string;
            if (text == null)
                text = JToken.FromObject(result).ToString(Newtonsoft.Json.Formatting.None);

            return new JObject
            {
                ["content"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "text",
                        ["text"] = text
                    }
                },
                ["isError"] = isError
            };
        }

        private static McpResponse CreateErrorResponse(JToken requestId, int code, string message)
        {
            return new McpResponse
            {
                Id = requestId,
                Error = new McpError { Code = code, Message = message }
            };
        }
    }
}
