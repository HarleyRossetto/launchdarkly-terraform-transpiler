using System.Text.Json.Serialization;

namespace LaunchDarkly.Terraform.Resources;

public record LaunchDarklyProject : ITerraformResource<LaunchDarklyProject>
{
    /// <summary>
    /// The project's unique key. A change in this field forces the destruction of the existing resource and the creation of a new one.
    /// </summary>
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    /// <summary>
    /// The project's name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Tags associated with your resource.
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string>? Tags { get; init; }

    /// <summary>
    /// Map of environments that belong to the project, keyed by environment key. 
    /// This is the complete, authoritative set of the project's environments: 
    /// any environment not present in the map is deleted on apply. 
    /// Reordering, adding, or removing one environment does not affect the others. 
    /// A project must have at least one environment.
    /// </summary>
    [JsonPropertyName("environments")]
    public required Dictionary<string, LaunchDarklyEnvironment> Environments { get; init; }

    /// <summary>
    /// Whether new flags created in this project must be associated with at least one view.
    /// </summary>
    [JsonPropertyName("require_view_association_for_new_flags")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? RequireViewAssociationForNewFlags { get; init; }

    /// <summary>
    /// Whether new segments created in this project must be associated with at least one view.
    /// </summary>
    [JsonPropertyName("require_view_association_for_new_segments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? RequireViewAssociationForNewSegments { get; init; }

    /// <summary>
    /// Which client-side SDKs can use new flags by default. 
    /// </summary>
    [JsonPropertyName("default_client_side_availability")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ClientSideAvailability? ClientSideAvailability { get; init; } = null;

    public static string ResourceType => "launchdarkly_project";

    [JsonIgnore]
    string ITerraformResource.ResourceName => Key;
}
