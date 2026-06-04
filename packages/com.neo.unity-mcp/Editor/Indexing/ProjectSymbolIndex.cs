// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.UnityMcp.Indexing
{
    // Project namespaces sourced from assembly METADATA (Assembly.GetExportedTypes().Namespace),
    // not by scanning .cs files — O(assemblies), not O(files). Only project assemblies (per
    // AssemblyDefinitionIndex) are inspected. Cached; reset by IndexInvalidation.
    internal static class ProjectSymbolIndex
    {
        private static List<string> _namespaces;

        public static IReadOnlyList<string> ProjectNamespaces
        {
            get { EnsureBuilt(); return _namespaces; }
        }

        public static void Invalidate()
        {
            _namespaces = null;
        }

        private static void EnsureBuilt()
        {
            if (_namespaces != null)
                return;

            var projectNames = AssemblyDefinitionIndex.ProjectAssemblyNames;
            var namespaces = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic)
                    continue;

                string name;
                try
                {
                    name = assembly.GetName().Name;
                }
                catch
                {
                    continue;
                }

                if (string.IsNullOrEmpty(name) || !projectNames.Contains(name))
                    continue;

                Type[] types;
                try
                {
                    types = assembly.GetExportedTypes();
                }
                catch (System.Reflection.ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (var type in types)
                {
                    if (!string.IsNullOrEmpty(type.Namespace))
                        namespaces.Add(type.Namespace);
                }
            }

            _namespaces = namespaces.ToList();
        }
    }
}
