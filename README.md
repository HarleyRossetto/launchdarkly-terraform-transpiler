# LaunchDarkly Resource Management tools

## CLI Terraform workflow

The CLI supports `generate`, `init`, `plan`, `apply`, `import`, `refresh-plan`, `refresh-apply`, `show`, and a scoped Terraform pass-through command. The output directory defaults to `./terraform`; each resource scope is written to its own namespace-derived subdirectory, such as `Tenants/TenantA`. Each generated scope also receives a `providers.tf.json` file with the centrally managed Terraform required-provider and LaunchDarkly provider defaults.

```sh
dotnet run --project src/Cli -- generate --output-dir ./terraform
dotnet run --project src/Cli -- init --output-dir ./terraform --scope Tenants/TenantA
dotnet run --project src/Cli -- plan --output-dir ./terraform --scope Tenants/TenantA
dotnet run --project src/Cli -- apply --output-dir ./terraform --scope Tenants/TenantA
dotnet run --project src/Cli -- import --output-dir ./terraform --scope Tenants/TenantA
dotnet run --project src/Cli -- refresh-plan --output-dir ./terraform --scope Tenants/TenantA
dotnet run --project src/Cli -- refresh-apply --output-dir ./terraform --scope Tenants/TenantA
dotnet run --project src/Cli -- show --output-dir ./terraform --scope Tenants/TenantA
dotnet run --project src/Cli -- show --output-dir ./terraform --scope Tenants/TenantA --resource launchdarkly_project.my-project
dotnet run --project src/Cli -- terraform --output-dir ./terraform --scope Tenants/TenantA -- state list
```

`init` regenerates the Terraform JSON and runs `terraform init -input=false` in every selected scope directory. `plan` also regenerates the JSON, initializes each scope, then saves its plan as `terraform.plan`. A scope whose initialization fails is skipped for planning, but other selected scopes are still processed. Terraform initialization honors the scope's `.terraform.lock.hcl`; the CLI does not request provider upgrades.

An ordinary `plan` refreshes remote values for comparison but does not persist those refreshed values into Terraform state. Use `refresh-plan` to regenerate configuration, initialize Terraform, and create/display a saved `terraform plan -refresh-only` plan without changing state. After reviewing it, run `refresh-apply` to apply that exact refresh-only plan and persist the observed remote values to state; it does not change LaunchDarkly infrastructure. The refresh plan is separate from `terraform.plan`, and a matching `terraform.refresh.json` file binds the saved plan to its scope and checksum. `refresh-apply` rejects missing or mismatched artifacts and does not regenerate configuration, initialize Terraform, or create a new plan. Omit `--scope` to process all configured scopes for either command.

Use `import` to bring already-existing LaunchDarkly resources into Terraform state without creating or changing any infrastructure. It regenerates configuration, initializes each selected scope, then checks Terraform state for the address of every configured `launchdarkly_project`, `launchdarkly_environment`, `launchdarkly_team`, and `launchdarkly_custom_role` resource. For each address missing from state, it runs `terraform import` using an ID derived from configuration (the project key, `<projectKey>/<environmentKey>`, the team key, or the custom role key). If the provider reports that no remote object exists with that ID, the CLI treats the resource as genuinely new, leaves it out of state, and lets a later `plan`/`apply` create it; any other import failure is reported and the command exits nonzero. `launchdarkly_relay_proxy_configuration` resources are always skipped and listed for manual handling, because their Terraform import ID is a LaunchDarkly-generated identifier that cannot be derived from the resource's configured name; find the ID with the LaunchDarkly API or an existing `terraform state show` on another environment, then run `terraform import` for it directly. `import` is safe to run repeatedly and only mutates local Terraform state. Omit `--scope` to process all configured scopes; it continues after a scope fails and returns a nonzero exit code if any scope fails.

`show` without `--resource` runs `terraform show` for each selected scope. Add `--resource <ADDRESS>` to run `terraform state show <ADDRESS>` for one resource; resource mode requires `--scope`. Omit `--scope` for scope-wide show across all configured scopes.

Use `terraform` to run other Terraform subcommands that the CLI does not expose directly. This command requires exactly one `--scope`, regenerates that scope's configuration, runs `terraform init -input=false`, then invokes Terraform with the arguments after `--` in the scope directory. For example:

```sh
dotnet run --project src/Cli -- terraform --scope Tenants/TenantA -- state list
dotnet run --project src/Cli -- terraform --scope Tenants/TenantA -- output 'launchdarkly_project.my-project.key'
```

The command forwards arguments directly without a shell, streams Terraform output to the terminal, and returns Terraform's exit code. It does not run if generation or initialization fails. Since pass-through operations can modify state or configuration, inspect the subcommand and arguments carefully before running.

