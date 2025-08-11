### Configuration loading and verification

Scope
- How configuration is loaded at runtime
- Ensuring `appsettings.json` is found when running from solution root or different working directories
- Verifying effective values

Loading strategy
- The console app uses `AppContext.BaseDirectory` as the base path for configuration, so the runtime looks for `appsettings.json` beside the compiled binaries.
- The project copies `appsettings.json` to the output directory using `CopyToOutputDirectory=PreserveNewest`.

Console app code
The `Program.cs` initializes configuration like this:

```csharp
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();
```

Runtime diagnostics
`Program.cs` prints the relevant values using `configuration["Section:Key"]` and `GetValue<T>()`:

```csharp
Console.WriteLine("BaseDir: " + AppContext.BaseDirectory);
Console.WriteLine("appsettings.json present at BaseDir: " + File.Exists(Path.Combine(AppContext.BaseDirectory, "appsettings.json")));
Console.WriteLine("VectorDb:Provider: " + configuration["VectorDb:Provider"]);
Console.WriteLine("VectorDb:Endpoint: " + configuration["VectorDb:Endpoint"]);
Console.WriteLine("VectorDb:CollectionPrefix: " + configuration["VectorDb:CollectionPrefix"]);
Console.WriteLine("VectorDb:TopK: " + configuration.GetValue<int?>("VectorDb:TopK"));
Console.WriteLine("ResearchAgent:MaxConcurrency: " + configuration.GetValue<int?>("ResearchAgent:MaxConcurrency"));
Console.WriteLine("ResearchAgent:DefaultPriority: " + configuration.GetValue<int?>("ResearchAgent:DefaultPriority"));
Console.WriteLine("ResearchAgent:MaxDecompositionDepth: " + configuration.GetValue<int?>("ResearchAgent:MaxDecompositionDepth"));
Console.WriteLine("ResearchAgent:LogPrompts: " + configuration.GetValue<bool?>("ResearchAgent:LogPrompts"));
```

Common pitfalls
- Using `configuration.GetSection("X:Y").Value` often returns null; prefer the indexer or `GetValue<T>()`.
- If you run from the solution root with `dotnet run --project ResearchAgentNetwork.ConsoleApp`, `Directory.GetCurrentDirectory()` is not the project/bin folder, so the JSON may not be found unless copied to output and BaseDirectory is used.

Testing
- Confirm the diagnostics show `appsettings.json present at BaseDir: True` and configuration values are printed.
- Change a value (e.g., `VectorDb:Provider`) and rerun to verify the updated value is picked up.

