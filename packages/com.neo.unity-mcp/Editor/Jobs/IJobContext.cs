// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System.Threading;

namespace Neo.UnityMcp.Jobs
{
    // Handed to a long-op's work delegate. Progress/logs/terminal transitions are persisted
    // through the JobManager (and thus the file-backed store), so they survive domain reloads.
    internal interface IJobContext
    {
        string JobId { get; }
        CancellationToken Cancellation { get; }

        void Report(float progress);
        void Log(string line);
        void Complete(string result);
        void Fail(string error);
    }
}
