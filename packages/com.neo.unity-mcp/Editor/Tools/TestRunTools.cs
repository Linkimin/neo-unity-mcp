// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Text;
using Neo.UnityMcp.DI;
using Neo.UnityMcp.Jobs;
using Neo.UnityMcp.Registry;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Neo.UnityMcp.Tools
{
    // run_tests as a long-running job (decision: closes the test-timeout hole). Returns a jobId
    // immediately; the run completes asynchronously via TestRunnerApi callbacks, which flush
    // status/logs through the file-backed JobManager. Poll get_job_status / get_job_logs.
    [NeoToolProvider("Tests")]
    internal sealed class TestRunTools
    {
        private readonly JobManager _jobs;

        // Test seam: the actual execution. Swapped in unit tests so wiring can be verified
        // without nesting a real test run inside the test runner.
        internal static Action<string, string, string, string, JobManager> Runner = DefaultRunner;

        [Inject]
        public TestRunTools(JobManager jobs)
        {
            _jobs = jobs;
        }

        [NeoTool("run_tests",
            "Run EditMode/PlayMode tests as a background job. Returns a jobId immediately (no timeout); " +
            "poll get_job_status for pass/fail counts and get_job_logs for per-test results.")]
        public object RunTests(
            [ToolParam("EditMode or PlayMode (default EditMode).", Required = false)] string mode = "EditMode",
            [ToolParam("Assembly name filter (optional).", Required = false)] string assembly = null,
            [ToolParam("Full test name filter (optional).", Required = false)] string testFilter = null)
        {
            var jobId = _jobs.StartJob("run_tests", ctx => Runner(ctx.JobId, mode, assembly, testFilter, _jobs));
            return Response.Success("Test run started.", new { jobId, mode });
        }

        private static void DefaultRunner(string jobId, string mode, string assembly, string testFilter, JobManager jobs)
        {
            var testMode = string.Equals(mode, "PlayMode", StringComparison.OrdinalIgnoreCase)
                ? TestMode.PlayMode
                : TestMode.EditMode;

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new JobCallbacks(jobs, jobId));

            var filter = new Filter { testMode = testMode };
            if (!string.IsNullOrEmpty(assembly))
                filter.assemblyNames = new[] { assembly };
            if (!string.IsNullOrEmpty(testFilter))
                filter.testNames = new[] { testFilter };

            jobs.Log(jobId, "Starting " + testMode + " test run...");
            api.Execute(new ExecutionSettings(filter));
        }

        private sealed class JobCallbacks : ICallbacks
        {
            private readonly JobManager _jobs;
            private readonly string _jobId;

            public JobCallbacks(JobManager jobs, string jobId)
            {
                _jobs = jobs;
                _jobId = jobId;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                // Log leaf results only.
                if (!result.HasChildren)
                    _jobs.Log(_jobId, result.TestStatus + ": " + result.FullName);
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var summary = string.Format(
                    "{0} passed={1} failed={2} skipped={3} inconclusive={4} duration={5:0.00}s",
                    result.TestStatus, result.PassCount, result.FailCount, result.SkipCount,
                    result.InconclusiveCount, result.Duration);

                if (result.FailCount > 0)
                {
                    var sb = new StringBuilder(summary);
                    AppendFailures(result, sb);
                    _jobs.Fail(_jobId, sb.ToString());
                }
                else
                {
                    _jobs.Complete(_jobId, summary);
                }
            }

            private static void AppendFailures(ITestResultAdaptor result, StringBuilder sb)
            {
                if (result.HasChildren)
                {
                    foreach (var child in result.Children)
                        AppendFailures(child, sb);
                    return;
                }

                if (result.TestStatus == TestStatus.Failed)
                {
                    sb.Append("\nFAIL: ").Append(result.FullName);
                    if (!string.IsNullOrEmpty(result.Message))
                        sb.Append(" — ").Append(result.Message.Replace("\r", " ").Replace("\n", " "));
                }
            }
        }
    }
}
