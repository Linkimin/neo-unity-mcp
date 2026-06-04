// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Threading;
using System.Threading.Tasks;
using Neo.UnityMcp.Protocol;

namespace Neo.UnityMcp.Transport
{
    internal interface IMcpTransport : IDisposable
    {
        bool IsRunning { get; }
        event Action<McpRequest, Action<McpResponse>> OnRequestReceived;
        Task<bool> StartAsync(CancellationToken ct = default);
        Task StopAsync();
        void Stop();
    }
}
