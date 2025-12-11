using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SentinelAI.Agents.Modules;
using SentinelAI.Agents.Orchestration;
using SentinelAI.Application.Services;
using AuthServiceImpl = SentinelAI.Application.Services.AuthenticationService;
using IAuthenticationService = SentinelAI.Core.Interfaces.IAuthenticationService;
using SentinelAI.Api.Middleware;
using SentinelAI.Core.Interfaces;
using SentinelAI.Infrastructure.Data;
using SentinelAI.Infrastructure.Repositories;
using SentinelAI.Infrastructure.Security;

namespace SentinelAI.Api.Extensions;

/// <summary>
/// Extension methods for configuring dependency injection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds core infrastructure services
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDbContext<SentinelDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(3);
                    npgsqlOptions.CommandTimeout(30);
                });
        });
        
        // Unit of Work and Repositories
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        
        // Redis Cache
        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnection))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = "SentinelAI:";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }
        
        return services;
    }
    
    /// <summary>
    /// Adds security services
    /// </summary>
    public static IServiceCollection AddSecurityServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Encryption settings
        services.Configure<EncryptionSettings>(
            configuration.GetSection(EncryptionSettings.SectionName));
        services.AddSingleton<IEncryptionService, EncryptionService>();
        
        // JWT settings
        services.Configure<JwtSettings>(
            configuration.GetSection(JwtSettings.SectionName));
        services.AddSingleton<ITokenService, TokenService>();
        
        // API Key service
        services.AddScoped<IApiKeyService, ApiKeyService>();
        
        // Data masking
        services.AddSingleton<IDataMaskingService, DataMaskingService>();
        
        return services;
    }
    
    /// <summary>
    /// Adds authentication and authorization
    /// </summary>
    public static IServiceCollection AddAuthenticationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>();
        
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings?.Secret ?? "DefaultDevSecretKeyThatIsLongEnough32Chars")),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings?.Issuer ?? "SentinelAI",
                ValidateAudience = true,
                ValidAudience = jwtSettings?.Audience ?? "SentinelAI.Clients",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
            
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                    {
                        context.Response.Headers.Append("Token-Expired", "true");
                    }
                    return Task.CompletedTask;
                }
            };
        })
        .AddApiKeyAuthentication();
        
        services.AddAuthorization(options =>
        {
            options.AddPolicy("ApiKeyOrJwt", policy =>
            {
                policy.AddAuthenticationSchemes("ApiKey", JwtBearerDefaults.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
            });
            
            options.AddPolicy("TenantAdmin", policy =>
            {
                policy.RequireRole(Core.Constants.Roles.TenantAdmin, Core.Constants.Roles.SuperAdmin);
            });
            
            options.AddPolicy("FraudAnalyst", policy =>
            {
                policy.RequireRole(
                    Core.Constants.Roles.FraudAnalyst,
                    Core.Constants.Roles.Investigator,
                    Core.Constants.Roles.TenantAdmin,
                    Core.Constants.Roles.SuperAdmin);
            });
        });
        
        return services;
    }
    
    /// <summary>
    /// Adds application services
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<IFraudDetectionService, FraudDetectionService>();
        services.AddScoped<IAuthenticationService, AuthServiceImpl>();
        
        // Add validators from assembly
        services.AddValidatorsFromAssemblyContaining<Application.Validators.TransactionAnalysisRequestValidator>();
        
        return services;
    }
    
    /// <summary>
    /// Adds AI agent services
    /// </summary>
    public static IServiceCollection AddAgentServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Azure AI Foundry settings
        services.Configure<AzureAIFoundrySettings>(
            configuration.GetSection(AzureAIFoundrySettings.SectionName));
        
        // HTTP client for Azure AI Foundry
        services.AddHttpClient("AzureAIFoundry", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        
        // Register Transaction Sentinel agents
        services.AddScoped<SentinelAI.Agents.Modules.TransactionSentinel.TransactionAnalyzerAgent>();
        services.AddScoped<SentinelAI.Agents.Modules.TransactionSentinel.BehaviorProfilerAgent>();
        services.AddScoped<SentinelAI.Agents.Modules.TransactionSentinel.RiskScorerAgent>();
        
        // Register POS Agent Shield agents  
        services.AddScoped<SentinelAI.Agents.Modules.POSAgentShield.TerminalMonitorAgent>();
        services.AddScoped<SentinelAI.Agents.Modules.POSAgentShield.MuleHunterAgent>();
        
        // Register Identity Fortress agents
        // TODO: Uncomment when Identity Fortress agents are implemented in SentinelAI.Agents
        // services.AddScoped<SentinelAI.Agents.Modules.IdentityFortress.IdentityVerifierAgent>();
        // services.AddScoped<SentinelAI.Agents.Modules.IdentityFortress.SyntheticDetectorAgent>();
        
        // Register modules
        services.AddScoped<IFraudModule, SentinelAI.Agents.Modules.TransactionSentinel.TransactionSentinelModule>();
        services.AddScoped<IFraudModule, SentinelAI.Agents.Modules.POSAgentShield.POSAgentShieldModule>();
        // services.AddScoped<IFraudModule, SentinelAI.Agents.Modules.IdentityFortress.IdentityFortressModule>();
        
        // Register orchestrator
        services.AddScoped<IAgentOrchestrator, MasterOrchestrator>();
        
        return services;
    }
    
    /// <summary>
    /// Adds rate limiting
    /// </summary>
    public static IServiceCollection AddRateLimitingServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var rateLimitConfig = configuration.GetSection("RateLimiting");
        var enableRateLimiting = rateLimitConfig.GetValue<bool>("EnableRateLimiting");
        
        if (!enableRateLimiting)
        {
            return services;
        }
        
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            
            options.AddPolicy("fixed", httpContext =>
            {
                var tenantId = httpContext.User.FindFirst(Core.Constants.ClaimTypes.TenantId)?.Value;
                
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: tenantId ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitConfig.GetValue<int>("PermitLimit"),
                        Window = TimeSpan.FromSeconds(rateLimitConfig.GetValue<int>("WindowSeconds")),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = rateLimitConfig.GetValue<int>("QueueLimit")
                    });
            });
            
            options.AddPolicy("analysis", httpContext =>
            {
                var tenantId = httpContext.User.FindFirst(Core.Constants.ClaimTypes.TenantId)?.Value;
                
                return RateLimitPartition.GetTokenBucketLimiter(
                    partitionKey: tenantId ?? "anonymous",
                    factory: _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 100,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                        TokensPerPeriod = 10,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 50
                    });
            });
            
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString();
                }
                
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    message = "Rate limit exceeded. Please try again later.",
                    retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var ra) 
                        ? ra.TotalSeconds 
                        : 60
                }, token);
            };
        });
        
        return services;
    }
    
    /// <summary>
    /// Adds Swagger/OpenAPI documentation
    /// </summary>
    public static IServiceCollection AddSwaggerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var swaggerConfig = configuration.GetSection("Swagger");
        
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = swaggerConfig["Title"] ?? "SENTINEL AI API",
                Description = swaggerConfig["Description"] ?? "Fraud Detection Platform API",
                Version = swaggerConfig["Version"] ?? "v1",
                Contact = new OpenApiContact
                {
                    Name = swaggerConfig["ContactName"],
                    Email = swaggerConfig["ContactEmail"]
                }
            });
            
            // JWT Authentication
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT Authorization header using the Bearer scheme."
            });
            
            // API Key Authentication
            options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
            {
                Name = "X-API-Key",
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "API Key authentication"
            });
            
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                },
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "ApiKey"
                        }
                    },
                    Array.Empty<string>()
                }
            });
            
            // Include XML comments if available
            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });
        
        return services;
    }
    
    /// <summary>
    /// Adds CORS configuration
    /// </summary>
    public static IServiceCollection AddCorsServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
            ?? new[] { "http://localhost:3000" };
        
        services.AddCors(options =>
        {
            options.AddPolicy("Default", builder =>
            {
                builder
                    .WithOrigins(allowedOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
                    .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
            });
            
            options.AddPolicy("AllowAll", builder =>
            {
                builder
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });
        
        return services;
    }
    
    /// <summary>
    /// Adds health checks
    /// </summary>
    public static IServiceCollection AddHealthCheckServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var healthChecks = services.AddHealthChecks();
        
        // Database health check
        var dbConnection = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(dbConnection))
        {
            healthChecks.AddNpgSql(dbConnection, name: "database", tags: new[] { "db", "sql", "postgresql" });
        }
        
        // Redis health check
        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnection))
        {
            healthChecks.AddRedis(redisConnection, name: "redis", tags: new[] { "cache", "redis" });
        }
        
        return services;
    }
}
