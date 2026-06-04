// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using Neo.UnityMcp.Services;
using Neo.UnityMcp.Threading;

namespace Neo.UnityMcp.DI
{
    internal sealed class RootScopeServices : IDisposable
    {
        private static RootScopeServices _instance;

        public static RootScopeServices Instance => _instance ?? (_instance = new RootScopeServices());

        public McpServerSettings Settings { get; }
        public IEditorThreadHelper EditorThreadHelper { get; }
        public NeoMcpServerService Server { get; }

        private RootScopeServices()
        {
            _instance = this;
            Settings = new McpServerSettings();
            EditorThreadHelper = new EditorThreadHelper();
            Server = new NeoMcpServerService(Settings, EditorThreadHelper);
        }

        public object GetService(Type type)
        {
            if (type == typeof(McpServerSettings))
                return Settings;
            if (type == typeof(IEditorThreadHelper))
                return EditorThreadHelper;
            if (type == typeof(NeoMcpServerService))
                return Server;

            return null;
        }

        public void Dispose()
        {
            Server.Dispose();
            EditorThreadHelper.Dispose();
        }
    }
}
