// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Neo.UnityMcp.Registry
{
    internal static class ToolSchemaBuilder
    {
        public static JObject Build(ToolDescriptor tool)
        {
            return new JObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description ?? string.Empty,
                ["inputSchema"] = BuildInputSchema(tool.Method)
            };
        }

        private static JObject BuildInputSchema(MethodInfo method)
        {
            var properties = new JObject();
            var required = new JArray();

            foreach (var parameter in method.GetParameters())
            {
                var toolParam = parameter.GetCustomAttribute<ToolParamAttribute>();
                var property = new JObject
                {
                    ["type"] = GetJsonType(parameter.ParameterType),
                    ["description"] = toolParam != null && !string.IsNullOrWhiteSpace(toolParam.Description)
                        ? toolParam.Description
                        : parameter.Name
                };

                if (parameter.ParameterType.IsEnum)
                    property["enum"] = new JArray(Enum.GetNames(parameter.ParameterType));

                properties[parameter.Name] = property;

                var isRequired = toolParam != null ? toolParam.Required : !parameter.HasDefaultValue;
                if (isRequired && !parameter.HasDefaultValue)
                    required.Add(parameter.Name);
            }

            return new JObject
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = required,
                ["additionalProperties"] = false
            };
        }

        private static string GetJsonType(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
                type = underlying;

            if (type == typeof(string) || type.IsEnum)
                return "string";
            if (type == typeof(bool))
                return "boolean";
            if (type == typeof(int) || type == typeof(long) || type == typeof(short) ||
                type == typeof(byte) || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort))
                return "integer";
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                return "number";
            if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)))
                return "array";
            if (typeof(JToken).IsAssignableFrom(type))
                return "object";

            return "object";
        }
    }
}
