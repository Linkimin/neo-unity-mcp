// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using Neo.UnityMcp.DI;
using UnityEditor;
using UnityEngine;

namespace Neo.UnityMcp.Jobs
{
    // On domain load: rehydrate the job store from disk (so jobs survive reloads) and drop
    // terminal jobs older than the TTL.
    [InitializeOnLoad]
    internal static class JobsBootstrap
    {
        private static readonly TimeSpan TerminalTtl = TimeSpan.FromHours(2);

        static JobsBootstrap()
        {
            EditorApplication.delayCall += Initialize;
        }

        private static void Initialize()
        {
            if (Application.isBatchMode)
                return;

            try
            {
                var store = RootScopeServices.Instance.JobStore;
                store.RehydrateFromDisk();
                store.Cleanup(TerminalTtl);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Neo MCP Server] Jobs bootstrap failed: " + ex.Message);
            }
        }
    }
}
