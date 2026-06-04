// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using Neo.UnityMcp.DI;
using Neo.UnityMcp.Services;
using UnityEditor;
using UnityEngine;

namespace Neo.UnityMcp
{
    internal sealed class NeoMcpWindow : EditorWindow
    {
        private int _port;

        [MenuItem("Neo/MCP Server")]
        public static void ShowWindow()
        {
            GetWindow<NeoMcpWindow>("Neo MCP Server");
        }

        private void OnEnable()
        {
            _port = RootScopeServices.Instance.Settings.Port;
        }

        private void OnGUI()
        {
            var root = RootScopeServices.Instance;
            var settings = root.Settings;
            var server = root.Server;

            EditorGUILayout.LabelField("Neo MCP Server", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Status", server.IsRunning ? "Running" : "Stopped");

            EditorGUI.BeginChangeCheck();
            var enabled = EditorGUILayout.Toggle("Enabled", settings.Enabled);
            if (EditorGUI.EndChangeCheck())
                settings.Enabled = enabled;

            EditorGUI.BeginChangeCheck();
            _port = EditorGUILayout.IntField("Port", _port);
            if (EditorGUI.EndChangeCheck())
                settings.Port = McpServerSettings.NormalizePort(_port);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Start"))
                    _ = server.StartAsync();

                if (GUILayout.Button("Stop"))
                    server.StopSync();
            }
        }
    }
}
