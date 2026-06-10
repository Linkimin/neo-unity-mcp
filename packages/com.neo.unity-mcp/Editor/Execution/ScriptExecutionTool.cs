// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Linq;
using System.Reflection;
using Neo.UnityMcp.Indexing;
using Neo.UnityMcp.Registry;

namespace Neo.UnityMcp.Execution
{
    // Primary execution tool (drop-in name: execute_code). Compiles a C# snippet in memory
    // (Roslyn, hash-cached) and runs it on the editor thread. Two templates:
    //   1) Recommended: implement INeoCommand — receives a NeoExecutionContext (auto-Undo,
    //      change tracking, structured logs returned in the response).
    //   2) Legacy: any class with `public static string Run()` — return value is the message.
    [NeoToolProvider("Script")]
    internal static class ScriptExecutionTool
    {
        private static readonly NeoScriptCompiler Compiler = new NeoScriptCompiler(new ReferenceSetBuilder());

        // Usings are injected TEXTUALLY (not via CSharpCompilationOptions.WithUsings, which only
        // applies to SourceCodeKind.Script). Source = NamespaceUsingsCache (framework defaults +
        // project namespaces from the metadata index). Duplicate `using` -> CS0105 warning only.
        private static string BuildUsings()
        {
            return string.Concat(NamespaceUsingsCache.GetUsings().Select(ns => "using " + ns + ";\n"));
        }

        [NeoTool("execute_code",
            "Compile a C# snippet in memory (Roslyn, hash-cached) and run it on the editor thread. " +
            "Implement INeoCommand for auto-Undo + structured logs, or use a class with public static string Run().")]
        [SceneEditingTool]
        public static object ExecuteCode(
            [ToolParam("C# code to execute. INeoCommand or legacy static Run() template.")] string code,
            [ToolParam("Reject obviously dangerous patterns before compiling. Default true.", Required = false)] bool? safety_checks = null)
        {
            if (string.IsNullOrWhiteSpace(code))
                return Response.Error("EMPTY_CODE");

            var safety = safety_checks ?? true;
            if (safety && SafetyChecks.IsBlocked(code, out var reason))
                return Response.Error("SAFETY_CHECK_BLOCKED",
                    new { reason, hint = "Pass safety_checks=false to bypass." });

            var usings = BuildUsings();
            var fullCode = code.Contains("class ") ? usings + code : WrapCode(code, usings);

            CompiledScript compiled;
            try
            {
                compiled = Compiler.Compile(fullCode);
            }
            catch (Exception ex)
            {
                return Response.Error("EXECUTE_CODE_FAILED", new { error = ex.ToString() });
            }

            if (!compiled.Success)
                return Response.Error("COMPILATION_FAILED",
                    new { errors = compiled.Diagnostics, count = compiled.Diagnostics.Count });

            var commandType = FindCommandType(compiled.Assembly);
            if (commandType != null)
                return ExecuteAsCommand(commandType, compiled.FromCacheHit);

            var runMethod = FindRunMethod(compiled.Assembly);
            if (runMethod == null)
                return Response.Error("NO_ENTRY_POINT",
                    new { hint = "Implement INeoCommand or provide a public static Run()." });

            try
            {
                var result = runMethod.Invoke(null, null);
                return Response.Success("Executed (legacy Run()).",
                    new { result = result?.ToString() ?? "OK", cached = compiled.FromCacheHit });
            }
            catch (TargetInvocationException ex)
            {
                var inner = ex.InnerException ?? ex;
                return Response.Error("RUNTIME_ERROR", new { message = inner.Message, stack = inner.StackTrace });
            }
        }

        private static object ExecuteAsCommand(Type commandType, bool cached)
        {
            INeoCommand instance;
            try
            {
                instance = (INeoCommand)Activator.CreateInstance(commandType);
            }
            catch (Exception ex)
            {
                return Response.Error("COMMAND_INSTANTIATION_FAILED",
                    new { type = commandType.FullName, error = ex.Message });
            }

            var ctx = new NeoExecutionContext();
            try
            {
                instance.Execute(ctx);
            }
            catch (Exception ex)
            {
                return Response.Error("COMMAND_RUNTIME_ERROR", new
                {
                    message = ex.Message,
                    stack = ex.StackTrace,
                    logs = ctx.Logs,
                    created = ctx.CreatedInstanceIds,
                    modified = ctx.ModifiedInstanceIds,
                    destroyed = ctx.DestroyedInstanceIds
                });
            }

            return Response.Success("Command executed.", new
            {
                logs = ctx.Logs,
                created = ctx.CreatedInstanceIds,
                modified = ctx.ModifiedInstanceIds,
                destroyed = ctx.DestroyedInstanceIds,
                returnValue = ctx.ReturnValue,
                cached
            });
        }

        private static Type FindCommandType(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes().FirstOrDefault(t =>
                    typeof(INeoCommand).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
            }
            catch (ReflectionTypeLoadException)
            {
                return null;
            }
        }

        private static MethodInfo FindRunMethod(Assembly assembly)
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    var method = type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                    if (method != null)
                        return method;
                }
            }
            catch (ReflectionTypeLoadException)
            {
            }

            return null;
        }

        private static string WrapCode(string code, string usings)
        {
            return usings +
                   "public static class NeoSnippet\n" +
                   "{\n" +
                   "    public static string Run()\n" +
                   "    {\n" +
                   "        " + code + "\n" +
                   "        return \"OK\";\n" +
                   "    }\n" +
                   "}\n";
        }
    }
}
