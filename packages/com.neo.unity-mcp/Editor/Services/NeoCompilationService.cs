// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Neo.UnityMcp.Services
{
    // Captures compiler messages across a compile cycle (and the domain reload that follows) via
    // SessionState, exposes IsCompiling, a formatted error report, and a non-blocking wait.
    [InitializeOnLoad]
    internal static class NeoCompilationService
    {
        private const string MessagesKey = "Neo.Compilation.Messages";

        [Serializable]
        private sealed class Message
        {
            public string type;
            public string message;
            public string file;
            public int line;
        }

        [Serializable]
        private sealed class MessageBox
        {
            public List<Message> items = new List<Message>();
        }

        static NeoCompilationService()
        {
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
        }

        public static bool IsCompiling => EditorApplication.isCompiling;

        public static string GetCompilationErrors(int maxEntries = 50, bool includeWarnings = false)
        {
            var box = Load();
            var filtered = box.items
                .Where(m => m.type == "Error" || (includeWarnings && m.type == "Warning"))
                .ToList();

            if (filtered.Count == 0)
                return includeWarnings ? "No compilation errors or warnings detected." : "No compilation errors detected.";

            var sb = new StringBuilder();
            sb.AppendLine(filtered.Count + " issue(s):");
            foreach (var m in filtered.Take(Mathf.Clamp(maxEntries, 1, 500)))
                sb.AppendLine("[" + m.type + "] " + m.file + "(" + m.line + "): " + m.message);
            return sb.ToString().TrimEnd();
        }

        public static Task<bool> WaitForCompilationAsync(bool forceRefresh, int timeoutSeconds)
        {
            if (forceRefresh)
                AssetDatabase.Refresh();

            if (!EditorApplication.isCompiling)
                return Task.FromResult(true);

            var tcs = new TaskCompletionSource<bool>();
            var deadline = DateTime.UtcNow.AddSeconds(Mathf.Clamp(timeoutSeconds, 1, 300));

            void Tick()
            {
                if (!EditorApplication.isCompiling)
                {
                    EditorApplication.update -= Tick;
                    tcs.TrySetResult(true);
                }
                else if (DateTime.UtcNow > deadline)
                {
                    EditorApplication.update -= Tick;
                    tcs.TrySetResult(false);
                }
            }

            EditorApplication.update += Tick;
            return tcs.Task;
        }

        private static void OnCompilationStarted(object _)
        {
            SessionState.EraseString(MessagesKey);
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            if (messages == null || messages.Length == 0)
                return;

            var box = Load();
            foreach (var m in messages)
            {
                if (m.type == CompilerMessageType.Error || m.type == CompilerMessageType.Warning)
                {
                    box.items.Add(new Message
                    {
                        type = m.type.ToString(),
                        message = m.message,
                        file = m.file,
                        line = m.line
                    });
                }
            }

            SessionState.SetString(MessagesKey, JsonConvert.SerializeObject(box));
        }

        private static MessageBox Load()
        {
            var raw = SessionState.GetString(MessagesKey, null);
            if (string.IsNullOrEmpty(raw))
                return new MessageBox();
            try
            {
                return JsonConvert.DeserializeObject<MessageBox>(raw) ?? new MessageBox();
            }
            catch
            {
                return new MessageBox();
            }
        }
    }
}
