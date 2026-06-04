using System;
using System.IO;
using Neo.UnityMcp.Jobs;
using Neo.UnityMcp.Registry;
using Neo.UnityMcp.Tools;
using NUnit.Framework;

namespace Neo.UnityMcp.Tests.Tools
{
    // Verifies run_tests job wiring via an injected runner — without nesting a real test run
    // inside the test runner.
    public sealed class TestRunToolsTests
    {
        private string _dir;
        private JobManager _jobs;
        private TestRunTools _tools;
        private Action<string, string, string, string, JobManager> _originalRunner;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "neo-jobs-" + Guid.NewGuid().ToString("N"));
            _jobs = new JobManager(new JobStore(_dir));
            _tools = new TestRunTools(_jobs);
            _originalRunner = TestRunTools.Runner;
        }

        [TearDown]
        public void TearDown()
        {
            TestRunTools.Runner = _originalRunner;
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
            catch { }
        }

        [Test]
        public void RunTests_ReturnsJobId_AndCompletesViaRunner()
        {
            TestRunTools.Runner = (jobId, mode, asm, filter, jobs) =>
            {
                jobs.Log(jobId, "Passed: Dummy.Test1");
                jobs.Complete(jobId, "Passed passed=2 failed=0");
            };

            var response = (Response)_tools.RunTests("EditMode", null, null);

            Assert.That(response.success, Is.True);
            var jobId = GetData(response, "jobId") as string;
            Assert.That(jobId, Is.Not.Null.And.Not.Empty);

            var job = _jobs.Get(jobId);
            Assert.That(job.status, Is.EqualTo(JobStatus.Succeeded));
            Assert.That(job.result, Does.Contain("passed=2"));
            Assert.That(job.logs, Does.Contain("Passed: Dummy.Test1"));
        }

        [Test]
        public void RunTests_ReturnsImmediately_JobRunning_WhenRunnerIsAsync()
        {
            TestRunTools.Runner = (jobId, mode, asm, filter, jobs) => { /* simulate async: not completed yet */ };

            var response = (Response)_tools.RunTests("EditMode", null, null);
            var jobId = GetData(response, "jobId") as string;

            Assert.That(_jobs.Get(jobId).status, Is.EqualTo(JobStatus.Running));
        }

        [Test]
        public void RunTests_FailingRun_MarksJobFailed()
        {
            TestRunTools.Runner = (jobId, mode, asm, filter, jobs) => jobs.Fail(jobId, "Failed passed=1 failed=1");

            var response = (Response)_tools.RunTests("EditMode", null, null);
            var jobId = GetData(response, "jobId") as string;

            Assert.That(_jobs.Get(jobId).status, Is.EqualTo(JobStatus.Failed));
        }

        private static object GetData(Response response, string propertyName)
        {
            return response.data.GetType().GetProperty(propertyName).GetValue(response.data);
        }
    }
}
