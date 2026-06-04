// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Threading;
using System.Threading.Tasks;
using Neo.UnityMcp.Protocol;
using Neo.UnityMcp.Threading;
using Neo.UnityMcp.Transport;
using UnityEngine;

namespace Neo.UnityMcp.Services
{
    internal sealed class NeoMcpServerService : IDisposable
    {
        private readonly McpServerSettings _settings;
        private readonly IEditorThreadHelper _threadHelper;
        private readonly object _lifecycleLock = new object();

        private IMcpTransport _transport;
        private McpRequestHandler _requestHandler;
        private Task<bool> _startTask;
        private CancellationTokenSource _startCts;
        private int _lifecycleVersion;
        private bool _isRunning;
        private bool _disposed;
        private bool _restartScheduled;
        private bool _restartInProgress;

        public bool IsRunning
        {
            get
            {
                lock (_lifecycleLock)
                    return _isRunning;
            }
        }

        public int Port { get; private set; }

        public NeoMcpServerService(McpServerSettings settings, IEditorThreadHelper threadHelper)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _threadHelper = threadHelper ?? throw new ArgumentNullException(nameof(threadHelper));
            Port = _settings.Port;
            _settings.OnSettingsChanged += HandleSettingsChanged;
        }

        public Task<bool> StartAsync(CancellationToken ct = default)
        {
            if (Application.isBatchMode)
            {
                Debug.LogWarning("[Neo MCP Server] Skipping server start in Unity batch mode.");
                return Task.FromResult(false);
            }

            if (!_settings.Enabled)
            {
                Debug.Log("[Neo MCP Server] Server is disabled in settings.");
                return Task.FromResult(false);
            }

            lock (_lifecycleLock)
            {
                if (_disposed)
                    return Task.FromResult(false);

                if (_isRunning && _transport?.IsRunning == true)
                    return Task.FromResult(true);

                if (_startTask != null)
                    return _startTask;

                _lifecycleVersion++;
                _startCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var startCts = _startCts;
                var startTask = StartCoreAsync(_lifecycleVersion, startCts);
                _startTask = startTask;
                _ = startTask.ContinueWith(
                    _ => ClearCompletedStartTask(startTask, startCts),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                return startTask;
            }
        }

        public Task StopAsync()
        {
            StopSync();
            return Task.CompletedTask;
        }

        public void StopSync()
        {
            CancellationTokenSource startCtsToCancel;
            lock (_lifecycleLock)
            {
                _lifecycleVersion++;
                startCtsToCancel = _startCts;
                _startCts = null;
                _startTask = null;
            }

            startCtsToCancel?.Cancel();
            CleanupServerState();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _settings.OnSettingsChanged -= HandleSettingsChanged;
            StopSync();
        }

        private async Task<bool> StartCoreAsync(int lifecycleVersion, CancellationTokenSource startCts)
        {
            IMcpTransport transport = null;
            try
            {
                var startupPort = McpServerSettings.NormalizePort(_settings.Port);
                var serverName = "Neo Unity MCP Server - " + Application.productName;
                var projectIdentity = NeoProjectIdentity.FromProjectPath(Application.dataPath);

                transport = new HttpMcpTransport(startupPort);
                var requestHandler = new McpRequestHandler(serverName, PackageVersionUtility.CurrentVersion, projectIdentity);
                transport.OnRequestReceived += HandleRequestReceived;

                var disposeBeforeAssign = false;
                lock (_lifecycleLock)
                {
                    if (_disposed || lifecycleVersion != _lifecycleVersion)
                    {
                        disposeBeforeAssign = true;
                    }
                    else
                    {
                        Port = startupPort;
                        _transport = transport;
                        _requestHandler = requestHandler;
                    }
                }

                if (disposeBeforeAssign)
                {
                    DisposeUnassignedTransport(transport);
                    return false;
                }

                var started = await transport.StartAsync(startCts.Token);
                if (!started)
                {
                    CleanupServerState(transport);
                    return false;
                }

                var shouldDisposeStartedTransport = false;
                lock (_lifecycleLock)
                {
                    if (_disposed || lifecycleVersion != _lifecycleVersion || !ReferenceEquals(_transport, transport))
                    {
                        shouldDisposeStartedTransport = true;
                    }
                    else
                    {
                        _isRunning = true;
                    }
                }

                if (shouldDisposeStartedTransport)
                {
                    DisposeUnassignedTransport(transport);
                    return false;
                }

                Debug.Log($"[Neo MCP Server] Started on http://127.0.0.1:{Port}/");
                return true;
            }
            catch (OperationCanceledException)
            {
                CleanupServerState(transport);
                return false;
            }
            catch (Exception ex)
            {
                CleanupServerState(transport);
                Debug.LogError($"[Neo MCP Server] Failed to start: {ex.Message}");
                return false;
            }
        }

        private void ClearCompletedStartTask(Task<bool> completedTask, CancellationTokenSource startCts)
        {
            lock (_lifecycleLock)
            {
                if (ReferenceEquals(_startTask, completedTask))
                    _startTask = null;
                if (ReferenceEquals(_startCts, startCts))
                    _startCts = null;
            }

            startCts.Dispose();
        }

        private bool CleanupServerState(IMcpTransport expectedTransport = null)
        {
            IMcpTransport transportToDispose;
            lock (_lifecycleLock)
            {
                if (expectedTransport != null &&
                    _transport != null &&
                    !ReferenceEquals(_transport, expectedTransport))
                {
                    return false;
                }

                transportToDispose = _transport ?? expectedTransport;
                _transport = null;
                _requestHandler = null;
                _isRunning = false;
            }

            if (transportToDispose != null)
            {
                transportToDispose.OnRequestReceived -= HandleRequestReceived;
                transportToDispose.Stop();
                transportToDispose.Dispose();
            }

            return transportToDispose != null;
        }

        private void DisposeUnassignedTransport(IMcpTransport transport)
        {
            if (transport == null)
                return;

            transport.OnRequestReceived -= HandleRequestReceived;
            transport.Stop();
            transport.Dispose();
        }

        private async void HandleRequestReceived(McpRequest request, Action<McpResponse> sendResponse)
        {
            try
            {
                McpRequestHandler requestHandler;
                lock (_lifecycleLock)
                    requestHandler = _requestHandler;

                if (requestHandler == null)
                {
                    sendResponse(new McpResponse
                    {
                        Id = request?.Id,
                        Error = new McpError { Code = -32000, Message = "MCP server is stopping or not ready." }
                    });
                    return;
                }

                var response = await _threadHelper.ExecuteAsyncOnEditorThreadAsync(
                    () => requestHandler.HandleRequestAsync(request, default));
                sendResponse(response);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Neo MCP Server] Error handling request: {ex.Message}");
                sendResponse(new McpResponse
                {
                    Id = request?.Id,
                    Error = new McpError { Code = -32603, Message = "Internal error" }
                });
            }
        }

