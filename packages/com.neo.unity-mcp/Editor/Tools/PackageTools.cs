// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System.Linq;
using System.Threading.Tasks;
using Neo.UnityMcp.Registry;
using UnityEditor;
using UnityEditor.PackageManager;

namespace Neo.UnityMcp.Tools
{
    [NeoToolProvider("Packages")]
    internal static class PackageTools
    {
        [NeoTool("list_packages", "List installed packages (offline): name, version, displayName, source.")]
        [ReadOnlyTool]
        public static Task<object> ListPackages()
        {
            var request = Client.List(offlineMode: true, includeIndirectDependencies: false);
            var tcs = new TaskCompletionSource<object>();

            void Tick()
            {
                if (!request.IsCompleted)
                    return;

                EditorApplication.update -= Tick;

                if (request.Status == StatusCode.Success)
                {
                    var packages = request.Result
                        .Select(p => new
                        {
                            name = p.name,
                            version = p.version,
                            displayName = p.displayName,
                            source = p.source.ToString()
                        })
                        .ToList();

                    tcs.TrySetResult(Response.Success(packages.Count + " package(s).", new
                    {
                        count = packages.Count,
                        packages
                    }));
                }
                else
                {
                    tcs.TrySetResult(Response.Error("PACKAGE_LIST_FAILED", new { error = request.Error?.message }));
                }
            }

            EditorApplication.update += Tick;
            return tcs.Task;
        }
    }
}
