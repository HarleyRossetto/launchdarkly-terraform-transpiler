namespace LaunchDarkly.Terraform.Resources;

// Non-generic contract for storing heterogeneous Terraform resources together.
public interface ITerraformResource
{
    string ResourceName { get; }
}

// Typed contract for resource metadata used by generic serialization code.
public interface ITerraformResource<TSelf> : ITerraformResource
    where TSelf : ITerraformResource<TSelf>
{
    static abstract string ResourceType { get; }
}
