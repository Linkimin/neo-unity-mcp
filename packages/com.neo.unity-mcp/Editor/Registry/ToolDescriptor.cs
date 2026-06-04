// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Reflection;

namespace Neo.UnityMcp.Registry
{
    internal sealed class ToolDescriptor
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Type ProviderType { get; set; }
        public MethodInfo Method { get; set; }
        public object ProviderInstance { get; set; }

        // Safety classification carried from [ReadOnlyTool] / [SceneEditingTool].
        // Captured at registration; used for tool gating in Task 7+ (not surfaced in v1 drop-in schema).
        public bool IsReadOnly { get; set; }
        public bool EditsScene { get; set; }
    }
}
