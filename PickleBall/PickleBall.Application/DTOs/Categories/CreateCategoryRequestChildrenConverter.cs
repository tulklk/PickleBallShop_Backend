using System.Text.Json;
using System.Text.Json.Serialization;

namespace PickleBall.Application.DTOs.Categories;

/// <summary>
/// Allows Children to accept either an array of strings (child names) or an array of CreateCategoryRequest objects.
/// </summary>
public class CreateCategoryRequestChildrenConverter : JsonConverter<List<CreateCategoryRequest>?>
{
    public override List<CreateCategoryRequest>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected start of array for Children.");

        var list = new List<CreateCategoryRequest>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                return list;

            if (reader.TokenType == JsonTokenType.String)
            {
                var name = reader.GetString();
                list.Add(new CreateCategoryRequest { Name = name ?? string.Empty, Description = null });
                continue;
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                var child = JsonSerializer.Deserialize<CreateCategoryRequest>(ref reader, options);
                if (child != null)
                    list.Add(child);
                continue;
            }

            throw new JsonException($"Unexpected token type {reader.TokenType} in Children array.");
        }

        throw new JsonException("Unexpected end of array for Children.");
    }

    public override void Write(Utf8JsonWriter writer, List<CreateCategoryRequest>? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        foreach (var item in value)
        {
            JsonSerializer.Serialize(writer, item, options);
        }
        writer.WriteEndArray();
    }
}
