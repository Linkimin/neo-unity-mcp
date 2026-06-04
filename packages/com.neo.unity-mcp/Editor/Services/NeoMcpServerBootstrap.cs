// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using Neo.UnityMcp.DI;
using UnityEditor;
using UnityEngine;

namespace Neo.UnityMcp.Services
{
    [InitializeOnLoad]
    internal static class NeoMcpServerBootstrap
    {
        private const string WasRunningBeforeReloadKey = "Neo.UnityMcp.Server.WasRunningBeforeReload";

        static NeoMcpServerBootstrap()
        {
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += AfterAssemblyReload;
            EditorApplication.quitting += Stop;
            EditorApplication.delayCall += AutoStartIfEnabled;
        }

        private static void AutoStartIfEnabled()
        {
            if (Application.isBatchMode)
                return;

            var root = RootScopeServices.Instance;
            if (root.Settings.Enabled)
                _ = root.Server.StartAsync();
        }

        private static void BeforeAssemblyReload()
        {
            var root = RootScopeServices.Instance;
            SessionState.SetBool(WasRunningBeforeReloadKey, root.Server.IsRunning);
            root.Server.StopSync();
        }

        private static void AfterAssemblyReload()
        {
            if (!SessionState.GetBool(WasRunningBeforeReloadKey, false))
                return;

            SessionState.EraseBool(WasRunningBeforeReloadKey);
            EditorApplication.delayCall += AutoStartIfEnabled;
        }

        private static void Stop()
        {
            RootScopeServices.Instance.Server.StopSync();
        }
    }
}
