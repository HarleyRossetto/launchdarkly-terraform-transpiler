using System.Text.Json.Serialization;

namespace LaunchDarkly.Terraform.Resources;

/// <summary>
/// A policy statement defining which actions are allowed or denied for specified resources.
/// </summary>
public record Policy
{
    /// <summary>
    /// Either <c>allow</c> or <c>deny</c>. Defines whether the statement allows or denies access to the named resources and actions.
    /// </summary>
    [JsonPropertyName("effect")]
    public required Effect Effect { get; init; }

    /// <summary>
    /// The action specifiers to which the statement applies. Either this property or <see cref="NotActions"/> must be specified.
    /// For available action specifiers, see the LaunchDarkly Actions reference.
    /// </summary>
    [JsonPropertyName("actions")]
    public List<string>? Actions { get; init; }

    /// <summary>
    /// The action specifiers to which the statement does not apply.
    /// </summary>
    [JsonPropertyName("not_actions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? NotActions { get; init; }

    /// <summary>
    /// The resource specifiers to which the statement applies.
    /// </summary>
    [JsonPropertyName("resources")]
    public List<string>? Resources { get; init; }

    /// <summary>
    /// The resource specifiers to which the statement does not apply.
    /// </summary>
    [JsonPropertyName("not_resources")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? NotResources { get; init; }

    public static implicit operator Policy((Effect effect, List<string> actions, List<string> resources) tuple)
    {
        return new Policy
        {
            Effect = tuple.effect,
            Actions = tuple.actions,
            Resources = tuple.resources
        };
    }
}

/// <summary>
/// The effect a policy statement has on access.
/// </summary>
public enum Effect
{
    Allow,
    Deny
}
