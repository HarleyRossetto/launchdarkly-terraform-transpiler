using System.Text.Json.Serialization;

namespace LaunchDarkly.Terraform.Resources;

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
