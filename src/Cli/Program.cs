using System.CommandLine;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using LaunchDarkly.Terraform.Resources;
using LaunchDarkly.Terraform.Resources.Loader;

internal static class Program
{
    private const string PlanFileName = "terraform.plan";
    private const string RefreshPlanFileName = "terraform.refresh.plan";
    private const string RefreshPlanMetadataFileName = "terraform.refresh.json";
    private const string BootstrapPlanFileName = "terraform.bootstrap.plan";
    private const string BootstrapMetadataFileName = "terraform.bootstrap.json";
    private const string NoTerraformStateMessage = "No state file was found!";
    private const string CannotImportNonExistentRemoteObjectMessage = "Cannot import non-existent remote object";
    private const string ResourceNamespacePrefix = "Cli.Resources.";

    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Generate and manage scoped LaunchDarkly Terraform resources.");

        rootCommand.Subcommands.Add(CreateGenerateCommand());
        rootCommand.Subcommands.Add(CreateInitCommand());
        rootCommand.Subcommands.Add(CreatePlanCommand());
        rootCommand.Subcommands.Add(CreateApplyCommand());
        rootCommand.Subcommands.Add(CreateImportCommand());
        rootCommand.Subcommands.Add(CreateRefreshPlanCommand());
        rootCommand.Subcommands.Add(CreateRefreshApplyCommand());
        rootCommand.Subcommands.Add(CreateShowCommand());
        rootCommand.Subcommands.Add(CreateTerraformCommand());

