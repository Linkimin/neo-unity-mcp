// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Neo.UnityMcp.DI;
using UnityEngine;

namespace Neo.UnityMcp.Registry
{
    internal sealed class ToolRegistry
    {
        private readonly Dictionary<string, ToolDescriptor> _tools;

        private ToolRegistry(Dictionary<string, ToolDescriptor> tools)
        {
            _tools = tools;
        }

        public IEnumerable<ToolDescriptor> Tools => _tools.Values.OrderBy(tool => tool.Name, StringComparer.Ordinal);

        public static ToolRegistry CreateDefault()
        {
            return FromProviderTypes(DiscoverProviderTypes(), ResolveRootService);
        }

        public static ToolRegistry FromProviderTypes(IEnumerable<Type> providerTypes, Func<Type, object> serviceResolver = null)
        {
            if (providerTypes == null)
                throw new ArgumentNullException(nameof(providerTypes));

            var discovered = new List<ToolDescriptor>();
            foreach (var providerType in providerTypes.OrderBy(type => type.FullName, StringComparer.Ordinal))
            {
                if (providerType == null || providerType.GetCustomAttribute<NeoToolProviderAttribute>() == null)
                    continue;

                object providerInstance = null;
                var methods = providerType
                    .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                    .Where(method => method.GetCustomAttribute<NeoToolAttribute>() != null)
                    .OrderBy(method => method.Name, StringComparer.Ordinal)
                    .ThenBy(method => method.ToString(), StringComparer.Ordinal);

                foreach (var method in methods)
                {
                    if (!method.IsStatic && providerInstance == null)
                    {
                        providerInstance = CreateProvider(providerType, serviceResolver);
                        if (providerInstance == null)
                            break;
                    }

                    var toolAttribute = method.GetCustomAttribute<NeoToolAttribute>();
                    var name = string.IsNullOrWhiteSpace(toolAttribute.Name)
                        ? ToSnakeCase(method.Name)
                        : toolAttribute.Name.Trim();

                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    discovered.Add(new ToolDescriptor
                    {
                        Name = name,
                        Description = string.IsNullOrWhiteSpace(toolAttribute.Description)
                            ? InsertSpaces(method.Name)
                            : toolAttribute.Description,
                        ProviderType = providerType,
                        ProviderInstance = method.IsStatic ? null : providerInstance,
                        Method = method,
                        IsReadOnly = method.GetCustomAttribute<ReadOnlyToolAttribute>() != null,
                        EditsScene = method.GetCustomAttribute<SceneEditingToolAttribute>() != null
                    });
                }
            }

            var tools = new Dictionary<string, ToolDescriptor>(StringComparer.Ordinal);
            foreach (var group in discovered.GroupBy(tool => tool.Name, StringComparer.Ordinal))
            {
                var orderedGroup = group
                    .OrderBy(tool => tool.ProviderType.FullName, StringComparer.Ordinal)
                    .ThenBy(tool => tool.Method.Name, StringComparer.Ordinal)
                    .ToArray();

                if (orderedGroup.Length == 1)
                {
                    tools[orderedGroup[0].Name] = orderedGroup[0];
                }
                else
                {
                    Debug.LogWarning("[Neo MCP Server] Duplicate tool name rejected: " + group.Key);
                }
            }

            return new ToolRegistry(tools);
        }

        public bool TryGetTool(string name, out ToolDescriptor tool)
        {
            if (name == null)
            {
                tool = null;
                return false;
            }

            return _tools.TryGetValue(name, out tool);
        }

        private static IEnumerable<Type> DiscoverProviderTypes()
        {
            var types = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(asm => asm.FullName, StringComparer.Ordinal))
            {
                if (assembly.IsDynamic)
                    continue;

                try
                {
                    types.AddRange(assembly.GetTypes().Where(type => type.GetCustomAttribute<NeoToolProviderAttribute>() != null));
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types.AddRange(ex.Types.Where(type => type != null && type.GetCustomAttribute<NeoToolProviderAttribute>() != null));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Neo MCP Server] Failed to scan tool assembly '" + assembly.FullName + "': " + ex.Message);
                }
            }

            return types;
        }

        private static object CreateProvider(Type providerType, Func<Type, object> serviceResolver)
        {
            try
            {
                var constructors = providerType
                    .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .OrderByDescending(ctor => ctor.GetCustomAttribute<InjectAttribute>() != null)
                    .ThenByDescending(ctor => ctor.GetParameters().Length)
                    .ToArray();

                foreach (var constructor in constructors)
                {
                    if (constructor.GetCustomAttribute<InjectAttribute>() == null &&
                        constructor.GetParameters().Length != 0)
                    {
                        continue;
                    }

                    if (TryBuildConstructorArguments(constructor, serviceResolver, out var args))
                    {
                        var instance = constructor.Invoke(args);
                        InjectMembers(instance, serviceResolver);
                        return instance;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Neo MCP Server] Failed to create tool provider '" + providerType.FullName + "': " + ex.Message);
            }

            return null;
        }

        private static bool TryBuildConstructorArguments(ConstructorInfo constructor, Func<Type, object> serviceResolver, out object[] args)
        {
            var parameters = constructor.GetParameters();
            args = new object[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                var service = ResolveService(parameters[i].ParameterType, serviceResolver);
                if (service == null)
                    return false;

                args[i] = service;
            }

            return true;
        }

        private static void InjectMembers(object instance, Func<Type, object> serviceResolver)
        {
            var type = instance.GetType();
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (field.GetCustomAttribute<InjectAttribute>() == null)
                    continue;

                var service = ResolveService(field.FieldType, serviceResolver);
                if (service != null)
                    field.SetValue(instance, service);
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (property.GetCustomAttribute<InjectAttribute>() == null || !property.CanWrite)
                    continue;

                var service = ResolveService(property.PropertyType, serviceResolver);
                if (service != null)
                    property.SetValue(instance, service, null);
            }
        }

        private static object ResolveService(Type type, Func<Type, object> serviceResolver)
        {
            return serviceResolver != null ? serviceResolver(type) : ResolveRootService(type);
        }

        private static object ResolveRootService(Type type)
        {
            return RootScopeServices.Instance.GetService(type);
        }

        public static string ToSnakeCase(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            var chars = new List<char>();
            for (var i = 0; i < name.Length; i++)
            {
                var current = name[i];
                if (char.IsUpper(current) && i > 0)
                {
                    var previousIsLower = char.IsLower(name[i - 1]);
                    var nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);
                    if (previousIsLower || nextIsLower)
                        chars.Add('_');
                }

                chars.Add(char.ToLowerInvariant(current));
            }

            return new string(chars.ToArray());
        }

        private static string InsertSpaces(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            var result = new System.Text.StringBuilder();
            for (var i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && char.IsLower(name[i - 1]))
                    result.Append(' ');
                result.Append(name[i]);
            }

            return result.ToString();
        }
    }
}
