// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Linq;
using System.Reflection;

namespace Neo.UnityMcp.Registry
{
    // Resolves a Type from a name across loaded assemblies (assembly-qualified,
    // namespace-qualified, or simple name). Ported as part of Task 3;
    // consumed by scene/component tools and the execution core in Task 4+.
    internal static class TypeResolver
    {
        public static Type Resolve(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return null;

            var type = Type.GetType(typeName, false);
            if (type != null)
                return type;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic)
                    continue;

                try
                {
                    type = assembly.GetType(typeName, false);
                    if (type != null)
                        return type;
                }
                catch
                {
                }
            }

            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic)
                .SelectMany(SafeGetTypes)
                .FirstOrDefault(candidate => string.Equals(candidate.Name, typeName, StringComparison.Ordinal));
        }

        private static Type[] SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null).ToArray();
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }
    }
}