        try
        {
            return await rootCommand.Parse(args).InvokeAsync();
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or IOException
                or UnauthorizedAccessException
                or System.Text.Json.JsonException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static Command CreateGenerateCommand()
    {
        var command = new Command("generate", "Generate Terraform JSON for configured resources.");
        var options = AddCommonOptions(command);

        command.SetAction(parseResult => RunSafely(() =>
        {
            var outputDirectory = GetOutputDirectory(parseResult.GetValue(options.OutputDirectory));
            var scopeFilter = parseResult.GetValue(options.Scope);
            var scopes = GetConfiguredScopes(scopeFilter);
            return GenerateResources(scopes, outputDirectory);
        }));

        return command;
    }

    private static Command CreatePlanCommand()
    {
        var command = new Command("plan", "Generate Terraform JSON and create saved Terraform plans.");
        var options = AddCommonOptions(command);

        command.SetAction((parseResult, cancellationToken) => RunSafely(async () =>
        {
            var outputDirectory = GetOutputDirectory(parseResult.GetValue(options.OutputDirectory));
            var scopeFilter = parseResult.GetValue(options.Scope);
            var scopes = GetConfiguredScopes(scopeFilter);

            var generationResult = GenerateResources(scopes, outputDirectory);
            if (generationResult != 0)
            {
                return generationResult;
            }

            var planScopes = scopes
                .Where(scope => Directory.Exists(Path.Combine(outputDirectory, scope.RelativePath))
                    && Directory.EnumerateFiles(Path.Combine(outputDirectory, scope.RelativePath), "*.tf.json").Any())
                .ToArray();
            if (planScopes.Length == 0)
            {
                Console.Error.WriteLine($"No scoped Terraform JSON resources were found under '{outputDirectory}'.");
                return 1;
            }

            return await RunTerraformInitAndPlanInScopes(
                planScopes,
                outputDirectory,
                cancellationToken);
        }));

        return command;
    }

    private static Command CreateInitCommand()
    {
        var command = new Command("init", "Generate Terraform JSON and initialize each selected Terraform scope.");
        var options = AddCommonOptions(command);

        command.SetAction((parseResult, cancellationToken) => RunSafely(async () =>
        {
            var outputDirectory = GetOutputDirectory(parseResult.GetValue(options.OutputDirectory));
            var scopeFilter = parseResult.GetValue(options.Scope);
            var scopes = GetConfiguredScopes(scopeFilter);

            var generationResult = GenerateResources(scopes, outputDirectory);
            if (generationResult != 0)
            {
                return generationResult;
            }

            var scopeDirectories = GetConfigurationDirectories(outputDirectory, scopes);
            if (scopeDirectories.Count == 0)
            {
                Console.Error.WriteLine($"No scoped Terraform JSON resources were found under '{outputDirectory}'.");
                return 1;
            }

            return await RunTerraformInDirectories(
                scopeDirectories,
                ["init", "-input=false"],
                cancellationToken);
        }));

        return command;
    }

    private static Command CreateApplyCommand()
    {
        var command = new Command("apply", "Apply saved Terraform plans without regenerating configuration.");
        var options = AddCommonOptions(command);

        command.SetAction((parseResult, cancellationToken) => RunSafely(async () =>
        {
            var outputDirectory = GetOutputDirectory(parseResult.GetValue(options.OutputDirectory));
            var scopeFilter = parseResult.GetValue(options.Scope);
            var scopes = GetConfiguredScopes(scopeFilter);
            if (scopes.Count == 0)
            {
                Console.Error.WriteLine("No configured resource scopes were found.");
                return 1;
            }

            var planScopes = new List<ScopedDirectory>();
            var missingPlans = 0;
            foreach (var scope in scopes)
            {
                var scopeDirectory = Path.Combine(outputDirectory, scope.RelativePath);
                if (File.Exists(Path.Combine(scopeDirectory, PlanFileName)))
                {
                    planScopes.Add(scope);
                }
                else
                {
                    Console.Error.WriteLine(
                        $"No saved plan found for scope '{scope.RelativePath}' at '{scopeDirectory}'. Run 'plan --scope {scope.RelativePath}' first.");
                    missingPlans++;
                }
            }

            var failures = missingPlans;
            foreach (var scope in planScopes)
            {
                var scopeDirectory = Path.Combine(outputDirectory, scope.RelativePath);
                try
                {
                    if (!await ApplyScope(scopeDirectory, cancellationToken))
                    {
                        failures++;
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    Console.Error.WriteLine($"Could not apply scope '{scope.RelativePath}': {exception.Message}");
                    failures++;
                }
            }

            return failures == 0 ? 0 : 1;
        }));

        return command;
    }

    private static Command CreateImportCommand()
    {
        var command = new Command(
            "import",
            "Import already-existing LaunchDarkly resources into Terraform state without creating or changing infrastructure.");
        var options = AddCommonOptions(command);

        command.SetAction((parseResult, cancellationToken) => RunSafely(async () =>
        {
            var outputDirectory = GetOutputDirectory(parseResult.GetValue(options.OutputDirectory));
            var scopeFilter = parseResult.GetValue(options.Scope);
            var scopes = GetConfiguredScopes(scopeFilter);

            var generationResult = GenerateResources(scopes, outputDirectory);
            if (generationResult != 0)
            {
                return generationResult;
            }

            var importScopes = scopes
                .Where(scope => Directory.Exists(Path.Combine(outputDirectory, scope.RelativePath))
                    && Directory.EnumerateFiles(Path.Combine(outputDirectory, scope.RelativePath), "*.tf.json").Any())
                .ToArray();
            if (importScopes.Length == 0)
            {
                Console.Error.WriteLine($"No scoped Terraform JSON resources were found under '{outputDirectory}'.");
                return 1;
            }

            return await RunTerraformInitAndImportInScopes(importScopes, outputDirectory, cancellationToken);
        }));

        return command;
    }

    private static Command CreateRefreshPlanCommand()
    {
        var command = new Command(
            "refresh-plan",
            "Generate and display Terraform refresh-only plans without updating state.");
        var options = AddCommonOptions(command);

        command.SetAction((parseResult, cancellationToken) => RunSafely(async () =>
        {
            var outputDirectory = GetOutputDirectory(parseResult.GetValue(options.OutputDirectory));
            var scopeFilter = parseResult.GetValue(options.Scope);
            var scopes = GetConfiguredScopes(scopeFilter);

            var generationResult = GenerateResources(scopes, outputDirectory);
            if (generationResult != 0)
            {
                return generationResult;
            }

            var refreshScopes = scopes
                .Where(scope => Directory.Exists(Path.Combine(outputDirectory, scope.RelativePath))
                    && Directory.EnumerateFiles(Path.Combine(outputDirectory, scope.RelativePath), "*.tf.json").Any())
                .ToArray();
            if (refreshScopes.Length == 0)
            {
                Console.Error.WriteLine($"No scoped Terraform JSON resources were found under '{outputDirectory}'.");
                return 1;
            }

            return await RunTerraformInitAndRefreshPlanInScopes(
                refreshScopes,
                outputDirectory,
                cancellationToken);
        }));

        return command;
    }

    private static Command CreateRefreshApplyCommand()
    {
        var command = new Command(
            "refresh-apply",
            "Apply saved Terraform refresh-only plans to update state without changing infrastructure.");
        var options = AddCommonOptions(command);

        command.SetAction((parseResult, cancellationToken) => RunSafely(async () =>
        {
            var outputDirectory = GetOutputDirectory(parseResult.GetValue(options.OutputDirectory));
            var scopeFilter = parseResult.GetValue(options.Scope);
            var scopes = GetConfiguredScopes(scopeFilter);
            if (scopes.Count == 0)
            {
                Console.Error.WriteLine("No configured resource scopes were found.");
                return 1;
            }

            var refreshScopes = new List<ScopedDirectory>();
            var missingPlans = 0;
            foreach (var scope in scopes)
            {
                var scopeDirectory = Path.Combine(outputDirectory, scope.RelativePath);
                var planPath = Path.Combine(scopeDirectory, RefreshPlanFileName);
                var metadataPath = Path.Combine(scopeDirectory, RefreshPlanMetadataFileName);
                if (File.Exists(planPath) && File.Exists(metadataPath))
                {
                    refreshScopes.Add(scope);
                }
                else
                {
                    Console.Error.WriteLine(
                        $"No saved refresh-only plan and metadata found for scope '{scope.RelativePath}' at '{scopeDirectory}'. Run 'refresh-plan --scope {scope.RelativePath}' first.");
                    missingPlans++;
                }
            }

            var failures = missingPlans;
            foreach (var scope in refreshScopes)
            {
                var scopeDirectory = Path.Combine(outputDirectory, scope.RelativePath);
                try
                {
                    if (!await ApplyRefreshPlan(scopeDirectory, scope.RelativePath, cancellationToken))
                    {
                        failures++;
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    Console.Error.WriteLine(
                        $"Could not apply refresh-only plan for scope '{scope.RelativePath}': {exception.Message}");
                    failures++;
                }
            }

            return failures == 0 ? 0 : 1;
        }));

        return command;
    }

    private static Command CreateShowCommand()
    {
        var command = new Command("show", "Show Terraform state for selected scope(s) or one resource.");
        var options = AddCommonOptions(command);
        var resourceOption = new Option<string?>("--resource")
        {
            Description = "Terraform resource address to show, for example launchdarkly_project.my-project. Requires --scope."
        };
        command.Options.Add(resourceOption);

        command.SetAction((parseResult, cancellationToken) => RunSafely(async () =>
        {
            var outputDirectory = GetOutputDirectory(parseResult.GetValue(options.OutputDirectory));
            var scopeFilter = parseResult.GetValue(options.Scope);
            var resourceAddress = parseResult.GetValue(resourceOption);

            if (!string.IsNullOrWhiteSpace(resourceAddress) && scopeFilter is null)
            {
                throw new ArgumentException("--scope is required when --resource is specified.", nameof(scopeFilter));
            }

            var scopes = GetConfiguredScopes(scopeFilter);
            var scopeDirectories = scopes
                .Select(scope => Path.Combine(outputDirectory, scope.RelativePath))
                .ToArray();
            var arguments = string.IsNullOrWhiteSpace(resourceAddress)
                ? new[] { "show" }
                : new[] { "state", "show", resourceAddress };

            return await RunTerraformInDirectories(scopeDirectories, arguments, cancellationToken);
        }));

        return command;
    }

    private static Command CreateTerraformCommand()
    {
        var command = new Command("terraform", "Prepare one scope and run a Terraform command in it.");
        var outputDirectoryOption = new Option<string?>("--output-dir")
        {
            Description = "Root directory for scoped Terraform files. Defaults to ./terraform."
        };
        var scopeOption = new Option<string>("--scope")
        {
            Description = "Required scope to run the command against, for example Tenants/TenantA.",
            Required = true
        };
        var terraformArguments = new Argument<string[]>("terraform-args")
        {
            Description = "Terraform subcommand and arguments. Use -- before Terraform flags.",
            Arity = ArgumentArity.OneOrMore
        };

        command.Options.Add(outputDirectoryOption);
        command.Options.Add(scopeOption);
        command.Arguments.Add(terraformArguments);
        command.SetAction((parseResult, cancellationToken) => RunSafely(async () =>
        {
            var outputDirectory = GetOutputDirectory(parseResult.GetValue(outputDirectoryOption));
            var scopeName = parseResult.GetValue(scopeOption);
            var terraformArgumentsValue = parseResult.GetValue(terraformArguments)
                ?? throw new ArgumentException("At least one Terraform subcommand argument is required.", nameof(terraformArguments));
            var scopes = GetConfiguredScopes(scopeName);
            var scope = scopes.Single();

            var generationResult = GenerateResources(scopes, outputDirectory);
            if (generationResult != 0)
            {
                return generationResult;
            }

            var scopeDirectory = Path.Combine(outputDirectory, scope.RelativePath);
            if (!await RunTerraformStep(scopeDirectory, ["init", "-input=false"], cancellationToken))
            {
                return 1;
            }

            return await RunTerraform(scopeDirectory, terraformArgumentsValue, cancellationToken);
        }));

        return command;
    }

    private static CommonOptions AddCommonOptions(Command command)
    {
        var outputDirectoryOption = new Option<string?>("--output-dir")
        {
            Description = "Root directory for scoped Terraform files. Defaults to ./terraform."
        };
        var scopeOption = new Option<string?>("--scope")
        {
            Description = "Run only the selected scope, for example Tenants/TenantA or Platform/Prod."
        };

        command.Options.Add(outputDirectoryOption);
        command.Options.Add(scopeOption);

        return new CommonOptions(outputDirectoryOption, scopeOption);
    }

    private static string GetOutputDirectory(string? outputDirectory)
        => Path.GetFullPath(outputDirectory ?? Path.Combine(Environment.CurrentDirectory, "terraform"));

    private static int RunSafely(Func<int> action)
    {
        try
        {
            return action();
        }
        catch (Exception exception) when (IsCommandException(exception))
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static async Task<int> RunSafely(Func<Task<int>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception exception) when (IsCommandException(exception))
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static bool IsCommandException(Exception exception)
        => exception is ArgumentException
            or InvalidOperationException
            or IOException
            or UnauthorizedAccessException
            or System.Text.Json.JsonException;

    private static IReadOnlyList<ScopedDirectory> GetConfiguredScopes(string? scopeFilter)
    {
        var assembly = Assembly.GetEntryAssembly()
            ?? throw new InvalidOperationException("Could not determine the CLI entry assembly.");
        var configuredScopes = ResourceLocator.ScopeResources(assembly)
            .Select(scope => new ScopedDirectory(scope, GetScopeRelativePath(scope.Scope)))
            .ToArray();

        if (scopeFilter is null)
        {
            return configuredScopes;
        }

        var normalizedFilter = NormalizeScopeFilter(scopeFilter);
        var selectedScopes = configuredScopes
            .Where(scope => string.Equals(scope.RelativePath, normalizedFilter, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (selectedScopes.Length == 0)
        {
            throw new ArgumentException(
                $"Scope '{scopeFilter}' was not found. Available scopes: {string.Join(", ", configuredScopes.Select(scope => scope.RelativePath))}",
                nameof(scopeFilter));
        }

        return selectedScopes;
    }

    private static string GetScopeRelativePath(string scopeNamespace)
    {
        if (!scopeNamespace.StartsWith(ResourceNamespacePrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Resource scope namespace '{scopeNamespace}' must start with '{ResourceNamespacePrefix}'.");
        }

        var scopeSegments = scopeNamespace[ResourceNamespacePrefix.Length..]
            .Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (scopeSegments.Length == 0 || scopeSegments.Any(segment => segment is "." or ".."))
        {
            throw new InvalidOperationException($"Resource scope namespace '{scopeNamespace}' is not a valid scope path.");
        }

        return Path.Combine(scopeSegments);
    }

    private static string NormalizeScopeFilter(string scopeFilter)
    {
        var segments = scopeFilter
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 1 && segments[0].Contains('.', StringComparison.Ordinal))
        {
            segments = segments[0].Split('.', StringSplitOptions.RemoveEmptyEntries);
        }

        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("Scope must contain valid path segments.", nameof(scopeFilter));
        }

        return Path.Combine(segments);
    }

    private static int GenerateResources(IReadOnlyList<ScopedDirectory> scopes, string outputDirectory)
    {
        var generator = new TerraformGenerator();

        foreach (var scope in scopes)
        {
            var scopeDirectory = Path.Combine(outputDirectory, scope.RelativePath);
            Directory.CreateDirectory(scopeDirectory);

            Console.WriteLine($"Generating scope '{scope.RelativePath}' in '{scopeDirectory}'.");
            foreach (var resource in scope.Resources.Resources)
            {
                var terraformJson = generator.GenerateTerraformFromResource(resource);
                var fileName = $"{resource.GetType().Name}-{SanitizeFileName(resource.ResourceName)}.tf.json";
                var resourceOutputPath = Path.Combine(scopeDirectory, fileName);

                File.WriteAllText(resourceOutputPath, terraformJson);
                Console.WriteLine($"  Wrote {resourceOutputPath}");
            }

            var providerConfiguration = scope.Resources.ProviderConfiguration ?? TerraformConfiguration.Default;
            var providerOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            var providerConfigurationJson = JsonSerializer.Serialize(providerConfiguration, providerOptions);
            var providerConfigurationPath = Path.Combine(scopeDirectory, "providers.tf.json");
            File.WriteAllText(providerConfigurationPath, providerConfigurationJson);
            Console.WriteLine($"  Wrote {providerConfigurationPath}");
        }

        return 0;
    }

    private static string SanitizeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        var sanitized = new string(value.Select(character =>
            invalidCharacters.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "resource" : sanitized;
    }

    private static List<string> GetConfigurationDirectories(
        string outputDirectory,
        IReadOnlyList<ScopedDirectory> configuredScopes)
    {
        return configuredScopes
            .Select(scope => Path.Combine(outputDirectory, scope.RelativePath))
            .Where(directory => Directory.Exists(directory)
                && Directory.EnumerateFiles(directory, "*.tf.json").Any())
            .ToList();
    }

    private static async Task<int> RunTerraformInDirectories(
        IReadOnlyList<string> directories,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var failures = 0;
        foreach (var directory in directories)
        {
            Console.WriteLine($"Running terraform {string.Join(' ', arguments)} in '{directory}'.");
            try
            {
                var exitCode = await RunTerraform(directory, arguments, cancellationToken);
                if (exitCode != 0)
                {
                    Console.Error.WriteLine($"Terraform failed in '{directory}' with exit code {exitCode}.");
                    failures++;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Console.Error.WriteLine($"Could not run Terraform in '{directory}': {exception.Message}");
                failures++;
            }
        }

        return failures == 0 ? 0 : 1;
    }

    private static async Task<int> RunTerraformInitAndPlanInScopes(
        IReadOnlyList<ScopedDirectory> scopes,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        var failures = 0;
        foreach (var scope in scopes)
        {
            var directory = Path.Combine(outputDirectory, scope.RelativePath);
            try
            {
                if (!await RunTerraformStep(directory, ["init", "-input=false"], cancellationToken))
                {
                    failures++;
                    continue;
                }

                var stateAddresses = await GetTerraformStateAddresses(directory, cancellationToken);
                if (stateAddresses is null)
                {
                    failures++;
                    continue;
                }

                if (!await RunTerraformStep(directory, ["plan", "-input=false", "-out", PlanFileName], cancellationToken))
                {
                    failures++;
                    continue;
                }

                var planOutput = await RunTerraformForOutput(directory, ["show", "-json", PlanFileName], cancellationToken);
                if (planOutput.ExitCode != 0)
                {
                    Console.Error.WriteLine(
                        $"Terraform show -json failed in '{directory}' with exit code {planOutput.ExitCode}.");
                    Console.Error.Write(planOutput.StandardError);
                    failures++;
                    continue;
                }

                Console.Error.Write(planOutput.StandardError);
                var plannedProjectCreates = GetPlannedProjectCreates(planOutput.StandardOutput);
                var configuredResources = GetBootstrapResources(scope.Resources);
                var eligibleProjectAddresses = configuredResources.ProjectAddresses
                    .Where(address => stateAddresses.Contains(address) || plannedProjectCreates.Contains(address))
                    .ToHashSet(StringComparer.Ordinal);
                var imports = configuredResources.EnvironmentImports
                    .Where(resource => !stateAddresses.Contains(resource.Address)
                        && eligibleProjectAddresses.Contains(resource.ProjectAddress))
                    .ToArray();
                var bootstrapProjectAddresses = plannedProjectCreates
                    .Where(address => !stateAddresses.Contains(address)
                        && configuredResources.ProjectAddresses.Contains(address, StringComparer.Ordinal))
                    .ToArray();

                var metadata = new BootstrapMetadata(bootstrapProjectAddresses, imports);
                var metadataPath = Path.Combine(directory, BootstrapMetadataFileName);
                await File.WriteAllTextAsync(
                    metadataPath,
                    JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }),
                    cancellationToken);
                Console.WriteLine($"  Wrote bootstrap metadata {metadataPath}");

                if (bootstrapProjectAddresses.Length > 0 || imports.Length > 0)
                {
                    Console.WriteLine(
                        $"Bootstrap required in '{scope.RelativePath}': {bootstrapProjectAddresses.Length} project(s), {imports.Length} environment import(s).");
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Console.Error.WriteLine($"Could not prepare plan for scope '{scope.RelativePath}': {exception.Message}");
                failures++;
            }
        }

        return failures == 0 ? 0 : 1;
    }

    private static async Task<int> RunTerraformInitAndRefreshPlanInScopes(
        IReadOnlyList<ScopedDirectory> scopes,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        var failures = 0;
        foreach (var scope in scopes)
        {
            var directory = Path.Combine(outputDirectory, scope.RelativePath);
            try
            {
                var planPath = Path.Combine(directory, RefreshPlanFileName);
                var metadataPath = Path.Combine(directory, RefreshPlanMetadataFileName);
                File.Delete(planPath);
                File.Delete(metadataPath);

                if (!await RunTerraformStep(directory, ["init", "-input=false"], cancellationToken)
                    || !await RunTerraformStep(
                        directory,
                        ["plan", "-refresh-only", "-input=false", "-out", RefreshPlanFileName],
                        cancellationToken)
                    || !await RunTerraformStep(directory, ["show", "-no-color", RefreshPlanFileName], cancellationToken))
                {
                    failures++;
                    continue;
                }

                var planHash = Convert.ToHexString(
                    SHA256.HashData(await File.ReadAllBytesAsync(planPath, cancellationToken)));
                var metadata = new RefreshPlanMetadata(scope.RelativePath, planHash);
                await File.WriteAllTextAsync(
                    metadataPath,
                    JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }),
                    cancellationToken);
                Console.WriteLine($"  Wrote refresh-only plan metadata {metadataPath}");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Console.Error.WriteLine(
                    $"Could not prepare refresh-only plan for scope '{scope.RelativePath}': {exception.Message}");
                failures++;
            }
        }

        return failures == 0 ? 0 : 1;
    }

    private static async Task<bool> ApplyRefreshPlan(
        string directory,
        string scope,
        CancellationToken cancellationToken)
    {
        var planPath = Path.Combine(directory, RefreshPlanFileName);
        var metadataPath = Path.Combine(directory, RefreshPlanMetadataFileName);
        if (!File.Exists(planPath) || !File.Exists(metadataPath))
        {
            Console.Error.WriteLine(
                $"A saved refresh-only plan and metadata are required in '{directory}'. Run 'refresh-plan --scope {scope}' first.");
            return false;
        }

        var metadata = JsonSerializer.Deserialize<RefreshPlanMetadata>(
                await File.ReadAllTextAsync(metadataPath, cancellationToken))
            ?? throw new JsonException($"Refresh plan metadata in '{metadataPath}' is empty.");
        if (!string.Equals(metadata.Scope, scope, StringComparison.Ordinal))
        {
            Console.Error.WriteLine(
                $"Refresh plan metadata in '{metadataPath}' is for scope '{metadata.Scope}', not '{scope}'.");
            return false;
        }

        var planHash = Convert.ToHexString(
            SHA256.HashData(await File.ReadAllBytesAsync(planPath, cancellationToken)));
        if (!string.Equals(metadata.PlanSha256, planHash, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine(
                $"Saved refresh-only plan '{planPath}' does not match its metadata. Run 'refresh-plan --scope {scope}' again.");
            return false;
        }

        Console.WriteLine($"Applying the reviewed refresh-only plan for scope '{scope}'.");
        return await RunTerraformStep(
            directory,
            ["apply", "-input=false", RefreshPlanFileName],
            cancellationToken);
    }

    private static async Task<bool> ApplyScope(string directory, CancellationToken cancellationToken)
    {
        var metadataPath = Path.Combine(directory, BootstrapMetadataFileName);
        if (!File.Exists(metadataPath))
        {
            return await RunTerraformStep(directory, ["apply", PlanFileName], cancellationToken);
        }

        var metadata = JsonSerializer.Deserialize<BootstrapMetadata>(
                await File.ReadAllTextAsync(metadataPath, cancellationToken))
            ?? throw new JsonException($"Bootstrap metadata in '{metadataPath}' is empty.");

        var stateAddresses = await GetTerraformStateAddresses(directory, cancellationToken);
        if (stateAddresses is null)
        {
            return false;
        }

        var missingProjects = metadata.ProjectAddresses
            .Where(address => !stateAddresses.Contains(address))
            .ToArray();
        var stateChanged = false;
        if (missingProjects.Length > 0)
        {
            var bootstrapArguments = new List<string> { "plan", "-input=false" };
            foreach (var address in missingProjects)
            {
                bootstrapArguments.Add("-target");
                bootstrapArguments.Add(address);
            }

            bootstrapArguments.Add("-out");
            bootstrapArguments.Add(BootstrapPlanFileName);
            if (!await RunTerraformStep(directory, bootstrapArguments, cancellationToken)
                || !await RunTerraformStep(directory, ["apply", BootstrapPlanFileName], cancellationToken))
            {
                return false;
            }

            stateChanged = true;
            stateAddresses = await GetTerraformStateAddresses(directory, cancellationToken);
            if (stateAddresses is null)
            {
                return false;
            }
        }

        foreach (var environment in metadata.EnvironmentImports)
        {
            if (stateAddresses.Contains(environment.Address))
            {
                continue;
            }

            if (!stateAddresses.Contains(environment.ProjectAddress))
            {
                Console.Error.WriteLine(
                    $"Cannot import environment '{environment.Address}' because project '{environment.ProjectAddress}' is not in Terraform state. If the project already exists in LaunchDarkly, import it manually before applying.");
                return false;
            }

            Console.WriteLine(
                $"Running terraform import -input=false {environment.Address} {environment.ImportId} in '{directory}'.");
            var importResult = await RunTerraformForOutput(
                directory,
                ["import", "-input=false", environment.Address, environment.ImportId],
                cancellationToken);

            if (importResult.ExitCode != 0)
            {
                if (importResult.StandardError.Contains(
                    CannotImportNonExistentRemoteObjectMessage,
                    StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(
                        $"  '{environment.Address}' does not exist remotely yet; Terraform will create it.");
                    continue;
                }

                Console.Error.WriteLine(
                    $"Terraform import failed for '{environment.Address}' in '{directory}' with exit code {importResult.ExitCode}.");
                Console.Error.Write(importResult.StandardError);
                return false;
            }

            stateChanged = true;
            stateAddresses = await GetTerraformStateAddresses(directory, cancellationToken);
            if (stateAddresses is null)
            {
                return false;
            }
        }

        if (stateChanged)
        {
            Console.WriteLine("Bootstrap changed Terraform state; creating a fresh plan before applying.");
            if (!await RunTerraformStep(directory, ["plan", "-input=false", "-out", PlanFileName], cancellationToken))
            {
                return false;
            }
        }

        return await RunTerraformStep(directory, ["apply", PlanFileName], cancellationToken);
    }

    private static async Task<HashSet<string>?> GetTerraformStateAddresses(
        string directory,
        CancellationToken cancellationToken)
    {
        var result = await RunTerraformForOutput(directory, ["state", "list"], cancellationToken);
        if (result.ExitCode != 0)
        {
            if (result.StandardError.Contains(NoTerraformStateMessage, StringComparison.OrdinalIgnoreCase))
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            Console.Error.WriteLine(
                $"Terraform state list failed in '{directory}' with exit code {result.ExitCode}.");
            Console.Error.Write(result.StandardError);
            return null;
        }

        Console.Error.Write(result.StandardError);
        return result.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static async Task<TerraformOutput> RunTerraformForOutput(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(workingDirectory))
        {
            throw new DirectoryNotFoundException($"Scope directory '{workingDirectory}' does not exist.");
        }

        Console.WriteLine($"Running terraform {string.Join(' ', arguments)} in '{workingDirectory}'.");
        var startInfo = CreateTerraformStartInfo(workingDirectory, arguments);
        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(standardOutputTask, standardErrorTask);

        return new TerraformOutput(
            process.ExitCode,
            await standardOutputTask,
            await standardErrorTask);
    }

    private static HashSet<string> GetPlannedProjectCreates(string planJson)
    {
        using var document = JsonDocument.Parse(planJson);
        var addresses = new HashSet<string>(StringComparer.Ordinal);
        if (!document.RootElement.TryGetProperty("resource_changes", out var resourceChanges)
            || resourceChanges.ValueKind != JsonValueKind.Array)
        {
            return addresses;
        }

        foreach (var resourceChange in resourceChanges.EnumerateArray())
        {
            if (!resourceChange.TryGetProperty("type", out var type)
                || type.GetString() != LaunchDarklyProject.ResourceType
                || !resourceChange.TryGetProperty("change", out var change)
                || !change.TryGetProperty("actions", out var actions)
                || actions.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var actionList = actions.EnumerateArray().Select(action => action.GetString()).ToArray();
            if (actionList.Length == 1
                && actionList[0] == "create"
                && resourceChange.TryGetProperty("address", out var address))
            {
                addresses.Add(address.GetString()
                    ?? throw new JsonException("Terraform plan contained a project change without an address."));
            }
        }

        return addresses;
    }

    private static BootstrapResources GetBootstrapResources(ScopedResources resources)
    {
        var projectAddresses = new List<string>();
        var environmentImports = new List<EnvironmentImport>();
        foreach (var project in resources.Resources.OfType<LaunchDarklyProject>())
        {
            var projectAddress = $"{LaunchDarklyProject.ResourceType}.{project.Key}";
            projectAddresses.Add(projectAddress);
            foreach (var environmentKey in project.Environments.Keys)
            {
                environmentImports.Add(new EnvironmentImport(
                    $"launchdarkly_environment.{project.Key}-{environmentKey}",
                    projectAddress,
                    $"{project.Key}/{environmentKey}"));
            }
        }

        return new BootstrapResources(projectAddresses, environmentImports);
    }

    private static async Task<int> RunTerraformInitAndImportInScopes(
        IReadOnlyList<ScopedDirectory> scopes,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        var failures = 0;
        foreach (var scope in scopes)
        {
            var directory = Path.Combine(outputDirectory, scope.RelativePath);
            try
            {
                if (!await RunTerraformStep(directory, ["init", "-input=false"], cancellationToken))
                {
                    failures++;
                    continue;
                }

                var stateAddresses = await GetTerraformStateAddresses(directory, cancellationToken);
                if (stateAddresses is null)
                {
                    failures++;
                    continue;
                }

                var candidates = GetImportCandidates(scope.Resources);
                foreach (var skipped in candidates.Skipped)
                {
                    Console.WriteLine(
                        $"  Skipping '{skipped}': this resource type's Terraform import ID cannot be derived from configuration. Import it manually with its LaunchDarkly-assigned ID if it already exists remotely.");
                }

                var missing = candidates.Importable
                    .Where(candidate => !stateAddresses.Contains(candidate.Address))
                    .ToArray();
                if (missing.Length == 0)
                {
                    Console.WriteLine($"Scope '{scope.RelativePath}' has no configured resources missing from state.");
                    continue;
                }

                var imported = 0;
                var notFound = 0;
                foreach (var candidate in missing)
                {
                    Console.WriteLine(
                        $"Checking whether '{candidate.Address}' already exists remotely (import ID '{candidate.ImportId}')...");
                    var result = await RunTerraformForOutput(
                        directory,
                        ["import", "-input=false", candidate.Address, candidate.ImportId],
                        cancellationToken);

                    if (result.ExitCode == 0)
                    {
                        Console.WriteLine($"  Imported '{candidate.Address}' into Terraform state.");
                        imported++;
                        continue;
                    }

                    if (result.StandardError.Contains(
                        CannotImportNonExistentRemoteObjectMessage,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine(
                            $"  '{candidate.Address}' does not exist remotely yet; leaving it for 'plan'/'apply' to create.");
                        notFound++;
                        continue;
                    }

                    Console.Error.WriteLine(
                        $"Import failed for '{candidate.Address}' in '{directory}' with exit code {result.ExitCode}.");
                    Console.Error.Write(result.StandardError);
                    failures++;
                }

                Console.WriteLine(
                    $"Scope '{scope.RelativePath}': imported {imported}, not found remotely {notFound}, out of {missing.Length} candidate(s).");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Console.Error.WriteLine(
                    $"Could not import resources for scope '{scope.RelativePath}': {exception.Message}");
                failures++;
            }
        }

        return failures == 0 ? 0 : 1;
    }

    private static ImportCandidates GetImportCandidates(ScopedResources resources)
    {
        var importable = new List<ImportCandidate>();
        var skipped = new List<string>();

        foreach (var project in resources.Resources.OfType<LaunchDarklyProject>())
        {
            importable.Add(new ImportCandidate($"{LaunchDarklyProject.ResourceType}.{project.Key}", project.Key));
            foreach (var environmentKey in project.Environments.Keys)
            {
                importable.Add(new ImportCandidate(
                    $"launchdarkly_environment.{project.Key}-{environmentKey}",
                    $"{project.Key}/{environmentKey}"));
            }
        }

        foreach (var team in resources.Resources.OfType<LaunchDarklyTeam>())
        {
            importable.Add(new ImportCandidate($"{LaunchDarklyTeam.ResourceType}.{team.Key}", team.Key));
        }

        foreach (var customRole in resources.Resources.OfType<LaunchDarklyCustomRole>())
        {
            importable.Add(new ImportCandidate($"{LaunchDarklyCustomRole.ResourceType}.{customRole.Key}", customRole.Key));
        }

        foreach (var relayProxyConfiguration in resources.Resources.OfType<RelayProxyConfiguration>())
        {
            skipped.Add($"{RelayProxyConfiguration.ResourceType}.{relayProxyConfiguration.Name}");
        }

        return new ImportCandidates(importable, skipped);
    }

    private static async Task<bool> RunTerraformStep(
        string directory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Running terraform {string.Join(' ', arguments)} in '{directory}'.");
        try
        {
            var exitCode = await RunTerraform(directory, arguments, cancellationToken);
            if (exitCode == 0)
            {
                return true;
            }

            Console.Error.WriteLine($"Terraform failed in '{directory}' with exit code {exitCode}.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine($"Could not run Terraform in '{directory}': {exception.Message}");
        }

        return false;
    }

    private static async Task<int> RunTerraform(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(workingDirectory))
        {
            throw new DirectoryNotFoundException($"Scope directory '{workingDirectory}' does not exist.");
        }

        var startInfo = CreateTerraformStartInfo(workingDirectory, arguments);
        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var standardOutputTask = CopyOutputAsync(process.StandardOutput, Console.Out, cancellationToken);
        var standardErrorTask = CopyOutputAsync(process.StandardError, Console.Error, cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(standardOutputTask, standardErrorTask);

        return process.ExitCode;
    }

    private static ProcessStartInfo CreateTerraformStartInfo(
        string workingDirectory,
        IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "terraform",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static async Task CopyOutputAsync(
        StreamReader reader,
        TextWriter writer,
        CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        int charsRead;
        while ((charsRead = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            await writer.WriteAsync(buffer.AsMemory(0, charsRead), cancellationToken);
            await writer.FlushAsync(cancellationToken);
        }
    }

    private sealed record CommonOptions(Option<string?> OutputDirectory, Option<string?> Scope);
    private sealed record ScopedDirectory(ScopedResources Resources, string RelativePath);
    private sealed record BootstrapResources(
        IReadOnlyList<string> ProjectAddresses,
        IReadOnlyList<EnvironmentImport> EnvironmentImports);
    private sealed record BootstrapMetadata(
        IReadOnlyList<string> ProjectAddresses,
        IReadOnlyList<EnvironmentImport> EnvironmentImports);
    private sealed record EnvironmentImport(string Address, string ProjectAddress, string ImportId);
    private sealed record ImportCandidate(string Address, string ImportId);
    private sealed record ImportCandidates(
        IReadOnlyList<ImportCandidate> Importable,
        IReadOnlyList<string> Skipped);
    private sealed record RefreshPlanMetadata(string Scope, string PlanSha256);
    private sealed record TerraformOutput(int ExitCode, string StandardOutput, string StandardError);
}
