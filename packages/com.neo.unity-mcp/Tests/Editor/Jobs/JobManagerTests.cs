using System;
using System.IO;
using System.Threading;
using Neo.UnityMcp.Jobs;
using NUnit.Framework;

namespace Neo.UnityMcp.Tests.Jobs
{
    public sealed class JobManagerTests
    {
        private string _dir;
        private JobManager _manager;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "neo-jobs-" + Guid.NewGuid().ToString("N"));
            _manager = new JobManager(new JobStore(_dir));
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
            catch { }
        }

        [Test]
        public void StartJob_LeavesRunning_WhenWorkDoesNotComplete()
        {
            var id = _manager.StartJob("noop", ctx => { });

            Assert.That(_manager.Get(id).status, Is.EqualTo(JobStatus.Running));
        }

        [Test]
        public void StartJob_ReportLogComplete_TransitionsToSucceeded()
        {
            var id = _manager.StartJob("work", ctx =>
            {
                ctx.Report(0.5f);
                ctx.Log("step1");
                ctx.Complete("done-result");
            });

            var job = _manager.Get(id);
            Assert.That(job.status, Is.EqualTo(JobStatus.Succeeded));
            Assert.That(job.result, Is.EqualTo("done-result"));
            Assert.That(job.progress, Is.EqualTo(0.5f));
            Assert.That(job.logs, Does.Contain("step1"));
        }

        [Test]
        public void StartJob_WorkThrows_TransitionsToFailed()
        {
            var id = _manager.StartJob("boom", ctx => throw new InvalidOperationException("kaboom"));

            var job = _manager.Get(id);
            Assert.That(job.status, Is.EqualTo(JobStatus.Failed));
            Assert.That(job.error, Does.Contain("kaboom"));
        }

        [Test]
        public void Cancel_TransitionsToCanceled_AndSignalsToken()
        {
            CancellationToken token = default;
            var id = _manager.StartJob("cancel-me", ctx => { token = ctx.Cancellation; });

            _manager.Cancel(id);

            Assert.That(_manager.Get(id).status, Is.EqualTo(JobStatus.Canceled));
            Assert.That(token.IsCancellationRequested, Is.True);
        }
    }
}
