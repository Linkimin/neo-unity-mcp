using Neo.UnityMcp.Registry;
using Neo.UnityMcp.Tools;
using NUnit.Framework;
using UnityEngine;

namespace Neo.UnityMcp.Tests.Tools
{
    public sealed class PlayVisualMenuToolsTests
    {
        [Test]
        public void ValidateMenuItem_ReturnsStructuredResult()
        {
            var response = (Response)MenuTools.ValidateMenuItem("File/Save Project");
            Assert.That(response.success, Is.True);
        }

        [Test]
        public void ExecuteMenuItem_EmptyPath_ReturnsError()
        {
            var response = (Response)MenuTools.ExecuteMenuItem("");
            Assert.That(response.success, Is.False);
            Assert.That(response.message, Is.EqualTo("MENU_PATH_REQUIRED"));
        }

        [Test]
        public void ExitPlayMode_WhenNotPlaying_ReportsNotPlaying()
        {
            var response = (Response)PlayModeTools.ExitPlayMode();
            Assert.That(response.success, Is.True);
            Assert.That(GetData(response, "isPlaying"), Is.EqualTo(false));
        }

        [Test]
        public void CaptureGameView_WithCamera_ReturnsPngDataUri()
        {
            var go = new GameObject("neo_cam_probe", typeof(Camera));
            try
            {
                var response = (Response)ScreenshotTools.CaptureGameView(256, 256);
                Assert.That(response.success, Is.True, response.message);
                var image = GetData(response, "image") as string;
                Assert.That(image, Does.StartWith("data:image/png;base64,"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static object GetData(Response response, string propertyName)
        {
            return response.data.GetType().GetProperty(propertyName).GetValue(response.data);
        }
    }
}
