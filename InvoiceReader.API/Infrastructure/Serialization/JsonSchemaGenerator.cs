using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace InvoiceReader.API.Infrastructure.Serialization;

public static class JsonSchemaGenerator
{
    public static Dictionary<string, object> GenerateSchema(Type type)
    {
        if (type == typeof(string) || type.IsPrimitive || type == typeof(decimal))
        {
            return new Dictionary<string, object>
            {
                ["type"] = MapType(type),
                ["additionalProperties"] = false,
            };
        }

        if (IsCollectionType(type, out Type? itemType))
        {
            return new Dictionary<string, object>
            {
                ["type"] = "array",
                ["additionalProperties"] = false,
                ["items"] = GenerateSchema(itemType)
            };
        }

        var schema = new Dictionary<string, object>
        {
            ["$schema"] = "http://json-schema.org/draft-07/schema#",
            ["title"] = type.Name,
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>(),
            ["additionalProperties"] = false,
            ["required"] = new List<string>()
        };

        var properties = (Dictionary<string, object>)schema["properties"];
        var required = (List<string>)schema["required"];

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (IsCollectionType(type, out _)) break;

            var propType = prop.PropertyType;
            var propSchema = GenerateSchema(propType);

            var descAttr = prop.GetCustomAttribute<DescriptionAttribute>();
            if (descAttr != null)
            {
                propSchema["description"] = descAttr.Description;
            }

            properties[prop.Name] = propSchema;
            required.Add(prop.Name);
        }

        return schema;
    }

    private static string MapType(Type type)
    {
        if (type == typeof(string)) return "string";
        if (type == typeof(int) || type == typeof(long) || type == typeof(short)) return "integer";
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return "number";
        if (type == typeof(bool)) return "boolean";
        if (type.IsArray || type.IsGenericType && typeof(IEnumerable<>).IsAssignableFrom(type.GetGenericTypeDefinition()))
            return "array";
        if (type.IsClass) return "object";
        return "string";
    }

    private static bool IsCollectionType(Type type, out Type? itemType)
    {
        itemType = null;

        if (type.IsArray)
        {
            itemType = type.GetElementType();
            return true;
        }

        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();

            if (genericDef == typeof(List<>) || genericDef == typeof(IEnumerable<>))
            {
                itemType = type.GetGenericArguments()[0];
                return true;
            }
        }

        var iface = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (iface != null)
        {
            itemType = iface.GetGenericArguments()[0];
            return true;
        }

        return false;
    }
}
