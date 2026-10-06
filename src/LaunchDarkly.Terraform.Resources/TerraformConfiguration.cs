using System.Text.Json.Serialization;

namespace LaunchDarkly.Terraform.Resources;

public sealed record TerraformConfiguration
{
    public static TerraformConfiguration Default { get; } = new()
    {
        Terraform = new()
        {
            RequiredProviders = new Dictionary<string, TerraformRequiredProvider>
            {
                ["launchdarkly"] = new()
                {
                    Source = "launchdarkly/launchdarkly",
                    Version = "~> 3.0"
                }
            }
        },
        Provider = new()
        {
            LaunchDarkly = new()
            {
                MaxConcurrency = 1,
                ArchiveFlagsOnDestroy = false
            }
        }
    };

    [JsonPropertyName("terraform")]
    public required TerraformSettings Terraform { get; init; }

    [JsonPropertyName("provider")]
    public required TerraformProviderSettings Provider { get; init; }
}

public sealed record TerraformSettings
{
    [JsonPropertyName("required_providers")]
    public required Dictionary<string, TerraformRequiredProvider> RequiredProviders { get; init; }
}

public sealed record TerraformRequiredProvider
{
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }
}

public sealed record TerraformProviderSettings
{
    [JsonPropertyName("launchdarkly")]
    public required LaunchDarklyProviderSettings LaunchDarkly { get; init; }
}

public sealed record LaunchDarklyProviderSettings
{
    [JsonPropertyName("max_concurrency")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxConcurrency { get; init; }

    [JsonPropertyName("archive_flags_on_destroy")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ArchiveFlagsOnDestroy { get; init; }
}
