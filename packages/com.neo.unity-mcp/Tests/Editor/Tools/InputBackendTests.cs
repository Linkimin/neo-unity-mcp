using Neo.UnityMcp.Registry;
using Neo.UnityMcp.Tools;
using NUnit.Framework;

namespace Neo.UnityMcp.Tests.Tools
{
    public sealed class InputBackendTests
    {
        [Test]
        public void SelectBackend_NewActive_PrefersNew()
        {
            Assert.That(InputSimTools.SelectBackend(true, false), Is.EqualTo(InputSimTools.InputBackend.NewInputSystem));
        }

        [Test]
        public void SelectBackend_BothActive_PrefersNew()
        {
            Assert.That(InputSimTools.SelectBackend(true, true), Is.EqualTo(InputSimTools.InputBackend.NewInputSystem));
        }

        [Test]
        public void SelectBackend_LegacyOnly_SelectsLegacy()
        {
            Assert.That(InputSimTools.SelectBackend(false, true), Is.EqualTo(InputSimTools.InputBackend.Legacy));
        }

        [Test]
        public void SelectBackend_NoneActive_SelectsNone()
        {
            Assert.That(InputSimTools.SelectBackend(false, false), Is.EqualTo(InputSimTools.InputBackend.None));
        }

        [Test]
        public void GetInputBackend_ReturnsValidBackend()
        {
            var response = (Response)InputSimTools.GetInputBackend();

            Assert.That(response.success, Is.True);
            var backend = GetData(response, "backend") as string;
            Assert.That(new[] { "None", "Legacy", "NewInputSystem" }, Does.Contain(backend));
        }

        [Test]
        public void SimulateMouseClick_InEditMode_ReturnsGracefulError()
        {
            // Not in play mode (and possibly a non-New backend) -> structured error, never throws.
            var response = (Response)InputSimTools.SimulateMouseClick(10f, 10f, "left");

            Assert.That(response.success, Is.False);
        }

        private static object GetData(Response response, string propertyName)
        {
            return response.data.GetType().GetProperty(propertyName).GetValue(response.data);
        }
    }
}
