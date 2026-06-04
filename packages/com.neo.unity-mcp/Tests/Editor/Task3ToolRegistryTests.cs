// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neo.UnityMcp.DI;
using Neo.UnityMcp.Protocol;
using Neo.UnityMcp.Registry;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Neo.UnityMcp.Tests.Editor
{
    public sealed class Task3ToolRegistryTests
    {
        [Test]
        [Timeout(2000)]
        public async Task ToolsCall_AwaitsTaskResultWithoutSyncBlocking()
        {
            var handler = CreateHandler(typeof(AsyncToolProvider));

            var response = await handler.HandleRequestAsync(ToolCall("delayed_value"), default);

            Assert.That(response.Error, Is.Null);
            Assert.That(ResultText(response), Is.EqualTo("async-ok"));
        }

        [Test]
        public async Task ToolsCall_RejectsNonObjectArguments()
        {
            var handler = CreateHandler(typeof(NoArgumentToolProvider));

            var response = await handler.HandleRequestAsync(ToolCall("no_arguments", new JArray()), default);

            Assert.That(response.Error, Is.Not.Null);
            Assert.That(response.Error.Code, Is.EqualTo(-32602));
            Assert.That(response.Error.Message, Does.Contain("arguments"));
        }

        [Test]
        public async Task ConstructorInjectProvider_CanBeInstantiatedAndInvoked()
        {
            var dependency = new ConstructorDependency("injected-ok");
            var handler = CreateHandler(
                new[] { typeof(ConstructorInjectedToolProvider) },
                serviceType => serviceType == typeof(ConstructorDependency) ? dependency : null);

            var response = await handler.HandleRequestAsync(ToolCall("constructor_injected"), default);

            Assert.That(response.Error, Is.Null);
            Assert.That(ResultText(response), Is.EqualTo("injected-ok"));
        }

        [Test]
        public async Task DuplicateToolNames_AreRejectedDeterministically()
        {
            var handler = CreateHandler(typeof(DuplicateToolProviderA), typeof(DuplicateToolProviderB));

            var listResponse = await handler.HandleRequestAsync(new McpRequest
            {
                Id = 1,
                Method = "tools/list"
            }, default);
            var names = ToolNames(listResponse).ToArray();

            Assert.That(names, Does.Not.Contain("duplicate_name"));

            var callResponse = await handler.HandleRequestAsync(ToolCall("duplicate_name"), default);
            Assert.That(callResponse.Error, Is.Not.Null);
            Assert.That(callResponse.Error.Code, Is.EqualTo(-32602));
        }

        [Test]
        public async Task ParameterBinding_UsesOriginalCSharpParameterNames()
        {
            var handler = CreateHandler(typeof(ParameterContractToolProvider));

            var response = await handler.HandleRequestAsync(ToolCall("echo_project_path", new JObject
            {
                ["projectPath"] = "Assets/Scripts"
            }), default);

            Assert.That(response.Error, Is.Null);
            Assert.That(ResultText(response), Is.EqualTo("Assets/Scripts"));
        }

        [Test]
        public async Task ParameterBinding_DoesNotAcceptSnakeCaseWhenCSharpNameIsCamelCase()
        {
            var handler = CreateHandler(typeof(ParameterContractToolProvider));

            var response = await handler.HandleRequestAsync(ToolCall("echo_project_path", new JObject
            {
                ["project_path"] = "Assets/Scripts"
            }), default);

            Assert.That(response.Error, Is.Not.Null);
            Assert.That(response.Error.Code, Is.EqualTo(-32602));
            Assert.That(response.Error.Message, Does.Contain("projectPath"));
        }

        [Test]
        public async Task DefaultRegistry_ListsAndInvokesPing()
        {
            var handler = new McpRequestHandler("test", "0.0.0-test", "project-id");

            var listResponse = await handler.HandleRequestAsync(new McpRequest
            {
                Id = 1,
                Method = "tools/list"
            }, default);

            Assert.That(ToolNames(listResponse), Does.Contain("ping"));

            var callResponse = await handler.HandleRequestAsync(ToolCall("ping"), default);

            Assert.That(callResponse.Error, Is.Null);
            Assert.That(ResultText(callResponse), Is.EqualTo("pong"));
        }

        [Test]
        public async Task ToolsCall_ToolBodyException_IsReportedAsToolErrorNotProtocolError()
        {
            var handler = CreateHandler(typeof(ThrowingToolProvider));

            var response = await handler.HandleRequestAsync(ToolCall("boom"), default);

            Assert.That(response.Error, Is.Null, "A failing tool body must not become a JSON-RPC protocol error.");
            var result = (JObject)response.Result;
            Assert.That((bool)result["isError"], Is.True);
            Assert.That(ResultText(response), Does.Contain("kaboom"));
        }

        [Test]
        public void Registry_CapturesReadOnlyAndSceneEditingClassification()
        {
            var registry = ToolRegistry.FromProviderTypes(new[] { typeof(ClassifiedToolProvider) }, null);

            Assert.That(registry.TryGetTool("read_thing", out var readOnly), Is.True);
            Assert.That(readOnly.IsReadOnly, Is.True);
            Assert.That(readOnly.EditsScene, Is.False);

            Assert.That(registry.TryGetTool("edit_thing", out var sceneEditing), Is.True);
            Assert.That(sceneEditing.IsReadOnly, Is.False);
            Assert.That(sceneEditing.EditsScene, Is.True);
        }

        private static McpRequestHandler CreateHandler(params Type[] providerTypes)
        {
            return CreateHandler(providerTypes, null);
        }

        private static McpRequestHandler CreateHandler(IEnumerable<Type> providerTypes, Func<Type, object> serviceResolver)
        {
            var registry = ToolRegistry.FromProviderTypes(providerTypes, serviceResolver);
            return new McpRequestHandler("test", "0.0.0-test", "project-id", registry);
        }

        private static McpRequest ToolCall(string name, JToken arguments = null)
        {
            var parameters = new JObject
            {
                ["name"] = name
            };
            if (arguments != null)
                parameters["arguments"] = arguments;

            return new McpRequest
            {
                Id = 1,
                Method = "tools/call",
                Params = parameters
            };
        }

        private static string ResultText(McpResponse response)
        {
            var result = (JObject)response.Result;
            return (string)result["content"][0]["text"];
        }

        private static IEnumerable<string> ToolNames(McpResponse response)
        {
            var result = (JObject)response.Result;
            return result["tools"].Select(tool => (string)tool["name"]);
        }

        [NeoToolProvider]
        private static class AsyncToolProvider
        {
            [NeoTool("delayed_value", "Returns a value after an await boundary.")]
            public static async Task<string> DelayedValue()
            {
                await Task.Yield();
                return "async-ok";
            }
        }

        [NeoToolProvider]
        private static class NoArgumentToolProvider
        {
            [NeoTool("no_arguments", "Takes no arguments.")]
            public static string NoArguments()
            {
                return "ok";
            }
        }

        private sealed class ConstructorDependency
        {
            public ConstructorDependency(string value)
            {
                Value = value;
            }

            public string Value { get; private set; }
        }

        [NeoToolProvider]
        private sealed class ConstructorInjectedToolProvider
        {
            private readonly ConstructorDependency _dependency;

            [Inject]
            public ConstructorInjectedToolProvider(ConstructorDependency dependency)
            {
                _dependency = dependency;
            }

            [NeoTool("constructor_injected", "Returns injected dependency value.")]
            public string ConstructorInjected()
            {
                return _dependency.Value;
            }
        }

        [NeoToolProvider]
        private static class DuplicateToolProviderA
        {
            [NeoTool("duplicate_name", "Duplicate A.")]
            public static string Duplicate()
            {
                return "a";
            }
        }

        [NeoToolProvider]
        private static class DuplicateToolProviderB
        {
            [NeoTool("duplicate_name", "Duplicate B.")]
            public static string Duplicate()
            {
                return "b";
            }
        }

        [NeoToolProvider]
        private static class ParameterContractToolProvider
        {
            [NeoTool("echo_project_path", "Echoes project path.")]
            public static string EchoProjectPath(string projectPath)
            {
                return projectPath;
            }
        }

        [NeoToolProvider]
        private static class ThrowingToolProvider
        {
            [NeoTool("boom", "Always throws.")]
            public static string Boom()
            {
                throw new InvalidOperationException("kaboom");
            }
        }

        [NeoToolProvider]
        private static class ClassifiedToolProvider
        {
            [NeoTool("read_thing", "Read-only tool.")]
            [ReadOnlyTool]
            public static string ReadThing()
            {
                return "r";
            }

            [NeoTool("edit_thing", "Scene-editing tool.")]
            [SceneEditingTool]
            public static string EditThing()
            {
                return "e";
            }
        }
    }
}
