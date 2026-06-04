// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Threading.Tasks;

namespace Neo.UnityMcp.Threading
{
    internal interface IEditorThreadHelper : IDisposable
    {
        bool IsMainThread { get; }
        Task ExecuteOnEditorThreadAsync(Action action);
        Task<T> ExecuteOnEditorThreadAsync<T>(Func<T> func);
        Task<T> ExecuteAsyncOnEditorThreadAsync<T>(Func<Task<T>> asyncFunc);
    }
}
