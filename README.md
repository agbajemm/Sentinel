# SENTINEL AI

**Modular AI-Powered Fraud Detection Platform for Nigerian Financial Institutions**

[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-Proprietary-red)]()
[![Azure AI](https://img.shields.io/badge/Azure%20AI-Foundry-blue)](https://azure.microsoft.com/en-us/products/ai-services/)

## Overview

SENTINEL AI is a comprehensive, enterprise-grade fraud detection platform designed specifically for Nigerian financial institutions. It leverages multi-agent AI architecture powered by Azure AI Foundry to provide real-time transaction monitoring, identity verification, and fraud prevention across multiple channels.

### Key Features

- **Multi-Agent Architecture**: Coordinated AI agents working together for comprehensive fraud detection
- **Modular Design**: Select only the modules you need - pay for what you use
- **Real-Time Analysis**: Sub-200ms transaction analysis for seamless customer experience
- **Nigerian Context**: Built-in support for BVN/NIN verification, POS agent networks, and local fraud patterns
- **Explainable AI**: Natural language explanations for every decision - CBN compliance ready

## Modules

| Module | Tier | Description |
|--------|------|-------------|
| **Transaction Sentinel** | Essential | Real-time transaction monitoring across all channels |
| **POS/Agent Shield** | Essential | Specialized detection for 5.9M+ POS terminals |
| **Identity Fortress** | Advanced | Identity verification and synthetic identity detection |
| **Insider Threat Detector** | Advanced | Employee fraud and collusion detection |
| **Network Intelligence** | Enterprise | Graph-based fraud ring detection |
| **Investigation Assistant** | Enterprise | AI-powered case management |

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    SENTINEL AI API                          │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐         │
│  │ Transaction │  │    POS      │  │  Identity   │         │
│  │  Sentinel   │  │   Shield    │  │  Fortress   │   ...   │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘         │
│         │                │                │                 │
│         └────────────────┼────────────────┘                 │
│                          │                                  │
│              ┌───────────┴───────────┐                      │
│              │   Master Orchestrator  │                     │
│              │    (Azure AI Foundry)  │                     │
│              └───────────────────────┘                      │
├─────────────────────────────────────────────────────────────┤
│  PostgreSQL  │  Redis Cache  │  Azure Service Bus           │
└─────────────────────────────────────────────────────────────┘
```

## Quick Start

### Prerequisites

- .NET 8.0 SDK
- Docker & Docker Compose
- PostgreSQL 16+ (or use Docker)
- Redis 7+ (or use Docker)
- Azure subscription with AI Foundry access

### Local Development

1. **Clone the repository**
   ```bash
   git clone https://github.com/your-org/sentinel-ai.git
   cd sentinel-ai
   ```

2. **Start infrastructure services**
   ```bash
   docker-compose up -d postgres redis
   ```

3. **Configure environment**
   ```bash
   cp src/SentinelAI.Api/appsettings.Development.json.example src/SentinelAI.Api/appsettings.Development.json
   # Edit the file with your Azure AI Foundry credentials
   ```

4. **Run database migrations**
   ```bash
   cd src/SentinelAI.Api
   dotnet ef database update
   ```

5. **Run the API**
   ```bash
   dotnet run
   ```

6. **Access the API**
   - Swagger UI: https://localhost:7001/swagger
   - Health Check: https://localhost:7001/health

### Docker Deployment

```bash
# Build and run all services
docker-compose up --build

# Or run with monitoring tools
docker-compose --profile tools up --build
```

## API Reference

### Authentication

SENTINEL AI supports two authentication methods:

1. **API Key** (for service-to-service)
   ```bash
   curl -H "X-API-Key: sk_live_your_api_key" https://api.sentinel-ai.com/api/v1/analysis/transaction
   ```

2. **JWT Bearer Token** (for user sessions)
   ```bash
   curl -H "Authorization: Bearer your_jwt_token" https://api.sentinel-ai.com/api/v1/analysis/transaction
   ```

### Transaction Analysis

```bash
POST /api/v1/analysis/transaction
Content-Type: application/json

{
  "transactionId": "TXN-2024-001234",
  "amount": 500000,
  "currency": "NGN",
  "channel": "Mobile",
  "sourceAccount": "0123456789",
  "destinationAccount": "9876543210",
  "transactionTime": "2024-01-15T14:30:00Z",
  "deviceFingerprint": "abc123...",
  "ipAddress": "196.168.1.100"
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "transactionId": "TXN-2024-001234",
    "analysisId": "ANL-001234",
    "riskScore": 0.35,
    "riskLevel": "Medium",
    "recommendedAction": "Challenge",
    "riskFactors": [
      {
        "code": "RF001",
        "description": "High transaction amount for customer profile",
        "contribution": 0.15
      }
    ],
    "explanation": "Transaction assessed as Medium risk (35%). Key factor: Transaction amount exceeds typical pattern.",
    "processingTimeMs": 145
  }
}
```

## Configuration

### Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `AZURE_AI_FOUNDRY_ENDPOINT` | Azure AI Foundry endpoint URL | Yes |
| `AZURE_AI_FOUNDRY_API_KEY` | API key for Azure AI Foundry | Yes |
| `AZURE_AI_FOUNDRY_PROJECT_NAME` | Project name in AI Foundry | Yes |
| `DB_PASSWORD` | PostgreSQL password | Yes |
| `REDIS_PASSWORD` | Redis password | No |
| `JWT_SECRET_KEY` | Secret for JWT signing (min 32 chars) | Yes |
| `ENCRYPTION_MASTER_KEY` | Master key for data encryption (min 32 chars) | Yes |

### Agent Configuration

Agent IDs can be configured in `appsettings.json`:

```json
{
  "AzureAIFoundry": {
    "AgentIds": {
      "TransactionAnalyzer": "your-agent-id",
      "BehaviorProfiler": "your-agent-id",
      ...
    }
  }
}
```

## Security

SENTINEL AI implements multiple security layers:

- **Encryption**: AES-256 for data at rest, TLS 1.3 for data in transit
- **Authentication**: JWT + API Key with automatic rotation
- **Authorization**: Role-based access control (RBAC)
- **Data Protection**: Field-level encryption for PII (account numbers, BVN, etc.)
- **Audit Logging**: Comprehensive audit trail for all operations
- **Rate Limiting**: Configurable per-tenant rate limits

## Compliance

- **CBN Fraud Desk**: Automated reporting integration
- **NDPR**: Data minimization and consent management
- **CBN KYC/AML**: Identity verification and sanctions screening
- **PCI DSS**: Secure card data handling

## Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test category
dotnet test --filter "Category=Unit"
```

## Project Structure

```
SentinelAI/
├── src/
│   ├── SentinelAI.Api/           # ASP.NET Core Web API
│   ├── SentinelAI.Core/          # Domain models, interfaces
│   ├── SentinelAI.Application/   # Business logic, services
│   ├── SentinelAI.Infrastructure/# Database, external services
│   └── SentinelAI.Agents/        # AI agent implementations
├── tests/
│   └── SentinelAI.Tests/         # Unit and integration tests
├── scripts/                       # Database and deployment scripts
├── docker-compose.yml            # Local development setup
└── Dockerfile                    # Production container image
```

## Support

For support, contact:
- Email: support@sentinel-ai.com
- Documentation: https://docs.sentinel-ai.com

## License

Proprietary - All Rights Reserved

---

Built with ❤️ for Nigerian Financial Institutions
#   s e n t i n e l a i  
 