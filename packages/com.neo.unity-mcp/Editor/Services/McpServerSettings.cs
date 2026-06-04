// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using UnityEditor;

namespace Neo.UnityMcp.Services
{
    internal sealed class McpServerSettings
    {
        private const string EnabledKey = "Neo.UnityMcp.Server.Enabled";
        private const string PortKey = "Neo.UnityMcp.Server.Port";
        public const int DefaultPort = 8765;

        public event Action OnSettingsChanged;

        public bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, true);
            set
            {
                if (Enabled == value)
                    return;

                EditorPrefs.SetBool(EnabledKey, value);
                OnSettingsChanged?.Invoke();
            }
        }

        public int Port
        {
            get => NormalizePort(EditorPrefs.GetInt(PortKey, DefaultPort));
            set
            {
                var normalized = NormalizePort(value);
                if (Port == normalized)
                    return;

                EditorPrefs.SetInt(PortKey, normalized);
                OnSettingsChanged?.Invoke();
            }
        }

        public static int NormalizePort(int port)
        {
            return port > 0 && port <= 65535 ? port : DefaultPort;
        }
    }
}
