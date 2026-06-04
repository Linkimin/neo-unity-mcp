using System;
using System.Collections;
using System.Linq;
using Neo.UnityMcp.Registry;
using Neo.UnityMcp.Services;
using Neo.UnityMcp.Tools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Neo.UnityMcp.Tests.Tools
{
    public sealed class ObservabilityToolsTests
    {
        [Test]
        public void GetEditorState_ReturnsStructuredState()
        {
            var response = (Response)EditorStateTools.GetEditorState();

            Assert.That(response.success, Is.True);
            var version = GetData(response, "unityVersion") as string;
            Assert.That(version, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void GetConsoleLogs_CapturesEmittedLog()
        {
            var marker = "neo_console_probe_" + Guid.NewGuid().ToString("N");
            Debug.LogWarning(marker); // warnings do not fail the test runner

            var response = (Response)ConsoleTools.GetConsoleLogs(500, "warning");

            Assert.That(response.success, Is.True);
            var entries = ((IEnumerable)GetData(response, "entries"))
                .Cast<ConsoleLogService.Entry>()
                .Select(e => e.message);
            Assert.That(entries, Has.Some.Contains(marker));
        }

        [Test]
        public void GetSelection_ReturnsStructuredResult()
        {
            var response = (Response)EditorStateTools.GetSelection();
            Assert.That(response.success, Is.True);
            Assert.That(GetData(response, "count"), Is.Not.Null);
        }

        [Test]
        public void SetSelection_ByName_SelectsThenClears()
        {
            var go = new GameObject("neo_sel_probe");
            try
            {
                var set = (Response)EditorStateTools.SetSelection("neo_sel_probe", "by_name");
                Assert.That(set.success, Is.True);
                Assert.That(Selection.gameObjects, Does.Contain(go));

                var cleared = (Response)EditorStateTools.SetSelection("", null);
                Assert.That(cleared.success, Is.True);
                Assert.That(Selection.gameObjects, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static object GetData(Response response, string propertyName)
        {
            return response.data.GetType().GetProperty(propertyName).GetValue(response.data);
        }
    }
}