Review the saved plan before running `apply`; `apply` consumes it without regenerating configuration or reinitializing Terraform, preserving the initialized context used to create the plan. Omit `--scope` to process all configured scopes for commands that support multi-scope operation. Multi-scope Terraform commands continue after a scope fails and return a nonzero exit code if any scope fails. If your backend requires configuration, provide it in the scope directory before running `init`; `-input=false` makes missing interactive values fail instead of prompting.

### CI plan/apply jobs and project bootstrap

`plan` detects projects that Terraform will create and standalone environment resources missing from state. It writes the reviewed full plan to `terraform.plan` and companion `terraform.bootstrap.json` metadata. The plan job remains read-only: it does not create projects or alter Terraform state. Transfer the complete initialized scope directory from the plan job to the apply job, including generated `.tf.json` files, `terraform.plan`, `terraform.bootstrap.json`, `.terraform.lock.hcl`, and the initialized `.terraform` directory. Use the same backend, workspace, Terraform version, and compatible runner environment in both jobs; protect the transferred artifacts because Terraform plans and initialized backend metadata may contain sensitive information.

After the apply job is approved, `apply` creates only the missing projects identified by the reviewed plan, imports their configured standalone environments (and any other configured environments absent from state), then creates a fresh full plan and applies it. The fresh plan is necessary because project creation and imports change state, making the original full plan stale. If no bootstrap or imports are needed, `apply` uses the original reviewed plan. The apply job's approval is the authorization gate for both bootstrap and the refreshed apply; the CLI does not add an extra interactive approval prompt.

State refresh can also run as a two-job CI workflow. In the first job, run `refresh-plan`; review its output and transfer the entire selected scope directory to the approval-gated job, including generated `.tf.json` files, `terraform.refresh.plan`, `terraform.refresh.json`, `.terraform.lock.hcl`, and the initialized `.terraform` directory. For a local backend, transfer the corresponding Terraform state file as well; for a remote backend, configure both jobs to use the same backend and workspace. After approval, run `refresh-apply` in the transferred directory. It applies the saved refresh-only plan without prompting, so the CI environment's job approval is the authorization gate. Do not regenerate, re-plan, or change the backend/workspace between the two jobs. Protect transferred artifacts because Terraform plans, state, and initialized backend metadata may contain sensitive information. Both commands accept `--scope`; without it they operate on all configured scopes, continuing across scopes and returning a nonzero status if any scope fails.

Bootstrap is retryable: apply checks state before creating projects or importing environments, so already completed work is skipped. If a project already exists remotely but is not in Terraform state, the CLI cannot reliably distinguish it from a new project before apply; the project create will fail rather than overwrite it. Run `import` to bring the pre-existing project (and any other pre-existing team, custom role, or environment resources) into state, then rerun `plan` and apply. A missing project in state is never imported automatically by `plan`/`apply`.

### Provider configuration defaults and overrides

The central default is `TerraformConfiguration.Default` in the resource library. It specifies the LaunchDarkly provider source/version, `max_concurrency = 1`, and `archive_flags_on_destroy = false`. The access token is intentionally omitted; set `LAUNCHDARKLY_ACCESS_TOKEN` in the process environment instead.

A scope holder can opt into a complete provider-configuration override by declaring a static `TerraformConfiguration` field. It is discovered alongside that holder's resources and replaces the central default for that scope. Since the configuration is a record, use `with` expressions to derive an adjusted configuration:

```csharp
public static readonly TerraformConfiguration TerraformConfig =
    TerraformConfiguration.Default with
    {
        Provider = TerraformConfiguration.Default.Provider with
        {
            LaunchDarkly = TerraformConfiguration.Default.Provider.LaunchDarkly with
            {
                MaxConcurrency = 2
            }
        }
    };
```

This emits the selected configuration as `providers.tf.json` in that scope's output directory. An override replaces the full configuration object; starting from `TerraformConfiguration.Default with { ... }` preserves the other default fields while changing selected values.

## Generated Terraform resources

The `exploration/ResourcesToJson` generator lets environments be declared once in C# while emitting both the project and standalone environment resources in one `.tf.json` file. The project resource retains its required `environments` map, but the generator adds `lifecycle.ignore_changes = ["environments"]` so Terraform does not manage those environments through both resource types. The standalone `launchdarkly_environment` resources own ongoing environment changes, including `segment_approval_settings`, which is not supported in the project's nested environment schema.

The CLI's `plan` and `apply` commands automate this sequence for configured resources. For manual recovery or use outside the CLI, bootstrap the project and its initial environments first, then import those environments into the generated standalone resource addresses:

```sh
terraform apply -target='launchdarkly_project.my-project'
terraform import 'launchdarkly_environment.my-project-dev' 'my-project/dev'
terraform import 'launchdarkly_environment.my-project-prod' 'my-project/prod'
terraform plan
```

For an existing project, import the project and each standalone environment into their corresponding Terraform addresses before applying. Keep `ignore_changes` on the project for as long as environments are managed by standalone resources; removing it lets the project's authoritative environment map compete with those resources and may cause environment changes or deletion.
