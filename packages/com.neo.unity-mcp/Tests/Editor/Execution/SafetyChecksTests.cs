using Neo.UnityMcp.Execution;
using NUnit.Framework;

namespace Neo.UnityMcp.Tests.Execution
{
    public sealed class SafetyChecksTests
    {
        [Test]
        public void Blocks_DangerousPatterns()
        {
            Assert.That(SafetyChecks.IsBlocked("File.Delete(\"x\");", out var fileReason), Is.True);
            Assert.That(fileReason, Does.Contain("File.Delete"));

            Assert.That(SafetyChecks.IsBlocked("while (true) { }", out _), Is.True);
            Assert.That(SafetyChecks.IsBlocked("Process.Start(\"x\");", out _), Is.True);
            Assert.That(SafetyChecks.IsBlocked("Environment.Exit(0);", out _), Is.True);
        }

        [Test]
        public void Allows_CleanSnippet()
        {
            Assert.That(SafetyChecks.IsBlocked("var x = 1 + 2; Debug.Log(x);", out var reason), Is.False);
            Assert.That(reason, Is.Null);
        }
    }
}
