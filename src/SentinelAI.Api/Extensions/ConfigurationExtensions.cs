using Microsoft.Extensions.Configuration;

namespace SentinelAI.Api.Extensions;

/// <summary>
/// Extension methods for configuration with environment variable support
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
    /// Adds configuration with environment variable placeholders support
    /// </summary>
    public static IConfigurationBuilder AddEnvironmentVariablePlaceholders(this IConfigurationBuilder builder)
    {
        var config = builder.Build();
        var processedConfig = new Dictionary<string, string?>();
        
        foreach (var kvp in config.AsEnumerable())
        {
            if (kvp.Value != null && kvp.Value.Contains("${"))
            {
                processedConfig[kvp.Key] = ReplaceEnvironmentVariables(kvp.Value);
            }
        }
        
        if (processedConfig.Count > 0)
        {
            builder.AddInMemoryCollection(processedConfig);
        }
        
        return builder;
    }
    
    /// <summary>
    /// Replaces ${VAR_NAME} placeholders with actual environment variable values
    /// </summary>
    public static string ReplaceEnvironmentVariables(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        
        var pattern = new System.Text.RegularExpressions.Regex(@"\$\{([^}]+)\}");
        
        return pattern.Replace(value, match =>
        {
            var envVarName = match.Groups[1].Value;
            var envValue = Environment.GetEnvironmentVariable(envVarName);
            
            // If environment variable is not set, keep the placeholder (useful for development)
            return envValue ?? match.Value;
        });
    }
    
    /// <summary>
    /// Gets a required configuration value, throwing if not found
    /// </summary>
    public static string GetRequiredValue(this IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"Required configuration key '{key}' is not set.");
        }
        return value;
    }
    
    /// <summary>
    /// Gets a configuration value with environment variable fallback
    /// </summary>
    public static string? GetWithEnvFallback(
        this IConfiguration configuration, 
        string configKey, 
        string envVarName)
    {
        var value = configuration[configKey];
        if (!string.IsNullOrEmpty(value) && !value.Contains("${"))
        {
            return value;
        }
        
        return Environment.GetEnvironmentVariable(envVarName);
    }
}

/// <summary>
/// Configuration provider that processes environment variable placeholders
/// </summary>
public class EnvironmentPlaceholderConfigurationProvider : ConfigurationProvider
{
    private readonly IConfigurationRoot _baseConfig;
    
    public EnvironmentPlaceholderConfigurationProvider(IConfigurationRoot baseConfig)
    {
        _baseConfig = baseConfig;
    }
    
    public override void Load()
    {
        Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var kvp in _baseConfig.AsEnumerable())
        {
            if (kvp.Value != null && kvp.Value.Contains("${"))
            {
                Data[kvp.Key] = ConfigurationExtensions.ReplaceEnvironmentVariables(kvp.Value);
            }
            else
            {
                Data[kvp.Key] = kvp.Value;
            }
        }
    }
}

/// <summary>
/// Configuration source for environment variable placeholder processing
/// </summary>
public class EnvironmentPlaceholderConfigurationSource : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new EnvironmentPlaceholderConfigurationProvider(builder.Build());
    }
}
