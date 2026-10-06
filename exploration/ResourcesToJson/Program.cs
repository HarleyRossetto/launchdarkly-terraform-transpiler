using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

// var launchDarklyProvider = new LaunchDarklyProvider
// {
//     AccessToken = Environment.GetEnvironmentVariable("LAUNCHDARKLY_ACCESS_TOKEN") ?? throw new InvalidOperationException("Missing LAUNCHDARKLY_ACCESS_TOKEN environment variable"),
//     MaxConcurrency = 1,
//     ArchiveFlagsOnDestroy = true
// };

var project = new LaunchDarklyProject
{
    Key = "my-project",
    Name = "My Project",
    Environments = new()
    {
        ["dev"] = new()
        {
            Key = "dev",
            Name = "Development",
            Color = "0000FF",
        },
        ["prod"] = new()
        {
            Key = "prod",
            Name = "Production",
            Color = "FF0000",
            ApprovalSettings = new()
            {
                AutoApplyApprovedChanges = false,
                MinimumNumberOfApprovals = 2,
                RequiredApprovalTags = ["compliance", "security"]
            },
            SegmentApprovalSettings = new()
            {
                AutoApplyApprovedChanges = false,
                MinimumNumberOfApprovals = 2,
                RequiredApprovalTags = ["compliance", "security", "hxr"]
            }
        }
    }
};



var options = new JsonSerializerOptions
{
    WriteIndented = true,
    Converters = { new TerraformProjectConverter() }
};

var jsonString = JsonSerializer.Serialize(project, options);

File.WriteAllText($"terraform/{project.Key}.tf.json", jsonString);

#pragma warning disable CA1050

public record LaunchDarklyProviderSource
{
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    [JsonPropertyName("version")]
    public required float Version { get; init; }
}

public record LaunchDarklyProvider
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("max_concurrency")]
    public required int MaxConcurrency { get; init; }

    [JsonPropertyName("archive_flags_on_destroy")]
    public required bool ArchiveFlagsOnDestroy { get; init; }
}

public record LaunchDarklyProject : ITerraformResource
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
    public bool? RequireViewAssociationForNewFlags { get; init; }

    /// <summary>
    /// Whether new segments created in this project must be associated with at least one view.
    /// </summary>
    [JsonPropertyName("require_view_association_for_new_segments")]
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

public record LaunchDarklyEnvironment
{
    /// <summary>
    /// The project-unique key for the environment. Must equal the map key. It defaults to the map key when omitted. Changing it (or the map key) replaces the environment.
    /// </summary>
    [JsonPropertyName("key")]
    public string? Key { get; init; } = null;

    /// <summary>
    /// The name of the environment.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// The color swatch as an RGB hex value with no leading #. For example: 000000
    /// </summary>
    [JsonPropertyName("color")]
    public required string Color { get; init; }

