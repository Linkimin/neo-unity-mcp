// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Neo.UnityMcp.Services
{
    // Captures Unity console output in-process (ring buffer) so get_console_logs can return it
    // without scraping the editor's LogEntries internal API. Survives via [InitializeOnLoad]
    // re-subscription on each domain load.
    [InitializeOnLoad]
    internal static class ConsoleLogService
    {
        public sealed class Entry
        {
            public string type;
            public string message;
            public string stackTrace;
            public string timeUtc;
        }

        private const int Capacity = 1000;
        private static readonly Queue<Entry> _entries = new Queue<Entry>();
        private static readonly object _lock = new object();

        static ConsoleLogService()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            Application.logMessageReceivedThreaded += OnLog;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            lock (_lock)
            {
                _entries.Enqueue(new Entry
                {
                    type = type.ToString(),
                    message = condition,
                    stackTrace = stackTrace,
                    timeUtc = DateTime.UtcNow.ToString("o")
                });
                while (_entries.Count > Capacity)
                    _entries.Dequeue();
            }
        }

        public static List<Entry> GetRecent(int count, LogType? filter)
        {
            lock (_lock)
            {
                IEnumerable<Entry> query = _entries;
                if (filter.HasValue)
                {
                    var wanted = filter.Value.ToString();
                    query = query.Where(e => e.type == wanted);
                }

                var list = query.ToList();
                var n = Mathf.Clamp(count, 1, Capacity);
                var skip = Math.Max(0, list.Count - n);
                return list.GetRange(skip, list.Count - skip);
            }
        }

        public static void Clear()
        {
            lock (_lock)
                _entries.Clear();
        }
    }
}
