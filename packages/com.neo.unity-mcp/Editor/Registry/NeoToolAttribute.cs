// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;

namespace Neo.UnityMcp.Registry
{
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class NeoToolAttribute : Attribute
    {
        public NeoToolAttribute(string name = null, string description = null)
        {
            Name = name;
            Description = description;
        }

        public string Name { get; private set; }
        public string Description { get; private set; }
    }
}
