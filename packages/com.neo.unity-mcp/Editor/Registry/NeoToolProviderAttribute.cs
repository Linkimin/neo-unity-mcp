// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;

namespace Neo.UnityMcp.Registry
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class NeoToolProviderAttribute : Attribute
    {
        public NeoToolProviderAttribute(string category = null)
        {
            Category = category;
        }

        public string Category { get; private set; }
    }
}
