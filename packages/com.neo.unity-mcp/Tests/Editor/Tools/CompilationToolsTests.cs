using System.Threading.Tasks;
using Neo.UnityMcp.Registry;
using Neo.UnityMcp.Tools;
using NUnit.Framework;

namespace Neo.UnityMcp.Tests.Tools
{
    // NOTE: request_recompile is intentionally NOT exercised here — it can trigger a domain
    // reload that would tear down the running test session.
    public sealed class CompilationToolsTests
    {
        [Test]
        public void GetCompilationErrors_WhenIdle_ReportsClean()
        {
            var response = (Response)CompilationTools.GetCompilationErrors();

            Assert.That(response.success, Is.True);
            Assert.That(response.message, Does.StartWith("No compilation errors"));
            Assert.That(GetData(response, "clean"), Is.EqualTo(true));
        }

        [Test]
        public async Task WaitForCompilation_WhenIdle_CompletesWithoutErrors()
        {
            var response = (Response)await CompilationTools.WaitForCompilation(forceRefresh: false, timeoutSeconds: 5);

            Assert.That(response.success, Is.True);
            Assert.That(response.message, Does.Contain("Compilation complete"));
        }

        [Test]
        public void GetReloadRecoveryStatus_ReturnsStructuredResult()
        {
            var response = (Response)CompilationTools.GetReloadRecoveryStatus(consume: false);

            Assert.That(response.success, Is.True);
        }

        private static object GetData(Response response, string propertyName)
        {
            return response.data.GetType().GetProperty(propertyName).GetValue(response.data);
        }
    }
}
