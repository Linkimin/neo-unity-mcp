using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Neo.UnityMcp.Registry;
using Neo.UnityMcp.Tools;
using NUnit.Framework;
using UnityEngine;

namespace Neo.UnityMcp.Tests.Tools
{
    public sealed class SceneAndPackageToolsTests
    {
        private GameObject _probe;

        [SetUp]
        public void SetUp()
        {
            _probe = new GameObject("neo_hier_probe");
        }

        [TearDown]
        public void TearDown()
        {
            if (_probe != null)
                Object.DestroyImmediate(_probe);
        }

        [Test]
        public void GetHierarchy_ReturnsScenesWithRoots()
        {
            var response = (Response)HierarchyTools.GetHierarchy(2, true);

            Assert.That(response.success, Is.True);
            Assert.That(GetData(response, "sceneCount"), Is.Not.Null);
        }

        [Test]
        public void GetGameObjectInfo_ByName_ReturnsComponents()
        {
            var response = (Response)HierarchyTools.GetGameObjectInfo("neo_hier_probe", "by_name");

            Assert.That(response.success, Is.True, response.message);
            Assert.That(GetData(response, "name"), Is.EqualTo("neo_hier_probe"));
            var components = (IEnumerable)GetData(response, "components");
            Assert.That(components.Cast<object>().Count(), Is.GreaterThanOrEqualTo(1)); // at least Transform
        }

        [Test]
        public void GetGameObjectInfo_Missing_ReturnsError()
        {
            var response = (Response)HierarchyTools.GetGameObjectInfo("nope_does_not_exist_xyz", "by_name");

            Assert.That(response.success, Is.False);
            Assert.That(response.message, Is.EqualTo("GAME_OBJECT_NOT_FOUND"));
        }

        [Test]
        public void FindGameObjects_ByNameSubstring_FindsProbe()
        {
            var response = (Response)HierarchyTools.FindGameObjects("neo_hier_probe", null, true, 50);

            Assert.That(response.success, Is.True);
            Assert.That((int)GetData(response, "count"), Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void GetSceneInfo_ReturnsActiveScene()
        {
            var response = (Response)SceneQueryTools.GetSceneInfo();

            Assert.That(response.success, Is.True);
            Assert.That(GetData(response, "sceneCount"), Is.Not.Null);
        }

        [Test]
        [Timeout(20000)]
        public async Task ListPackages_ReturnsInstalledPackages()
        {
            var response = (Response)await PackageTools.ListPackages();

            Assert.That(response.success, Is.True, response.message);
            Assert.That((int)GetData(response, "count"), Is.GreaterThan(0));
        }

        private static object GetData(Response response, string propertyName)
        {
            return response.data.GetType().GetProperty(propertyName).GetValue(response.data);
        }
    }
}
