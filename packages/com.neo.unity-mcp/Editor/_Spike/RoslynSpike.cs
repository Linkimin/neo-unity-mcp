// Task 0 de-risk spike — proves Roslyn compiles & runs C# in this Unity editor
// without assembly conflicts. Remove (or keep behind the menu for regression) once
// the real execution core (Task 4) is in place.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UnityEditor;
using UnityEngine;

namespace Neo.UnityMcp.Spike
{
    public static class RoslynSpike
    {
        [MenuItem("Neo/Spike/Roslyn")]
        public static void Run()
        {
            const string code = @"
using System.Collections.Generic; using System.Linq; using UnityEngine;
public static class S {
    public static string Run() {
        var list = new List<int>{1,2,3}.Where(x => x > 1).ToList();
        var go = new GameObject(""SpikeProbe"");
        var name = go.name; Object.DestroyImmediate(go);
        return $""count={list.Count}; unity={Application.unityVersion}; go={name}"";
    }
}";
            try
            {
                var tree = CSharpSyntaxTree.ParseText(code);
                var refs = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                    .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
                    .ToList();
                var comp = CSharpCompilation.Create(
                    "NeoSpike_" + Guid.NewGuid().ToString("N"),
                    new[] { tree }, refs,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                using var ms = new MemoryStream();
                var emit = comp.Emit(ms);
                if (!emit.Success)
                {
                    Debug.LogError("[NeoSpike] compile FAILED:\n" + string.Join("\n",
                        emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(20)));
                    return;
                }
                var asm = Assembly.Load(ms.ToArray());
                var result = asm.GetType("S").GetMethod("Run").Invoke(null, null);
                Debug.Log("[NeoSpike] OK: " + result);
            }
            catch (Exception e)
            {
                Debug.LogError("[NeoSpike] EXCEPTION: " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
            }
        }
    }
}
