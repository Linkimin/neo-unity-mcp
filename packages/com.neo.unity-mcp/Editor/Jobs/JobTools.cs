// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Generic;
using System.Linq;
using Neo.UnityMcp.DI;
using Neo.UnityMcp.Registry;

namespace Neo.UnityMcp.Jobs
{
    // Control/observability tools for long-running jobs. The job itself is started inside a
    // long-op tool (first consumer: run_tests, Task 9), which returns the jobId.
    [NeoToolProvider("Jobs")]
    internal sealed class JobTools
    {
        private readonly JobManager _manager;

        [Inject]
        public JobTools(JobManager manager)
        {
            _manager = manager;
        }

        [NeoTool("get_job_status", "Status, progress, result and error for a long-running job.")]
        [ReadOnlyTool]
        public object GetJobStatus([ToolParam("Job id returned by the long-op tool.")] string jobId)
        {
            var job = _manager.Get(jobId);
            if (job == null)
                return Response.Error("JOB_NOT_FOUND", new { jobId });

            return Response.Success("Job status.", new
            {
                id = job.id,
                tool = job.tool,
                status = job.status.ToString(),
                progress = job.progress,
                result = job.result,
                error = job.error
            });
        }

        [NeoTool("cancel_job", "Request cancellation of a running job.")]
        public object CancelJob([ToolParam("Job id.")] string jobId)
        {
            var job = _manager.Get(jobId);
            if (job == null)
                return Response.Error("JOB_NOT_FOUND", new { jobId });

            _manager.Cancel(jobId);
            return Response.Success("Job cancellation requested.", new
            {
                id = jobId,
                status = _manager.Get(jobId)?.status.ToString()
            });
        }

        [NeoTool("get_job_logs", "Tail of a job's captured log lines.")]
        [ReadOnlyTool]
        public object GetJobLogs(
            [ToolParam("Job id.")] string jobId,
            [ToolParam("Number of trailing lines (default 50).", Required = false)] int tailLines = 50)
        {
            var job = _manager.Get(jobId);
            if (job == null)
                return Response.Error("JOB_NOT_FOUND", new { jobId });

            var logs = job.logs ?? new List<string>();
            if (tailLines < 0)
                tailLines = 0;
            var tail = logs.Skip(Math.Max(0, logs.Count - tailLines)).ToList();

            return Response.Success(tail.Count + " log line(s).", new
            {
                id = jobId,
                total = logs.Count,
                logs = tail
            });
        }
    }
}
