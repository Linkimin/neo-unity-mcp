using System.Collections;
using System.Linq;
using Neo.UnityMcp.Execution;
using Neo.UnityMcp.Registry;
using NUnit.Framework;
using UnityEngine;

namespace Neo.UnityMcp.Tests.Execution
{
    public sealed class ScriptExecutionToolTests
    {
        [TearDown]
        public void Cleanup()
        {
            var go = GameObject.Find("neo_exec_test");
            if (go != null)
                Object.DestroyImmediate(go);
        }

        [Test]
        public void Legacy_Run_ReturnsValue()
        {
            var response = (Response)ScriptExecutionTool.ExecuteCode(
                "return (new System.Collections.Generic.List<int>{1,2,3}).Where(x => x > 1).Count().ToString();",
                false);

            Assert.That(response.success, Is.True, response.message);
            Assert.That(GetData(response, "result"), Is.EqualTo("2"));
        }

        [Test]
        public void Command_CreatesAndTracksObject()
        {
            const string code = @"
public class CreateCube : INeoCommand
{
    public void Execute(NeoExecutionContext ctx)
    {
        var go = new GameObject(""neo_exec_test"");
        ctx.RegisterObjectCreation(go);
        ctx.Log(""created {0}"", go.name);
    }
}";
            var response = (Response)ScriptExecutionTool.ExecuteCode(code, false);

            Assert.That(response.success, Is.True, Failure(response));
            var created = (IEnumerable)GetData(response, "created");
            Assert.That(created.Cast<object>().Count(), Is.EqualTo(1));
            Assert.That(GameObject.Find("neo_exec_test"), Is.Not.Null);
        }

        [Test]
        public void Safety_BlocksDangerousByDefault()
        {
            var response = (Response)ScriptExecutionTool.ExecuteCode("File.Delete(\"x\");");

            Assert.That(response.success, Is.False);
            Assert.That(response.message, Is.EqualTo("SAFETY_CHECK_BLOCKED"));
        }

        [Test]
        public void Compilation_Error_ReportsFailure()
        {
            var response = (Response)ScriptExecutionTool.ExecuteCode("return notavar + 1;", false);

            Assert.That(response.success, Is.False);
            Assert.That(response.message, Is.EqualTo("COMPILATION_FAILED"));
        }

        private static object GetData(Response response, string propertyName)
        {
            return response.data.GetType().GetProperty(propertyName).GetValue(response.data);
        }

        private static string Failure(Response response)
        {
            if (response.success)
                return string.Empty;

            var errors = response.data?.GetType().GetProperty("errors")?.GetValue(response.data) as IEnumerable;
            var joined = errors == null ? string.Empty : string.Join(" | ", errors.Cast<object>());
            return response.message + ": " + joined;
        }
    }
}