        private void HandleSettingsChanged()
        {
            if (_disposed)
                return;

            Port = _settings.Port;
            var startTaskInFlight = HasStartTaskInFlight();
            if (startTaskInFlight)
                SupersedeInFlightStart();

            if (_settings.Enabled)
            {
                if (IsRunning)
                    ScheduleRestart();
                else
                    ScheduleStartWithCurrentSettings();
            }
            else
            {
                StopSync();
            }
        }

        private bool HasStartTaskInFlight()
        {
            lock (_lifecycleLock)
                return _startTask != null;
        }

        private void SupersedeInFlightStart()
        {
            CancellationTokenSource startCtsToCancel;
            lock (_lifecycleLock)
            {
                if (_startTask == null)
                    return;

                _lifecycleVersion++;
                startCtsToCancel = _startCts;
                _startCts = null;
                _startTask = null;
            }

            startCtsToCancel?.Cancel();
            CleanupServerState();
        }

        private void ScheduleStartWithCurrentSettings()
        {
            if (_restartScheduled)
                return;

            _restartScheduled = true;
            UnityEditor.EditorApplication.delayCall += StartWithCurrentSettings;
        }

        private async void StartWithCurrentSettings()
        {
            _restartScheduled = false;
            if (_disposed || !_settings.Enabled)
                return;

            if (_restartInProgress)
            {
                ScheduleStartWithCurrentSettings();
                return;
            }

            _restartInProgress = true;
            try
            {
                await StartAsync();
            }
            finally
            {
                _restartInProgress = false;
            }
        }

        private void ScheduleRestart()
        {
            if (_restartScheduled)
                return;

            _restartScheduled = true;
            UnityEditor.EditorApplication.delayCall += RestartTransportAfterSettingsChange;
        }

        private async void RestartTransportAfterSettingsChange()
        {
            _restartScheduled = false;
            if (_disposed)
                return;

            if (_restartInProgress)
            {
                ScheduleRestart();
                return;
            }

            _restartInProgress = true;
            try
            {
                StopSync();
                await StartAsync();
            }
            finally
            {
                _restartInProgress = false;
            }
        }
    }
}
