using System.Text.Json.Serialization;

namespace LaunchDarkly.Terraform.Resources;

/// <summary>
/// A LaunchDarkly team and its associated members, roles, and role attributes.
/// </summary>
public record LaunchDarklyTeam : ITerraformResource<LaunchDarklyTeam>
{

    /// <summary>
    /// The team's key. A change in this field forces the destruction of the existing resource and the creation of a new one.
    /// </summary>
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    /// <summary>
    /// A human-friendly name for the team.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// List of custom role keys granted to the team. The referenced custom roles must already exist in LaunchDarkly;
    /// if they do not, the provider may behave unexpectedly.
    /// </summary>
    [JsonPropertyName("custom_role_keys")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? CustomRoleKeys { get; init; } = [];

    /// <summary>
    /// The team description.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    /// <summary>
    /// List of member IDs for users who maintain the team.
    /// </summary>
    [JsonPropertyName("maintainers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? MaintainerIDs { get; init; }

    /// <summary>
    /// List of member IDs for users who belong to the team.
    /// </summary>
    [JsonPropertyName("member_ids")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? MemberIDs { get; init; }


    /// <summary>
    /// Role attributes keyed by attribute key, with each value containing the resource keys assigned to that attribute.
    /// </summary>
    [JsonPropertyName("role_attributes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string[]>? RoleAttributes { get; init; }

    public static string ResourceType => "launchdarkly_team";

    [JsonIgnore]
    string ITerraformResource.ResourceName => Key;
}
