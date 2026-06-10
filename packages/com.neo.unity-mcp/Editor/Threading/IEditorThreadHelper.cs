// Neo Unity MCP.

using System;
using System.Threading.Tasks;

namespace Neo.UnityMcp.Threading
{
    // Marshals work onto the Unity editor (main) thread. Calls from the main thread run
    // synchronously; calls from background threads are queued and drained on EditorApplication.update.
    internal interface IEditorThreadHelper : IDisposable
    {
        bool IsMainThread { get; }
        Task ExecuteOnEditorThreadAsync(Action action);
        Task<T> ExecuteOnEditorThreadAsync<T>(Func<T> func);
        Task<T> ExecuteAsyncOnEditorThreadAsync<T>(Func<Task<T>> asyncFunc);
    }
}
