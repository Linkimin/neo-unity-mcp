// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System.Collections.Generic;
using UnityEditor;
using UnityObject = UnityEngine.Object;

namespace Neo.UnityMcp.Execution
{
    // Injected into INeoCommand.Execute by execute_code. Use Register/Destroy instead of
    // touching Undo directly — snippets then participate in editor Undo and the host returns
    // a structured changelog (created/modified/destroyed instance ids) to the agent.
    // Logs go to the response only (not the Unity console unless the snippet calls Debug.Log).
    public sealed class NeoExecutionContext
    {
        public sealed class LogEntry
        {
            public string Level; // "info" / "warning" / "error"
            public string Message;
        }

        private readonly List<LogEntry> _logs = new List<LogEntry>();
        private readonly List<int> _created = new List<int>();
        private readonly List<int> _modified = new List<int>();
        private readonly List<int> _destroyed = new List<int>();

        public IReadOnlyList<LogEntry> Logs => _logs;
        public IReadOnlyList<int> CreatedInstanceIds => _created;
        public IReadOnlyList<int> ModifiedInstanceIds => _modified;
        public IReadOnlyList<int> DestroyedInstanceIds => _destroyed;

        // Object the snippet returns explicitly (optional). Serialized into the response.
        public object ReturnValue { get; set; }

        // ----- Undo + tracking -----

        public void RegisterObjectCreation(UnityObject obj)
        {
            if (obj == null) return;
            Undo.RegisterCreatedObjectUndo(obj, "execute_code: create");
            _created.Add(obj.GetInstanceID());
        }

        public void RegisterObjectModification(UnityObject obj)
        {
            if (obj == null) return;
            Undo.RecordObject(obj, "execute_code: modify");
            _modified.Add(obj.GetInstanceID());
        }

        public void DestroyObject(UnityObject obj)
        {
            if (obj == null) return;
            _destroyed.Add(obj.GetInstanceID());
            Undo.DestroyObjectImmediate(obj);
        }

        // ----- Logging -----

        public void Log(string format, params object[] args)
            => _logs.Add(new LogEntry { Level = "info", Message = Format(format, args) });

        public void LogWarning(string format, params object[] args)
            => _logs.Add(new LogEntry { Level = "warning", Message = Format(format, args) });

        public void LogError(string format, params object[] args)
            => _logs.Add(new LogEntry { Level = "error", Message = Format(format, args) });

        private static string Format(string format, object[] args)
        {
            if (args == null || args.Length == 0) return format ?? string.Empty;
            try { return string.Format(format ?? string.Empty, args); }
            catch { return (format ?? string.Empty) + " " + string.Join(", ", args); }
        }
    }
}
