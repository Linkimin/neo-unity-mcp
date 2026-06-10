// Neo Unity MCP.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Neo.UnityMcp.Threading
{
    // Independent editor main-thread marshaller. A single queue of self-contained work items is
    // drained on EditorApplication.update (capped per frame). Main-thread callers run synchronously;
    // background callers get a Task that completes when the item is pumped. Each queued item is
    // disposal-aware, so pending work is canceled (not run) on Dispose.
    internal sealed class EditorThreadHelper : IEditorThreadHelper
    {
        private const int MaxItemsPerFrame = 16;

        private readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();
        private readonly int _mainThreadId;
        private volatile bool _disposed;

        public EditorThreadHelper()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            EditorApplication.update += ProcessQueue;
        }

        public bool IsMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        public Task ExecuteOnEditorThreadAsync(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            return ExecuteOnEditorThreadAsync(() =>
            {
                action();
                return (object)null;
            });
        }

        public Task<T> ExecuteOnEditorThreadAsync<T>(Func<T> func)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));
            if (_disposed)
                return Canceled<T>();

            if (IsMainThread)
            {
                try { return Task.FromResult(func()); }
                catch (Exception ex) { return Task.FromException<T>(ex); }
            }

            var tcs = new TaskCompletionSource<T>();
            _queue.Enqueue(() =>
            {
                if (_disposed) { tcs.TrySetCanceled(); return; }
                try { tcs.TrySetResult(func()); }
                catch (Exception ex) { tcs.TrySetException(ex); }
            });
            return tcs.Task;
        }

        public Task<T> ExecuteAsyncOnEditorThreadAsync<T>(Func<Task<T>> asyncFunc)
        {
            if (asyncFunc == null)
                throw new ArgumentNullException(nameof(asyncFunc));
            if (_disposed)
                return Canceled<T>();

            if (IsMainThread)
                return asyncFunc();

            var tcs = new TaskCompletionSource<T>();
            _queue.Enqueue(() =>
            {
                if (_disposed) { tcs.TrySetCanceled(); return; }
                try
                {
                    asyncFunc().ContinueWith(
                        t =>
                        {
                            if (t.IsFaulted) tcs.TrySetException(t.Exception.InnerException ?? (Exception)t.Exception);
                            else if (t.IsCanceled) tcs.TrySetCanceled();
                            else tcs.TrySetResult(t.Result);
                        },
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
            return tcs.Task;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            EditorApplication.update -= ProcessQueue;

            // Drain: each item sees _disposed and cancels its own TCS instead of running.
            while (_queue.TryDequeue(out var work))
            {
                try { work(); }
                catch { }
            }
        }

        // Drains queued work on the main thread. Subscribed to EditorApplication.update; also the
        // deterministic test entry point.
        internal void ProcessQueue()
        {
            if (_disposed)
                return;

            var processed = 0;
            while (processed++ < MaxItemsPerFrame && _queue.TryDequeue(out var work))
            {
                try
                {
                    work();
                }
                catch (Exception ex)
                {
                    Debug.LogError("[Neo MCP Server] Editor-thread work failed: " + ex.Message);
                }
            }
        }

        private static Task<T> Canceled<T>()
        {
            var tcs = new TaskCompletionSource<T>();
            tcs.SetCanceled();
            return tcs.Task;
        }
    }
}
