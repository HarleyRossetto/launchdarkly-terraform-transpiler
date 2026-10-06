using System.Text.Json;
using System.Text.Json.Serialization;

namespace LaunchDarkly.Terraform.Resources.Serialisers;


// Wraps T's normal JSON output in Terraform's resource block shape:
// { "resource": { "<resource_type>": { "<resource_name>": {...T...} } } }
public sealed class TerraformResourceConverter<T> : JsonConverter<T> where T : ITerraformResource<T>
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotSupportedException($"Reading Terraform-wrapped {typeof(T).Name} is not supported.");

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        // Serialise the value normally, but with this converter removed so we
        // don't recurse into Write() forever.
        var plainOptions = new JsonSerializerOptions(options);
        plainOptions.Converters.Clear();
        foreach (var converter in options.Converters)
        {
            if (converter is not TerraformResourceConverter<T>)
                plainOptions.Converters.Add(converter);
        }

        var body = JsonSerializer.SerializeToNode(value, plainOptions);

        writer.WriteStartObject();
        writer.WritePropertyName("resource");
        writer.WriteStartObject();
        writer.WritePropertyName(T.ResourceType);
        writer.WriteStartObject();
        writer.WritePropertyName(value.ResourceName);
        body!.WriteTo(writer);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
