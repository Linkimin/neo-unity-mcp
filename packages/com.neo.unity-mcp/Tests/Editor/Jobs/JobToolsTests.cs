using System;
using System.IO;
using System.Linq;
using Neo.UnityMcp.Jobs;
using Neo.UnityMcp.Registry;
using NUnit.Framework;

namespace Neo.UnityMcp.Tests.Jobs
{
    public sealed class JobToolsTests
    {
        private string _dir;
        private JobManager _manager;
        private JobTools _tools;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "neo-jobs-" + Guid.NewGuid().ToString("N"));
            _manager = new JobManager(new JobStore(_dir));
            _tools = new JobTools(_manager);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
            catch { }
        }

        [Test]
        public void GetJobStatus_UnknownJob_ReturnsError()
        {
            var response = (Response)_tools.GetJobStatus("does-not-exist");

            Assert.That(response.success, Is.False);
            Assert.That(response.message, Is.EqualTo("JOB_NOT_FOUND"));
        }

        [Test]
        public void GetJobStatus_RunningJob_ReportsStatus()
        {
            var id = _manager.StartJob("noop", ctx => { });

            var response = (Response)_tools.GetJobStatus(id);

            Assert.That(response.success, Is.True);
            Assert.That(GetData(response, "status"), Is.EqualTo("Running"));
        }

        [Test]
        public void GetJobLogs_ReturnsTail()
        {
            var id = _manager.StartJob("work", ctx =>
            {
                ctx.Log("a");
                ctx.Log("b");
                ctx.Log("c");
            });

            var response = (Response)_tools.GetJobLogs(id, 2);

            Assert.That(response.success, Is.True);
            var logs = ((System.Collections.IEnumerable)GetData(response, "logs")).Cast<object>().Select(o => o.ToString()).ToList();
            Assert.That(logs, Is.EqualTo(new[] { "b", "c" }));
        }

        [Test]
        public void CancelJob_SetsCanceled()
        {
            var id = _manager.StartJob("cancel-me", ctx => { });

            var response = (Response)_tools.CancelJob(id);

            Assert.That(response.success, Is.True);
            Assert.That(_manager.Get(id).status, Is.EqualTo(JobStatus.Canceled));
        }

        [Test]
        public void Registry_DiscoversJobTools_ViaConstructorInjection()
        {
            // Validates [Inject] ctor resolution + tool discovery for the instance provider.
            var registry = ToolRegistry.FromProviderTypes(
                new[] { typeof(JobTools) },
                serviceType => serviceType == typeof(JobManager) ? _manager : null);

            var names = registry.Tools.Select(t => t.Name).ToList();
            Assert.That(names, Does.Contain("get_job_status"));
            Assert.That(names, Does.Contain("cancel_job"));
            Assert.That(names, Does.Contain("get_job_logs"));
        }

        private static object GetData(Response response, string propertyName)
        {
            return response.data.GetType().GetProperty(propertyName).GetValue(response.data);
        }
    }
}