    [JsonPropertyName("approval_settings")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ApprovalSettings? ApprovalSettings { get; init; } = null;


    [JsonPropertyName("segment_approval_settings")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ApprovalSettings? SegmentApprovalSettings { get; init; } = null;

    /// <summary>
    /// Set to true if this environment requires confirmation for flag and segment changes. This field defaults to false when not set.
    /// </summary>
    [JsonPropertyName("confirm_changes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ConfirmChanges { get; init; } = null;

    /// <summary>
    /// Denotes whether the environment is critical.
    /// </summary>
    [JsonPropertyName("critical")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Critical { get; init; } = null;

    /// <summary>
    /// Set to true to enable data export for every flag created in this environment after you configure this argument. 
    /// This field defaults to false when not set. To learn more, read <see href="https://launchdarkly.com/docs/integrations/data-export">Data Export</see>.
    /// </summary>
    [JsonPropertyName("default_track_events")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? DefaultTrackEvents { get; init; } = null;

    /// <summary>
    /// The TTL for the environment. This must be between 0 and 60 minutes. 
    /// The TTL setting only applies to environments using the PHP SDK. 
    /// This field defaults to 0 when not set. To learn more, read <see href="https://launchdarkly.com/docs/home/account/environment#ttl-settings">TTL settings</see>.
    /// </summary>
    [JsonPropertyName("default_ttl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DefaultTtl { get; init; } = null;

    /// <summary>
    /// Set to true if this environment requires comments for flag and segment changes. This field defaults to false when not set.
    /// </summary>
    [JsonPropertyName("require_comments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? RequireComments { get; init; } = null;

    /// <summary>
    /// Set to true to ensure a user of the client-side SDK cannot impersonate another user. This field defaults to false when not set.
    /// </summary>
    [JsonPropertyName("secure_mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? SecureMode { get; init; } = null;

    /// <summary>
    /// Tags associated with your resource.
    /// </summary>
    [JsonPropertyName("tags")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Tags { get; init; } = null;
}

public record ApprovalSettings
{
    /// <summary>
    /// Automatically apply changes that have been approved by all reviewers. This field is only applicable for approval service kinds other than <c>launchdarkly</c>.
    /// </summary>
    [JsonPropertyName("auto_apply_approved_changes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? AutoApplyApprovedChanges { get; init; }

    /// <summary>
    /// Set to <see langword="true"/> if changes can be applied as long as the minimum number of approvals is met, regardless of whether any reviewers have declined a request. Defaults to <see langword="true"/>.
    /// </summary>
    [JsonPropertyName("can_apply_declined_changes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? CanaApplyDeclinedChanges { get; init; }

    /// <summary>
    /// Set to <see langword="true"/> if requesters can approve or decline their own request. They may always comment. Defaults to <see langword="false"/>.
    /// </summary>
    [JsonPropertyName("can_review_own_request")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? CanReviewOwnRequest { get; init; }

    /// <summary>
    /// The number of approvals required before an approval request can be applied. Must be between 1 and 5. Defaults to 1.
    /// </summary>
    [JsonPropertyName("min_num_approvals")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? MinimumNumberOfApprovals { get; init; }

    /// <summary>
    /// Set to <see langword="true"/> for changes to flags in this environment to require approval. Set either this property or <see cref="RequiredApprovalTags"/>, but not both. Defaults to <see langword="false"/>.
    /// </summary>
    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Required { get; init; }

    /// <summary>
    /// Tags for flags that require approval. Set either this property or <see cref="Required"/>, but not both.
    /// </summary>
    [JsonPropertyName("required_approval_tags")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? RequiredApprovalTags { get; init; } = null;

    /// <summary>
    /// Configuration for the associated approval service. For <c>servicenow</c>, supported keys are <c>template</c> (the ServiceNow Standard Change Request Template sys_id) and <c>detail_column</c> (the ServiceNow column used for detailed approval request information, commonly <c>justification</c>).
    /// </summary>
    [JsonPropertyName("service_config")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? ServiceConfiguration { get; init; } = null;

    /// <summary>
    /// The approval service kind. Valid values are <c>servicenow</c> and <c>launchdarkly</c>. Non-LaunchDarkly services must already be configured in LaunchDarkly before applying.
    /// </summary>
    [JsonPropertyName("service_kind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ServiceKind { get; init; } = "launchdarkly";
}

public record ClientSideAvailability
{
    /// <summary>
    /// Set to true to allow client-side SDKs to evaluate this feature flag. Defaults to false.
    /// </summary>
    [JsonPropertyName("using_environment_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ClientSide { get; init; } = null;

    /// <summary>
    /// Set to true to allow client-side SDKs to evaluate this feature flag using the environment's mobile key. Defaults to false.
    /// </summary>
    [JsonPropertyName("using_mobile_key")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? MobileKey { get; init; } = null;
}


// Implemented (explicitly) by any type that should serialise as a Terraform
// resource block: { "resource": { "<ResourceType>": { "<ResourceName>": {...} } } }.
// ResourceType is a static abstract member so it's a compile-time-checked
// piece of the contract rather than a reflected attribute string.
public interface ITerraformResource
{
    static abstract string ResourceType { get; }
    string ResourceName { get; }
}

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

// Wraps T's normal JSON output in Terraform's resource block shape:
// { "resource": { "<resource_type>": { "<resource_name>": {...T...} } } }
public sealed class TerraformResourceConverter<T> : JsonConverter<T> where T : ITerraformResource
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
