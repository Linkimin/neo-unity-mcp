// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace Neo.UnityMcp.Threading
{
    internal sealed class EditorThreadHelper : IEditorThreadHelper
    {
        private readonly ConcurrentQueue<(Action action, TaskCompletionSource<bool> tcs)> _actionQueue
            = new ConcurrentQueue<(Action, TaskCompletionSource<bool>)>();

        private readonly ConcurrentQueue<(Func<object> func, TaskCompletionSource<object> tcs)> _funcQueue
            = new ConcurrentQueue<(Func<object>, TaskCompletionSource<object>)>();

        private readonly int _mainThreadId;
        private bool _disposed;

        public bool IsMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        public EditorThreadHelper()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            EditorApplication.update += ProcessQueues;
        }

        public Task ExecuteOnEditorThreadAsync(Action action)
        {
            if (_disposed)
                return CreateCanceledTask();

            if (IsMainThread)
            {
                try
                {
                    action();
                    return Task.CompletedTask;
                }
                catch (Exception ex)
                {
                    return Task.FromException(ex);
                }
            }

            var tcs = new TaskCompletionSource<bool>();
            _actionQueue.Enqueue((action, tcs));
            return tcs.Task;
        }

        public Task<T> ExecuteOnEditorThreadAsync<T>(Func<T> func)
        {
            if (_disposed)
                return CreateCanceledTask<T>();

            if (IsMainThread)
            {
                try
                {
                    return Task.FromResult(func());
                }
                catch (Exception ex)
                {
                    return Task.FromException<T>(ex);
                }
            }

            var outerTcs = new TaskCompletionSource<T>();
            var tcs = new TaskCompletionSource<object>();
            tcs.Task.ContinueWith(
                task =>
                {
                    if (task.IsCanceled)
                        outerTcs.TrySetCanceled();
                    else if (task.IsFaulted)
                        outerTcs.TrySetException(task.Exception?.InnerException ?? task.Exception ?? new Exception("Unknown error"));
                    else
                        outerTcs.TrySetResult((T)task.Result);
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            _funcQueue.Enqueue((() => func(), tcs));
            return outerTcs.Task;
        }

        public Task<T> ExecuteAsyncOnEditorThreadAsync<T>(Func<Task<T>> asyncFunc)
        {
            if (_disposed)
                return CreateCanceledTask<T>();

            if (IsMainThread)
                return asyncFunc();

            var outerTcs = new TaskCompletionSource<T>();
            var tcs = new TaskCompletionSource<object>();
            tcs.Task.ContinueWith(
                task =>
                {
                    if (task.IsCanceled)
                        outerTcs.TrySetCanceled();
                    else if (task.IsFaulted)
                        outerTcs.TrySetException(task.Exception?.InnerException ?? task.Exception ?? new Exception("Unknown error"));
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            _funcQueue.Enqueue((() =>
            {
                asyncFunc().ContinueWith(task =>
                {
                    if (task.IsFaulted)
                        outerTcs.TrySetException(task.Exception?.InnerException ?? task.Exception ?? new Exception("Unknown error"));
                    else if (task.IsCanceled)
                        outerTcs.TrySetCanceled();
                    else
                        outerTcs.TrySetResult(task.Result);
                });
                return null;
            }, tcs));

            return outerTcs.Task;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            EditorApplication.update -= ProcessQueues;

            while (_actionQueue.TryDequeue(out var action))
                action.tcs.TrySetCanceled();
            while (_funcQueue.TryDequeue(out var func))
                func.tcs.TrySetCanceled();
        }

        private void ProcessQueues()
        {
            if (_disposed)
                return;

            var processedCount = 0;
            const int maxPerFrame = 10;

            while (processedCount < maxPerFrame && _actionQueue.TryDequeue(out var action))
            {
                try
                {
                    action.action();
                    action.tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    action.tcs.TrySetException(ex);
                }

                processedCount++;
            }

            while (processedCount < maxPerFrame && _funcQueue.TryDequeue(out var func))
            {
                try
                {
                    func.tcs.TrySetResult(func.func());
                }
                catch (Exception ex)
                {
                    func.tcs.TrySetException(ex);
                }

                processedCount++;
            }
        }

        private static Task CreateCanceledTask()
        {
            var tcs = new TaskCompletionSource<bool>();
            tcs.SetCanceled();
            return tcs.Task;
        }

        private static Task<T> CreateCanceledTask<T>()
        {
            var tcs = new TaskCompletionSource<T>();
            tcs.SetCanceled();
            return tcs.Task;
        }
    }
}
