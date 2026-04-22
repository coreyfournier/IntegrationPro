using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json.Nodes;

namespace IntegrationPro.Application.Catalog;

/// <summary>
/// Build a JSON Schema representation of a POCO type directly via reflection.
/// Emitted as <see cref="JsonObject"/> so the catalog never stores an external
/// library's schema object — avoids any cross-ALC schema-cache collisions when
/// two plugin versions expose types with identical FullName/AssemblyQualifiedName.
/// </summary>
internal static class PocoSchemaBuilder
{
    public static JsonObject Build(Type type)
    {
        var schema = new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft-07/schema#",
            ["type"] = "object",
            ["title"] = type.Name,
            ["additionalProperties"] = false,
        };

        var properties = new JsonObject();
        var required = new List<string>();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead) continue;
            var jsonName = Camel(prop.Name);
            properties[jsonName] = BuildPropertySchema(prop);
            if (prop.GetCustomAttribute<RequiredAttribute>() is not null)
                required.Add(jsonName);
        }

        schema["properties"] = properties;
        if (required.Count > 0)
            schema["required"] = new JsonArray(required.Select(r => JsonValue.Create(r)!).ToArray<JsonNode>());

        return schema;
    }

    private static JsonObject BuildPropertySchema(PropertyInfo prop)
    {
        var propType = prop.PropertyType;
        var isNullable = Nullable.GetUnderlyingType(propType) is not null
            || !propType.IsValueType;
        var innerType = Nullable.GetUnderlyingType(propType) ?? propType;

        var p = new JsonObject();
        SetType(p, innerType, isNullable && Nullable.GetUnderlyingType(propType) is not null);

        if (prop.GetCustomAttribute<DescriptionAttribute>() is { Description: { } desc })
            p["description"] = desc;

        if (prop.GetCustomAttribute<DefaultValueAttribute>() is { Value: { } defaultValue })
            p["default"] = ToJsonValue(defaultValue);

        if (prop.GetCustomAttribute<RangeAttribute>() is { } range)
        {
            if (range.Minimum is IConvertible minConv)
                p["minimum"] = JsonValue.Create(Convert.ToDouble(minConv));
            if (range.Maximum is IConvertible maxConv)
                p["maximum"] = JsonValue.Create(Convert.ToDouble(maxConv));
        }

        return p;
    }

    private static void SetType(JsonObject p, Type innerType, bool allowNull)
    {
        string jsonType;
        if (innerType == typeof(string)) jsonType = "string";
        else if (innerType == typeof(bool)) jsonType = "boolean";
        else if (innerType == typeof(int) || innerType == typeof(long) || innerType == typeof(short)
                 || innerType == typeof(uint) || innerType == typeof(ulong) || innerType == typeof(ushort)
                 || innerType == typeof(byte) || innerType == typeof(sbyte)) jsonType = "integer";
        else if (innerType == typeof(double) || innerType == typeof(float) || innerType == typeof(decimal))
            jsonType = "number";
        else if (innerType.IsEnum)
        {
            p["type"] = "string";
            var names = Enum.GetNames(innerType);
            p["enum"] = new JsonArray(names.Select(n => JsonValue.Create(n)!).ToArray<JsonNode>());
            return;
        }
        else if (innerType.IsArray
                 || (innerType.IsGenericType && typeof(IEnumerable).IsAssignableFrom(innerType)))
        {
            p["type"] = "array";
            return;
        }
        else
        {
            // Nested POCO — recurse into its schema.
            var nested = Build(innerType);
            foreach (var kv in nested.ToList())
            {
                if (kv.Key == "$schema") continue;
                p[kv.Key] = kv.Value?.DeepClone();
            }
            return;
        }

        p["type"] = allowNull
            ? new JsonArray(JsonValue.Create(jsonType)!, JsonValue.Create("null")!)
            : JsonValue.Create(jsonType);
    }

    private static JsonNode? ToJsonValue(object value) => value switch
    {
        string s => JsonValue.Create(s),
        bool b => JsonValue.Create(b),
        int i => JsonValue.Create(i),
        long l => JsonValue.Create(l),
        double d => JsonValue.Create(d),
        float f => JsonValue.Create(f),
        decimal m => JsonValue.Create(m),
        Enum e => JsonValue.Create(e.ToString()),
        _ => JsonValue.Create(value.ToString() ?? ""),
    };

    private static string Camel(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s[1..];
}
