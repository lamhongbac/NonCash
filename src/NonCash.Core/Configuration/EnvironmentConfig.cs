namespace NonCash.Core.Configuration;

public class EnvironmentConfig
{
    public const string SectionName = "Environment";

    /// <summary>
    /// Environment name: dev, pilot, or production.
    /// </summary>
    public string Name { get; set; } = "dev";

    public bool IsDev => Name.Equals("dev", StringComparison.OrdinalIgnoreCase);
    public bool IsPilot => Name.Equals("pilot", StringComparison.OrdinalIgnoreCase);
    public bool IsProduction => Name.Equals("production", StringComparison.OrdinalIgnoreCase);
}
