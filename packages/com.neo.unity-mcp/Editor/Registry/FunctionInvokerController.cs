// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.UnityMcp.Registry
{
    internal sealed class FunctionInvokerController
    {
        private readonly ToolRegistry _registry;

        public FunctionInvokerController(ToolRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public async Task<object> InvokeAsync(string toolName, JObject arguments)
        {
            if (!_registry.TryGetTool(toolName, out var tool))
                throw new ToolInvocationException("Unknown tool: " + toolName);

            // Argument binding failures are PROTOCOL errors (-32602): let
            // ToolInvocationException propagate out of the try below.
            var invokeArguments = BuildArguments(tool.Method, arguments ?? new JObject());

            // Failures from here on are TOOL errors (isError=true), not protocol errors.
            try
            {
                var result = tool.Method.Invoke(tool.ProviderInstance, invokeArguments);
                return await NormalizeResultAsync(result);
            }
            catch (TargetInvocationException ex)
            {
                throw new ToolExecutionException((ex.InnerException ?? ex).Message);
            }
            catch (Exception ex)
            {
                // Async tool bodies surface their fault here via the await above.
                throw new ToolExecutionException(ex.Message);
            }
        }

        private static async Task<object> NormalizeResultAsync(object result)
        {
            var task = result as Task;
            if (task == null)
                return result;

            await task;

            var resultProperty = task.GetType().GetProperty("Result");
            return resultProperty == null ? null : resultProperty.GetValue(task, null);
        }

        private static object[] BuildArguments(MethodInfo method, JObject arguments)
        {
            var parameters = method.GetParameters();
            var values = new object[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                var token = arguments[parameter.Name];
                if (token == null)
                {
                    if (parameter.HasDefaultValue)
                    {
                        values[i] = parameter.DefaultValue;
                        continue;
                    }

                    throw new ToolInvocationException("Missing required parameter: " + parameter.Name);
                }

                values[i] = ConvertToken(token, parameter.ParameterType, parameter.Name);
            }

            return values;
        }

        private static object ConvertToken(JToken token, Type targetType, string parameterName)
        {
            if (token.Type == JTokenType.Null)
            {
                if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
                    return null;

                throw new ToolInvocationException("Parameter '" + parameterName + "' cannot be null.");
            }

            var nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType != null)
                targetType = nullableType;

            try
            {
                if (targetType == typeof(JToken) || targetType.IsAssignableFrom(token.GetType()))
                    return token;
                if (targetType == typeof(JObject))
                    return token as JObject ?? throw new ToolInvocationException("Parameter '" + parameterName + "' must be an object.");
                if (targetType == typeof(JArray))
                    return token as JArray ?? throw new ToolInvocationException("Parameter '" + parameterName + "' must be an array.");
                if (targetType == typeof(string))
                    return token.Type == JTokenType.String ? (string)token : token.ToString(Formatting.None);
                if (targetType.IsEnum)
                    return Enum.Parse(targetType, token.ToString(), true);
                if (targetType == typeof(bool))
                    return token.Value<bool>();
                if (targetType == typeof(int))
                    return token.Value<int>();
                if (targetType == typeof(long))
                    return token.Value<long>();
                if (targetType == typeof(float))
                    return token.Value<float>();
                if (targetType == typeof(double))
                    return token.Value<double>();
                if (targetType == typeof(decimal))
                    return token.Value<decimal>();

                return token.ToObject(targetType);
            }
            catch (ToolInvocationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ToolInvocationException(
                    string.Format(CultureInfo.InvariantCulture, "Invalid value for parameter '{0}': {1}", parameterName, ex.Message));
            }
        }
    }
}
