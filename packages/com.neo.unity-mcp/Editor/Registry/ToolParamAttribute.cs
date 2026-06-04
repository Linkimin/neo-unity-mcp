// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;

namespace Neo.UnityMcp.Registry
{
    [AttributeUsage(AttributeTargets.Parameter)]
    internal sealed class ToolParamAttribute : Attribute
    {
        public ToolParamAttribute(string description = null)
        {
            Description = description;
        }

        public string Description { get; private set; }
        public bool Required { get; set; } = true;
    }
}
