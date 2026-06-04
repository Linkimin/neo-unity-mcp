// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Neo.UnityMcp.Jobs
{
    // File-backed job store (decision D): Library/NeoMcp/jobs/<id>.json + in-memory cache.
    // Surviving domain reloads is the whole point — every transition is flushed to disk.
    // The directory is injectable so tests can use a temp dir.
    internal sealed class JobStore
    {
        private readonly string _dir;
        private readonly Dictionary<string, JobState> _mem = new Dictionary<string, JobState>(StringComparer.Ordinal);

        public JobStore(string directory = null)
        {
            _dir = string.IsNullOrEmpty(directory)
                ? Path.Combine("Library", "NeoMcp", "jobs")
                : directory;
        }

        public void Save(JobState job)
        {
            if (job == null || string.IsNullOrEmpty(job.id))
                return;

            _mem[job.id] = job;
            try
            {
                Directory.CreateDirectory(_dir);
                File.WriteAllText(PathFor(job.id), JsonConvert.SerializeObject(job));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Neo MCP Server] Job save failed: " + ex.Message);
            }
        }

        public JobState Load(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            if (_mem.TryGetValue(id, out var cached))
                return cached;

            var path = PathFor(id);
            if (!File.Exists(path))
                return null;

            try
            {
                var loaded = JsonConvert.DeserializeObject<JobState>(File.ReadAllText(path));
                if (loaded != null)
                    _mem[id] = loaded;
                return loaded;
            }
            catch
            {
                return null;
            }
        }

        public IEnumerable<JobState> List()
        {
            LoadAllFiles();
            return _mem.Values.ToList();
        }

        public void RehydrateFromDisk()
        {
            _mem.Clear();
            LoadAllFiles();
        }

        public void Cleanup(TimeSpan ttl)
        {
            var cutoff = DateTime.UtcNow - ttl;
            foreach (var job in List().ToList())
            {
                if (!IsTerminal(job.status))
                    continue;
                if (!TryParseUtc(job.updatedAtUtc, out var updated) || updated > cutoff)
                    continue;

                _mem.Remove(job.id);
                try
                {
                    var path = PathFor(job.id);
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch
                {
                }
            }
        }

        private void LoadAllFiles()
        {
            if (!Directory.Exists(_dir))
                return;

            foreach (var file in Directory.GetFiles(_dir, "*.json"))
            {
                var id = Path.GetFileNameWithoutExtension(file);
                if (_mem.ContainsKey(id))
                    continue;

                try
                {
                    var job = JsonConvert.DeserializeObject<JobState>(File.ReadAllText(file));
                    if (job != null && !string.IsNullOrEmpty(job.id))
                        _mem[job.id] = job;
                }
                catch
                {
                }
            }
        }

        private string PathFor(string id) => Path.Combine(_dir, id + ".json");

        private static bool IsTerminal(JobStatus status) =>
            status == JobStatus.Succeeded || status == JobStatus.Failed || status == JobStatus.Canceled;

        private static bool TryParseUtc(string iso, out DateTime utc)
        {
            if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                utc = parsed.ToUniversalTime();
                return true;
            }

            utc = default;
            return false;
        }
    }
}
