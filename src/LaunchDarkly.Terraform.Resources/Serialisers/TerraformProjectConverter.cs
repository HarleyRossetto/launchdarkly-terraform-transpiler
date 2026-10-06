using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace LaunchDarkly.Terraform.Resources.Serialisers;

public sealed class TerraformProjectConverter : JsonConverter<LaunchDarklyProject>
{
    public override LaunchDarklyProject? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotSupportedException("Reading Terraform project configurations is not supported.");

    public override void Write(Utf8JsonWriter writer, LaunchDarklyProject value, JsonSerializerOptions options)
    {
        var plainOptions = new JsonSerializerOptions(options);
        plainOptions.Converters.Clear();
        foreach (var converter in options.Converters)
        {
            if (converter is not TerraformProjectConverter)
            {
                plainOptions.Converters.Add(converter);
            }
        }

        var projectBody = JsonSerializer.SerializeToNode(value, plainOptions)?.AsObject()
            ?? throw new JsonException("Could not serialize the LaunchDarkly project.");
        // The project schema requires environments but does not support segment approvals.
        // Keep the seed map and delegate ongoing environment management to standalone resources.
        if (projectBody["environments"] is JsonObject projectEnvironments)
        {
            foreach (var environmentNode in projectEnvironments.Select(environment => environment.Value))
            {
                if (environmentNode is JsonObject environmentBody)
                {
                    environmentBody.Remove("segment_approval_settings");
                }
            }
        }

        projectBody["lifecycle"] = new JsonObject
        {
            ["ignore_changes"] = new JsonArray(JsonValue.Create("environments"))
        };

        var resources = new JsonObject
        {
            [LaunchDarklyProject.ResourceType] = new JsonObject
            {
                [value.Key] = projectBody
            }
        };

        // We have to be a bit hacky here, we want everything to be configured on the project object, however the underlying 
        // provider does not support segment approval settings currently.
        // Here we extract a copy of the environment and create additional resources for each. This is a somewhat painful because we need
        // to ensure that the project is created first, then import the environments into the terraform state before we can apply them, they cannot 
        // be applied at the same time.
        // https://github.com/launchdarkly/terraform-provider-launchdarkly/pull/464 -> Scope mentions a follow up is required to align 
        // inline schema with the separate environment schema. It has not been compelted yet.
        var environmentResources = new JsonObject();
        foreach (var (environmentKey, environment) in value.Environments)
        {
            var environmentBody = JsonSerializer.SerializeToNode(environment, plainOptions)?.AsObject()
                ?? throw new JsonException($"Could not serialize environment '{environmentKey}'.");
            environmentBody["key"] = environmentKey;
            environmentBody["project_key"] = $"${{{LaunchDarklyProject.ResourceType}.{value.Key}.key}}";
            environmentResources[$"{value.Key}-{environmentKey}"] = environmentBody;
        }

        if (environmentResources.Count > 0)
        {
            resources["launchdarkly_environment"] = environmentResources;
        }

        new JsonObject { ["resource"] = resources }.WriteTo(writer);
    }
}
