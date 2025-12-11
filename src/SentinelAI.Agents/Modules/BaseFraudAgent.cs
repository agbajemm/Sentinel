using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SentinelAI.Agents.Orchestration;
using SentinelAI.Core.DTOs;
using SentinelAI.Core.Enums;
using SentinelAI.Core.Exceptions;
using SentinelAI.Core.Interfaces;

namespace SentinelAI.Agents.Modules;

/// <summary>
/// Base class for all fraud detection agents
/// </summary>
public abstract class BaseFraudAgent : IFraudAgent
{
    protected readonly ILogger _logger;
    protected readonly AzureAIFoundrySettings _settings;
    protected readonly HttpClient _httpClient;
    
    public abstract string AgentId { get; }
    public abstract string Name { get; }
    public abstract AgentLevel Level { get; }
    public abstract ModuleType Module { get; }
    
    protected BaseFraudAgent(
        IOptions<AzureAIFoundrySettings> settings,
        IHttpClientFactory httpClientFactory,
        ILogger logger)
    {
        _settings = settings.Value;
        _httpClient = httpClientFactory.CreateClient("AzureAIFoundry");
        _logger = logger;
    }
    
    public abstract Task<AgentResponse> ProcessAsync(AgentRequest request, CancellationToken cancellationToken = default);
    
    public virtual Task<IEnumerable<AgentTool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Enumerable.Empty<AgentTool>());
    }
    
    public virtual async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple health check - verify we can reach Azure AI Foundry
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_settings.Endpoint}/health");
            request.Headers.Add("api-key", _settings.ApiKey);
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _httpClient.SendAsync(request, cts.Token);
            
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Invokes the Azure AI Foundry agent
    /// </summary>
    protected async Task<AgentResponse> InvokeAzureAgentAsync(
        string azureAgentId,
        string prompt,
        Dictionary<string, object>? context,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogDebug("Invoking Azure agent {AgentId}", azureAgentId);
            
            // Prepare the request to Azure AI Foundry
            var requestBody = new
            {
                agent_id = azureAgentId,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                context = context ?? new Dictionary<string, object>()
            };
            
            var request = new HttpRequestMessage(HttpMethod.Post, 
                $"{_settings.Endpoint}/projects/{_settings.ProjectName}/agents/run")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
            
            request.Headers.Add("api-key", _settings.ApiKey);
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_settings.DefaultTimeoutSeconds));
            
            var response = await _httpClient.SendAsync(request, cts.Token);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new AgentProcessingException(AgentId, 
                    $"Azure agent returned {response.StatusCode}: {errorContent}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var agentResult = JsonSerializer.Deserialize<AzureAgentResponse>(responseContent);
            
            stopwatch.Stop();
            
            return ParseAgentResponse(agentResult, (int)stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            throw new AgentProcessingException(AgentId, "Agent processing timed out");
        }
        catch (Exception ex) when (ex is not AgentProcessingException)
        {
            throw new AgentProcessingException(AgentId, $"Failed to invoke agent: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Simulates agent processing for development/testing when Azure AI Foundry is not available
    /// </summary>
    protected async Task<AgentResponse> SimulateAgentProcessingAsync(
        AgentRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Simulate processing time
        await Task.Delay(Random.Shared.Next(50, 200), cancellationToken);
        
        // Generate simulated risk factors based on transaction characteristics
        var riskFactors = GenerateSimulatedRiskFactors(request.Transaction);
        var riskScore = CalculateSimulatedRiskScore(riskFactors);
        
        stopwatch.Stop();
        
        return new AgentResponse
        {
            AgentId = AgentId,
            IsSuccess = true,
            RiskScore = riskScore,
            Confidence = 0.85m,
            RiskFactors = riskFactors,
            Explanation = GenerateSimulatedExplanation(riskFactors, riskScore),
            ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds
        };
    }
    
    protected virtual List<RiskFactor> GenerateSimulatedRiskFactors(TransactionAnalysisRequest transaction)
    {
        var factors = new List<RiskFactor>();
        
        // High amount check
        if (transaction.Amount > 1000000) // > 1M NGN
        {
            factors.Add(new RiskFactor
            {
                Code = Core.Constants.RiskFactorCodes.HighAmountForCustomer,
                Description = $"Transaction amount ({transaction.Amount:N0} {transaction.Currency}) exceeds typical threshold",
                Weight = 0.3m,
                Contribution = 0.15m,
                Source = AgentId
            });
        }
        
        // Unusual time check
        var hour = transaction.TransactionTime.Hour;
        if (hour < 6 || hour > 22)
        {
            factors.Add(new RiskFactor
            {
                Code = Core.Constants.RiskFactorCodes.UnusualTransactionTime,
                Description = "Transaction initiated during unusual hours",
                Weight = 0.2m,
                Contribution = 0.1m,
                Source = AgentId
            });
        }
        
        return factors;
    }
    
    protected virtual decimal CalculateSimulatedRiskScore(List<RiskFactor> factors)
    {
        if (!factors.Any()) return 0.1m;
        return Math.Min(factors.Sum(f => f.Contribution) + 0.1m, 1.0m);
    }
    
    protected virtual string GenerateSimulatedExplanation(List<RiskFactor> factors, decimal score)
    {
        if (!factors.Any())
            return $"{Name}: No significant risk indicators detected.";
        
        var topFactor = factors.OrderByDescending(f => f.Contribution).First();
        return $"{Name}: Risk score {score:P0}. Primary concern: {topFactor.Description}";
    }
    
    private AgentResponse ParseAgentResponse(AzureAgentResponse? response, int processingTimeMs)
    {
        if (response == null)
        {
            return new AgentResponse
            {
                AgentId = AgentId,
                IsSuccess = false,
                ErrorMessage = "Empty response from Azure agent",
                ProcessingTimeMs = processingTimeMs
            };
        }
        
        return new AgentResponse
        {
            AgentId = AgentId,
            IsSuccess = true,
            RiskScore = response.RiskScore,
            Confidence = response.Confidence,
            RiskFactors = response.RiskFactors ?? new List<RiskFactor>(),
            Explanation = response.Explanation,
            ReasoningChain = response.ReasoningChain,
            ProcessingTimeMs = processingTimeMs
        };
    }
}

/// <summary>
/// Response structure from Azure AI Foundry agent
/// </summary>
public class AzureAgentResponse
{
    public decimal RiskScore { get; set; }
    public decimal Confidence { get; set; }
    public List<RiskFactor>? RiskFactors { get; set; }
    public string? Explanation { get; set; }
    public string? ReasoningChain { get; set; }
}
