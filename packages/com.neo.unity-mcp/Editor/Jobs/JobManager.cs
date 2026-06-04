// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Neo.UnityMcp.Jobs
{
    // Owns job lifecycle on the editor (main) thread. StartJob persists a Running job, then
    // runs the work delegate; the work either completes synchronously (ctx.Complete) or wires
    // async completion (e.g. run_tests via TestRunnerApi callbacks) which calls back later.
    // Every transition flushes through the store, so state survives domain reloads.
    internal sealed class JobManager
    {
        private const int MaxLogLines = 200;

        private readonly JobStore _store;
        private readonly Dictionary<string, CancellationTokenSource> _cts =
            new Dictionary<string, CancellationTokenSource>(StringComparer.Ordinal);

        public JobManager(JobStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public JobState Get(string id) => _store.Load(id);

        public IEnumerable<JobState> List() => _store.List();

        public string StartJob(string tool, Action<IJobContext> work)
        {
            var now = UtcNow();
            var job = new JobState
            {
                id = Guid.NewGuid().ToString("N"),
                tool = tool,
                status = JobStatus.Running,
                createdAtUtc = now,
                updatedAtUtc = now
            };
            _store.Save(job);

            var cts = new CancellationTokenSource();
            _cts[job.id] = cts;
            var ctx = new JobContext(this, job.id, cts.Token);

            if (work != null)
            {
                try
                {
                    work(ctx);
                }
                catch (OperationCanceledException)
                {
                    Cancel(job.id);
                }
                catch (Exception ex)
                {
                    Fail(job.id, ex.Message);
                }
            }

            return job.id;
        }

        public void Complete(string id, string result) => SetTerminal(id, JobStatus.Succeeded, result, null);

        public void Fail(string id, string error) => SetTerminal(id, JobStatus.Failed, null, error);

        public void Cancel(string id)
        {
            if (_cts.TryGetValue(id, out var cts))
            {
                try { cts.Cancel(); }
                catch { }
            }

            SetTerminal(id, JobStatus.Canceled, null, null);
        }

        internal void Report(string id, float progress)
        {
            var job = _store.Load(id);
            if (job == null)
                return;

            job.progress = Mathf.Clamp01(progress);
            job.updatedAtUtc = UtcNow();
            _store.Save(job);
        }

        internal void Log(string id, string line)
        {
            var job = _store.Load(id);
            if (job == null)
                return;

            job.logs ??= new List<string>();
            job.logs.Add(line ?? string.Empty);
            if (job.logs.Count > MaxLogLines)
                job.logs.RemoveRange(0, job.logs.Count - MaxLogLines);
            job.updatedAtUtc = UtcNow();
            _store.Save(job);
        }

        private void SetTerminal(string id, JobStatus status, string result, string error)
        {
            var job = _store.Load(id);
            if (job != null)
            {
                job.status = status;
                if (result != null) job.result = result;
                if (error != null) job.error = error;
                job.updatedAtUtc = UtcNow();
                _store.Save(job);
            }

            if (_cts.TryGetValue(id, out var cts))
            {
                cts.Dispose();
                _cts.Remove(id);
            }
        }

        private static string UtcNow() => DateTime.UtcNow.ToString("o");

        private sealed class JobContext : IJobContext
        {
            private readonly JobManager _manager;

            public string JobId { get; }
            public CancellationToken Cancellation { get; }

            public JobContext(JobManager manager, string jobId, CancellationToken cancellation)
            {
                _manager = manager;
                JobId = jobId;
                Cancellation = cancellation;
            }

            public void Report(float progress) => _manager.Report(JobId, progress);
            public void Log(string line) => _manager.Log(JobId, line);
            public void Complete(string result) => _manager.Complete(JobId, result);
            public void Fail(string error) => _manager.Fail(JobId, error);
        }
    }
}
