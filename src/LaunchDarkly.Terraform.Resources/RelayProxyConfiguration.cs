using System.Text.Json.Serialization;

namespace LaunchDarkly.Terraform.Resources;

public record RelayProxyConfiguration : ITerraformResource<RelayProxyConfiguration>
{

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("policy")]
    public required List<Policy> Policy { get; init; } = [];

    public static string ResourceType => "launchdarkly_relay_proxy_configuration";

    [JsonIgnore]
    string ITerraformResource.ResourceName => Name;
}
