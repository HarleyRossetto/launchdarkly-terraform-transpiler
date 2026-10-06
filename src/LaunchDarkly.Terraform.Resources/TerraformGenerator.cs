using System.Text.Json;
using System.Text.Json.Serialization;
using LaunchDarkly.Terraform.Resources.Serialisers;

namespace LaunchDarkly.Terraform.Resources;

public class TerraformGenerator
{
    private readonly JsonSerializerOptions _options;

    public TerraformGenerator()
    {
        _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
                new TerraformProjectConverter(),
                new TerraformResourceConverter<RelayProxyConfiguration>(),
                new TerraformResourceConverter<LaunchDarklyTeam>(),
                new TerraformResourceConverter<LaunchDarklyCustomRole>()
            }
        };
    }
    public string GenerateTerraformFromResource<T>(T resource)
    {
        if (resource is null)
        {
            throw new ArgumentNullException(nameof(resource));
        }

        if (resource is LaunchDarklyProject project)
        {
            return JsonSerializer.Serialize(project, _options);
        }
        else if (resource is LaunchDarklyEnvironment environment)
        {
            return JsonSerializer.Serialize(environment, _options);
        }
        else if (resource is RelayProxyConfiguration relayProxyConfiguration)
        {
            return JsonSerializer.Serialize(relayProxyConfiguration, _options);
        }
        else if (resource is LaunchDarklyTeam team)
        {
            return JsonSerializer.Serialize(team, _options);
        }
        else if (resource is LaunchDarklyCustomRole customRole)
        {
            return JsonSerializer.Serialize(customRole, _options);
        }
        else
        {
            throw new NotSupportedException($"Resource type {typeof(T).Name} is not supported for Terraform generation.");
        }
    }
}
