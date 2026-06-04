using System;
using System.IO;
using System.Linq;
using Neo.UnityMcp.Jobs;
using NUnit.Framework;

namespace Neo.UnityMcp.Tests.Jobs
{
    public sealed class JobStoreTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "neo-jobs-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
            catch { }
        }

        [Test]
        public void Save_ThenLoad_RoundTripsThroughDisk()
        {
            var store = new JobStore(_dir);
            var job = new JobState
            {
                id = "j1",
                tool = "run_tests",
                status = JobStatus.Succeeded,
                progress = 0.5f,
                result = "ok",
                createdAtUtc = Now(),
                updatedAtUtc = Now()
            };
            job.logs.Add("line1");
            job.logs.Add("line2");
            store.Save(job);

            // Fresh store with no in-memory cache forces a disk read.
            var loaded = new JobStore(_dir).Load("j1");

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.status, Is.EqualTo(JobStatus.Succeeded));
            Assert.That(loaded.progress, Is.EqualTo(0.5f));
            Assert.That(loaded.result, Is.EqualTo("ok"));
            Assert.That(loaded.logs, Is.EquivalentTo(new[] { "line1", "line2" }));
        }

        [Test]
        public void List_ReturnsAllSavedJobs()
        {
            var store = new JobStore(_dir);
            store.Save(new JobState { id = "a", status = JobStatus.Running, createdAtUtc = Now(), updatedAtUtc = Now() });
            store.Save(new JobState { id = "b", status = JobStatus.Running, createdAtUtc = Now(), updatedAtUtc = Now() });

            var ids = new JobStore(_dir).List().Select(j => j.id);

            Assert.That(ids, Is.EquivalentTo(new[] { "a", "b" }));
        }

        [Test]
        public void Cleanup_RemovesTerminalJobsOlderThanTtl_KeepsRunningAndRecent()
        {
            var store = new JobStore(_dir);
            var old = DateTime.UtcNow.AddHours(-3).ToString("o");
            store.Save(new JobState { id = "old_done", status = JobStatus.Succeeded, createdAtUtc = old, updatedAtUtc = old });
            store.Save(new JobState { id = "old_running", status = JobStatus.Running, createdAtUtc = old, updatedAtUtc = old });
            store.Save(new JobState { id = "fresh_done", status = JobStatus.Succeeded, createdAtUtc = Now(), updatedAtUtc = Now() });

            store.Cleanup(TimeSpan.FromHours(2));

            Assert.That(store.Load("old_done"), Is.Null, "terminal + stale -> removed");
            Assert.That(store.Load("old_running"), Is.Not.Null, "non-terminal -> kept");
            Assert.That(store.Load("fresh_done"), Is.Not.Null, "terminal but recent -> kept");
        }

        private static string Now() => DateTime.UtcNow.ToString("o");
    }
}
