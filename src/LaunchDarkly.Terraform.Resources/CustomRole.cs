using System.Text.Json.Serialization;

namespace LaunchDarkly.Terraform.Resources;

/// <summary>
/// A LaunchDarkly custom role defining a reusable set of permissions.
/// </summary>
public record LaunchDarklyCustomRole : ITerraformResource<LaunchDarklyCustomRole>
{
    /// <summary>
    /// A unique key that references the custom role in your code. A change in this field forces the destruction of the existing resource and the creation of a new one.
    /// </summary>
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    /// <summary>
    /// A name for the custom role. This must be unique within your organization.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Description of the custom role.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public required string Description { get; init; }

    /// <summary>
    /// The base permission level - either <c>reader</c> or <c>no_access</c>. While newer API versions default to <c>no_access</c>, this field defaults to <c>reader</c> in keeping with previous API versions.
    /// </summary>
    [JsonPropertyName("base_permissions")]
    public Permissions? BasePermissions { get; init; } = Permissions.No_access;

    /// <summary>
    /// An array of the policy statements that define the permissions for the custom role. This field accepts role attributes.
    /// To use role attributes, use the syntax <c>$${roleAttribute/&lt;YOUR_ROLE_ATTRIBUTE&gt;}</c> in lieu of your usual resource keys.
    /// Mutually exclusive with <see cref="PolicyStatementsJson"/>.
    /// </summary>
    [JsonPropertyName("policy_statements")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public required List<Policy>? PolicyStatements { get; init; }

    /// <summary>
    /// Policy statements expressed as a single JSON document, an array of statement objects with the same keys as the
    /// <see cref="PolicyStatements"/> attribute (<c>resources</c>, <c>not_resources</c>, <c>actions</c>, <c>not_actions</c>, <c>effect</c>).
    /// Mutually exclusive with <see cref="PolicyStatements"/>. Use this form when reading the policy from a file or templating it dynamically,
    /// for example with <c>jsonencode(...)</c> or <c>file("policy.json")</c>. To use role attributes, escape the <c>$</c> as
    /// <c>$${roleAttribute/&lt;YOUR_ROLE_ATTRIBUTE&gt;}</c> inside HCL strings.
    /// </summary>
    [JsonPropertyName("policy_statements_json")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PolicyStatementsJson { get; init; }

    public static string ResourceType => "launchdarkly_custom_role";

    [JsonIgnore]
    string ITerraformResource.ResourceName => Key;
}

/// <summary>
/// The base permission level for a custom role.
/// </summary>
public enum Permissions
{
    Reader,
    No_access,
    Writer,
    Admin
}
