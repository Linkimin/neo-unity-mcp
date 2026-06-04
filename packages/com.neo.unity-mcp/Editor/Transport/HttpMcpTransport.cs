// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Neo.UnityMcp.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Neo.UnityMcp.Transport
{
    internal sealed class HttpMcpTransport : IMcpTransport
    {
        private const int StartRetryAttempts = 40;
        private const int StartRetryDelayMs = 250;
        private const int MaxHeaderBytes = 64 * 1024;

        private readonly int _port;
        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private bool _isRunning;

        public bool IsRunning => _isRunning;
        public event Action<McpRequest, Action<McpResponse>> OnRequestReceived;

        public HttpMcpTransport(int port)
        {
            _port = port > 0 ? port : 8765;
        }

        public async Task<bool> StartAsync(CancellationToken ct = default)
        {
            if (_isRunning)
                return true;

            for (var attempt = 1; attempt <= StartRetryAttempts; attempt++)
            {
                try
                {
                    ct.ThrowIfCancellationRequested();

                    _listener = new TcpListener(IPAddress.Loopback, _port);
                    _listener.Server.NoDelay = true;
                    _listener.Start();

                    _cts = new CancellationTokenSource();
                    _isRunning = true;
                    _ = Task.Run(() => ListenLoopAsync(_cts.Token), _cts.Token);

                    Debug.Log($"[Neo MCP Server] HTTP transport started on http://127.0.0.1:{_port}/");
                    return true;
                }
                catch (OperationCanceledException)
                {
                    CleanupFailedStart();
                    return false;
                }
                catch (Exception ex) when (IsAddressInUse(ex))
                {
                    CleanupFailedStart();
                    if (attempt >= StartRetryAttempts)
                    {
                        Debug.LogWarning($"[Neo MCP Server] Port {_port} is in use; Neo transport did not start. {ex.Message}");
                        return false;
                    }

                    if (attempt == 1)
                    {
                        Debug.LogWarning(
                            $"[Neo MCP Server] Port {_port} is in use; retrying for up to {(StartRetryAttempts * StartRetryDelayMs) / 1000f:0.#} seconds.");
                    }

                    if (!await DelayBeforeRetryAsync(ct).ConfigureAwait(false))
                        return false;
                }
                catch (Exception ex)
                {
                    CleanupFailedStart();
                    Debug.LogError($"[Neo MCP Server] Failed to start HTTP transport: {ex.Message}");
                    return false;
                }
            }

            return false;
        }

        public Task StopAsync()
        {
            Stop();
            return Task.CompletedTask;
        }

        public void Stop()
        {
            if (!_isRunning && _listener == null && _cts == null)
                return;

            try
            {
                _isRunning = false;
                _cts?.Cancel();
                CloseListener();
                Debug.Log("[Neo MCP Server] HTTP transport stopped");
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Neo MCP Server] Error stopping HTTP transport: {ex.Message}");
            }
            finally
            {
                _listener = null;
                _cts?.Dispose();
                _cts = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private async Task ListenLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _isRunning)
                {
                    try
                    {
                        var client = await _listener.AcceptTcpClientAsync();
                        _ = Task.Run(() => HandleClientAsync(client, ct), ct);
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted ||
                                                     ex.SocketErrorCode == SocketError.OperationAborted)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (!ct.IsCancellationRequested && _isRunning)
                            Debug.LogError($"[Neo MCP Server] Error in listen loop: {ex.Message}");
                        break;
                    }
                }
            }
            finally
            {
                if (!ct.IsCancellationRequested && _isRunning)
                    Stop();
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            McpRequest request = null;
            NetworkStream stream = null;
            try
            {
                using (client)
                {
                    stream = client.GetStream();
                    var httpRequest = await ReadHttpRequestAsync(stream, ct);
                    if (httpRequest == null)
                        return;

                    if (string.Equals(httpRequest.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
                    {
                        await SendOptionsResponseAsync(stream, ct);
                        return;
                    }

                    if (!string.Equals(httpRequest.Method, "POST", StringComparison.OrdinalIgnoreCase))
                    {
                        await SendMethodNotAllowedAsync(stream, "POST, OPTIONS", ct);
                        return;
                    }

                    request = ParseJsonRequest(httpRequest.Body);
                    if (request == null)
                    {
                        await SendResponseAsync(stream, CreateErrorResponse(null, -32700, "Parse error"), ct);
                        return;
                    }

                    var requestReceived = OnRequestReceived;
                    if (requestReceived == null)
                    {
                        await SendResponseAsync(stream, CreateErrorResponse(request.Id, -32000, "MCP server is not ready."), ct);
                        return;
                    }

                    var responseTcs = new TaskCompletionSource<McpResponse>();
                    requestReceived.Invoke(request, response => responseTcs.TrySetResult(response));

                    using (var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
                    using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token))
                    {
                        var completedTask = await Task.WhenAny(responseTcs.Task, Task.Delay(-1, linkedCts.Token));
                        if (completedTask == responseTcs.Task)
                        {
                            var response = await responseTcs.Task;
                            if (response == null)
                                await SendAcceptedAsync(stream, ct);
                            else
                                await SendResponseAsync(stream, response, ct);
                        }
                        else
                        {
                            await SendResponseAsync(stream, CreateErrorResponse(request.Id, -32000, "Request timeout"), CancellationToken.None);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Neo MCP Server] Error handling request: {ex.Message}");
                if (stream != null)
                    await SendResponseAsync(stream, CreateErrorResponse(request?.Id, -32603, "Internal error"), CancellationToken.None);
            }
        }

        private static async Task<HttpRequestData> ReadHttpRequestAsync(NetworkStream stream, CancellationToken ct)
        {
            var buffer = new byte[8192];
            var rawRequest = new MemoryStream();
            var headerEnd = -1;

            while (headerEnd < 0)
            {
                var read = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                if (read == 0)
                    return null;

                rawRequest.Write(buffer, 0, read);
                if (rawRequest.Length > MaxHeaderBytes)
                    throw new InvalidOperationException("HTTP header is too large.");

                headerEnd = FindHeaderEnd(rawRequest.GetBuffer(), (int)rawRequest.Length);
            }

            var requestBytes = rawRequest.ToArray();
            var headerText = Encoding.ASCII.GetString(requestBytes, 0, headerEnd);
            var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length == 0)
                return null;

            var requestLineParts = lines[0].Split(' ');
            if (requestLineParts.Length < 1)
                return null;

            var contentLength = 0;
            for (var i = 1; i < lines.Length; i++)
            {
                var separator = lines[i].IndexOf(':');
                if (separator <= 0)
                    continue;

                var name = lines[i].Substring(0, separator).Trim();
                if (string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(lines[i].Substring(separator + 1).Trim(), out contentLength);
            }

            var bodyStart = headerEnd + 4;
            var bodyBytes = new byte[contentLength];
            var copied = Math.Min(contentLength, requestBytes.Length - bodyStart);
            if (copied > 0)
                Buffer.BlockCopy(requestBytes, bodyStart, bodyBytes, 0, copied);

            while (copied < contentLength)
            {
                var read = await stream.ReadAsync(bodyBytes, copied, contentLength - copied, ct);
                if (read == 0)
                    break;
                copied += read;
            }

            return new HttpRequestData
            {
                Method = requestLineParts[0],
                Body = Encoding.UTF8.GetString(bodyBytes, 0, copied)
            };
        }

        private static McpRequest ParseJsonRequest(string json)
        {
            try
            {
                var obj = JObject.Parse(json);
                return new McpRequest
                {
                    JsonRpc = (string)obj["jsonrpc"] ?? "2.0",
                    Id = obj["id"],
                    Method = (string)obj["method"],
                    Params = obj["params"] as JObject ?? new JObject()
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Neo MCP Server] JSON parse error: {ex.Message}");
                return null;
            }
        }

        private static int FindHeaderEnd(byte[] buffer, int length)
        {
            for (var i = 3; i < length; i++)
            {
                if (buffer[i - 3] == '\r' &&
                    buffer[i - 2] == '\n' &&
                    buffer[i - 1] == '\r' &&
                    buffer[i] == '\n')
                {
                    return i - 3;
                }
            }

            return -1;
        }

        private static Task SendOptionsResponseAsync(NetworkStream stream, CancellationToken ct)
        {
            return SendRawResponseAsync(stream, (int)HttpStatusCode.NoContent, "No Content", "text/plain", string.Empty, ct);
        }

        private static Task SendMethodNotAllowedAsync(NetworkStream stream, string allowHeader, CancellationToken ct)
        {
            return SendRawResponseAsync(stream, (int)HttpStatusCode.MethodNotAllowed, "Method Not Allowed", "text/plain", string.Empty, ct, "Allow: " + allowHeader + "\r\n");
        }

        private static Task SendAcceptedAsync(NetworkStream stream, CancellationToken ct)
        {
            return SendRawResponseAsync(stream, (int)HttpStatusCode.Accepted, "Accepted", "text/plain", string.Empty, ct);
        }

        private static Task SendResponseAsync(NetworkStream stream, McpResponse response, CancellationToken ct)
        {
            var json = SerializeResponse(response);
            return SendRawResponseAsync(stream, (int)HttpStatusCode.OK, "OK", "application/json; charset=utf-8", json, ct);
        }

        private static async Task SendRawResponseAsync(
            NetworkStream stream,
            int statusCode,
            string reasonPhrase,
            string contentType,
            string body,
            CancellationToken ct,
            string extraHeaders = "")
        {
            var bodyBytes = Encoding.UTF8.GetBytes(body ?? string.Empty);
            var header =
                $"HTTP/1.1 {statusCode} {reasonPhrase}\r\n" +
                $"Content-Type: {contentType}\r\n" +
                $"Content-Length: {bodyBytes.Length}\r\n" +
                "Connection: close\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                "Access-Control-Allow-Methods: POST, OPTIONS\r\n" +
                "Access-Control-Allow-Headers: Content-Type\r\n" +
                extraHeaders +
                "\r\n";

            var headerBytes = Encoding.ASCII.GetBytes(header);
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length, ct);
            if (bodyBytes.Length > 0)
                await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length, ct);
        }

        private static McpResponse CreateErrorResponse(JToken requestId, int code, string message)
        {
            return new McpResponse
            {
                Id = requestId,
                Error = new McpError { Code = code, Message = message }
            };
        }

        private static string SerializeResponse(McpResponse response)
        {
            var obj = new JObject
            {
                ["jsonrpc"] = response.JsonRpc ?? "2.0"
            };

            obj["id"] = response.Id ?? JValue.CreateNull();
            if (response.Error != null)
            {
                var error = new JObject
                {
                    ["code"] = response.Error.Code,
                    ["message"] = response.Error.Message
                };
                if (response.Error.Data != null)
                    error["data"] = JToken.FromObject(response.Error.Data);
                obj["error"] = error;
            }
            else
            {
                obj["result"] = response.Result == null ? JValue.CreateNull() : JToken.FromObject(response.Result);
            }

            return JsonConvert.SerializeObject(obj, Formatting.None);
        }

        private void CleanupFailedStart()
        {
            _isRunning = false;
            CloseListener();
            _listener = null;
        }

        private void CloseListener()
        {
            try
            {
                _listener?.Stop();
            }
            catch
            {
            }
        }

        private static async Task<bool> DelayBeforeRetryAsync(CancellationToken ct)
        {
            try
            {
                await Task.Delay(StartRetryDelayMs, ct).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private static bool IsAddressInUse(Exception ex)
        {
            var message = ex?.Message ?? string.Empty;
            if (message.IndexOf("Only one usage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("Address already in use", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return ex is SocketException socketException &&
                   (socketException.ErrorCode == 48 ||
                    socketException.ErrorCode == 98 ||
                    socketException.ErrorCode == 183 ||
                    socketException.ErrorCode == 10048);
        }

        private sealed class HttpRequestData
        {
            public string Method { get; set; }
            public string Body { get; set; }
        }
    }
}
