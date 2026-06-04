// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System.Collections.Generic;

namespace Neo.UnityMcp.Jobs
{
    internal enum JobStatus
    {
        Queued,
        Running,
        Succeeded,
        Failed,
        Canceled
    }

    // Serialized to Library/NeoMcp/jobs/<id>.json (Newtonsoft). Public fields keep the
    // on-disk shape stable and obvious.
    internal sealed class JobState
    {
        public string id;
        public string tool;
        public JobStatus status;
        public float? progress;
        public string result;
        public string error;
        public List<string> logs = new List<string>(); // tail buffer (ring, capped by JobManager)
        public string createdAtUtc;
        public string updatedAtUtc;
    }
}
