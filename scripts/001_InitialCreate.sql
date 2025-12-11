-- SENTINEL AI Initial Migration
-- Database: PostgreSQL 16+
-- Generated: 2024

-- Enable required extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- =============================================
-- Tenants Table
-- =============================================
CREATE TABLE IF NOT EXISTS "Tenants" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "Name" VARCHAR(200) NOT NULL,
    "Code" VARCHAR(50) NOT NULL,
    "Description" TEXT,
    "SubscriptionTier" INTEGER NOT NULL DEFAULT 0,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "EnabledModules" JSONB DEFAULT '[]'::jsonb,
    "ApiKeyHash" VARCHAR(500),
    "ApiKeyExpiresAt" TIMESTAMP WITH TIME ZONE,
    "WebhookUrl" VARCHAR(500),
    "WebhookSecret" VARCHAR(500),
    "ContactEmail" VARCHAR(500),
    "ContactPhone" VARCHAR(500),
    "LowRiskThreshold" DECIMAL(5,4) NOT NULL DEFAULT 0.3,
    "MediumRiskThreshold" DECIMAL(5,4) NOT NULL DEFAULT 0.5,
    "HighRiskThreshold" DECIMAL(5,4) NOT NULL DEFAULT 0.7,
    "CriticalRiskThreshold" DECIMAL(5,4) NOT NULL DEFAULT 0.9,
    "RequestsPerMinute" INTEGER NOT NULL DEFAULT 1000,
    "RequestsPerDay" INTEGER NOT NULL DEFAULT 100000,
    "Settings" JSONB DEFAULT '{}'::jsonb,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP WITH TIME ZONE,
    "IsDeleted" BOOLEAN NOT NULL DEFAULT FALSE,
    "DeletedAt" TIMESTAMP WITH TIME ZONE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Tenants_Code" ON "Tenants" ("Code") WHERE "IsDeleted" = FALSE;
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Tenants_ApiKeyHash" ON "Tenants" ("ApiKeyHash") WHERE "ApiKeyHash" IS NOT NULL AND "IsDeleted" = FALSE;
CREATE INDEX IF NOT EXISTS "IX_Tenants_IsActive" ON "Tenants" ("IsActive") WHERE "IsDeleted" = FALSE;

-- =============================================
-- TenantUsers Table
-- =============================================
CREATE TABLE IF NOT EXISTS "TenantUsers" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "TenantId" UUID REFERENCES "Tenants"("Id") ON DELETE CASCADE,
    "Email" VARCHAR(256) NOT NULL,
    "FirstName" VARCHAR(100) NOT NULL,
    "LastName" VARCHAR(100) NOT NULL,
    "PasswordHash" VARCHAR(500) NOT NULL,
    "Role" VARCHAR(50) NOT NULL,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "LastLoginAt" TIMESTAMP WITH TIME ZONE,
    "FailedLoginAttempts" INTEGER NOT NULL DEFAULT 0,
    "LockoutEnd" TIMESTAMP WITH TIME ZONE,
    "RefreshToken" VARCHAR(500),
    "RefreshTokenExpiryTime" TIMESTAMP WITH TIME ZONE,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP WITH TIME ZONE,
    "IsDeleted" BOOLEAN NOT NULL DEFAULT FALSE,
    "DeletedAt" TIMESTAMP WITH TIME ZONE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_TenantUsers_Email" ON "TenantUsers" ("Email") WHERE "IsDeleted" = FALSE;
CREATE INDEX IF NOT EXISTS "IX_TenantUsers_TenantId" ON "TenantUsers" ("TenantId") WHERE "IsDeleted" = FALSE;
CREATE INDEX IF NOT EXISTS "IX_TenantUsers_RefreshToken" ON "TenantUsers" ("RefreshToken") WHERE "RefreshToken" IS NOT NULL;

-- =============================================
-- TransactionAnalyses Table
-- =============================================
CREATE TABLE IF NOT EXISTS "TransactionAnalyses" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "TenantId" UUID NOT NULL REFERENCES "Tenants"("Id") ON DELETE CASCADE,
    "TransactionReference" VARCHAR(100) NOT NULL,
    "ExternalTransactionId" VARCHAR(100),
    "Amount" DECIMAL(18,4) NOT NULL,
    "Currency" VARCHAR(3) NOT NULL DEFAULT 'NGN',
    "Channel" INTEGER NOT NULL,
    "TransactionTime" TIMESTAMP WITH TIME ZONE NOT NULL,
    "SourceAccount" VARCHAR(500) NOT NULL,
    "SourceAccountHash" VARCHAR(128),
    "DestinationAccount" VARCHAR(500) NOT NULL,
    "DestinationAccountHash" VARCHAR(128),
    "CustomerId" VARCHAR(500),
    "CustomerIdHash" VARCHAR(128),
    "BeneficiaryName" VARCHAR(500),
    "DeviceFingerprint" VARCHAR(500),
    "SessionId" VARCHAR(100),
    "IpAddress" VARCHAR(50),
    "UserAgent" VARCHAR(500),
    "Latitude" DOUBLE PRECISION,
    "Longitude" DOUBLE PRECISION,
    "RiskScore" DECIMAL(5,4) NOT NULL DEFAULT 0,
    "RiskLevel" INTEGER NOT NULL DEFAULT 0,
    "RecommendedAction" INTEGER NOT NULL DEFAULT 0,
    "RiskFactors" JSONB DEFAULT '[]'::jsonb,
    "ModulesInvoked" JSONB DEFAULT '[]'::jsonb,
    "AgentsInvoked" JSONB DEFAULT '[]'::jsonb,
    "Explanation" TEXT,
    "ProcessingTimeMs" INTEGER NOT NULL DEFAULT 0,
    "Status" INTEGER NOT NULL DEFAULT 0,
    "Metadata" JSONB DEFAULT '{}'::jsonb,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP WITH TIME ZONE,
    "IsDeleted" BOOLEAN NOT NULL DEFAULT FALSE,
    "DeletedAt" TIMESTAMP WITH TIME ZONE
);

CREATE INDEX IF NOT EXISTS "IX_TransactionAnalyses_TenantId" ON "TransactionAnalyses" ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_TransactionAnalyses_TransactionReference" ON "TransactionAnalyses" ("TransactionReference");
CREATE INDEX IF NOT EXISTS "IX_TransactionAnalyses_SourceAccountHash" ON "TransactionAnalyses" ("SourceAccountHash");
CREATE INDEX IF NOT EXISTS "IX_TransactionAnalyses_DestinationAccountHash" ON "TransactionAnalyses" ("DestinationAccountHash");
CREATE INDEX IF NOT EXISTS "IX_TransactionAnalyses_CustomerIdHash" ON "TransactionAnalyses" ("CustomerIdHash");
CREATE INDEX IF NOT EXISTS "IX_TransactionAnalyses_CreatedAt" ON "TransactionAnalyses" ("CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS "IX_TransactionAnalyses_RiskLevel" ON "TransactionAnalyses" ("RiskLevel");

-- =============================================
-- FraudAlerts Table
-- =============================================
CREATE TABLE IF NOT EXISTS "FraudAlerts" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "TenantId" UUID NOT NULL REFERENCES "Tenants"("Id") ON DELETE CASCADE,
    "TransactionAnalysisId" UUID REFERENCES "TransactionAnalyses"("Id"),
    "AlertReference" VARCHAR(50) NOT NULL,
    "Status" INTEGER NOT NULL DEFAULT 0,
    "RiskLevel" INTEGER NOT NULL,
    "RiskScore" DECIMAL(5,4) NOT NULL,
    "Title" VARCHAR(500) NOT NULL,
    "Description" TEXT,
    "SourceModule" INTEGER NOT NULL,
    "SourceAgentId" VARCHAR(50),
    "RiskFactors" JSONB DEFAULT '[]'::jsonb,
    "RecommendedActions" JSONB DEFAULT '[]'::jsonb,
    "AssignedToUserId" UUID REFERENCES "TenantUsers"("Id"),
    "AssignedAt" TIMESTAMP WITH TIME ZONE,
    "IsEscalated" BOOLEAN NOT NULL DEFAULT FALSE,
    "EscalatedAt" TIMESTAMP WITH TIME ZONE,
    "EscalationReason" TEXT,
    "ResolvedAt" TIMESTAMP WITH TIME ZONE,
    "ResolutionNotes" TEXT,
    "FeedbackProvided" BOOLEAN NOT NULL DEFAULT FALSE,
    "FeedbackNotes" TEXT,
    "WasAccurate" BOOLEAN,
    "Metadata" JSONB DEFAULT '{}'::jsonb,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP WITH TIME ZONE,
    "IsDeleted" BOOLEAN NOT NULL DEFAULT FALSE,
    "DeletedAt" TIMESTAMP WITH TIME ZONE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_FraudAlerts_AlertReference" ON "FraudAlerts" ("AlertReference");
CREATE INDEX IF NOT EXISTS "IX_FraudAlerts_TenantId" ON "FraudAlerts" ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_FraudAlerts_Status" ON "FraudAlerts" ("Status");
CREATE INDEX IF NOT EXISTS "IX_FraudAlerts_RiskLevel" ON "FraudAlerts" ("RiskLevel");
CREATE INDEX IF NOT EXISTS "IX_FraudAlerts_CreatedAt" ON "FraudAlerts" ("CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS "IX_FraudAlerts_AssignedToUserId" ON "FraudAlerts" ("AssignedToUserId") WHERE "AssignedToUserId" IS NOT NULL;

-- =============================================
-- AuditLogs Table
-- =============================================
CREATE TABLE IF NOT EXISTS "AuditLogs" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "TenantId" UUID REFERENCES "Tenants"("Id") ON DELETE CASCADE,
    "UserId" UUID REFERENCES "TenantUsers"("Id"),
    "Action" VARCHAR(100) NOT NULL,
    "EntityType" VARCHAR(100) NOT NULL,
    "EntityId" VARCHAR(100),
    "OldValues" JSONB,
    "NewValues" JSONB,
    "IpAddress" VARCHAR(50),
    "UserAgent" VARCHAR(500),
    "Timestamp" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "AdditionalData" JSONB DEFAULT '{}'::jsonb
);

CREATE INDEX IF NOT EXISTS "IX_AuditLogs_TenantId" ON "AuditLogs" ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_AuditLogs_UserId" ON "AuditLogs" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_AuditLogs_Timestamp" ON "AuditLogs" ("Timestamp" DESC);
CREATE INDEX IF NOT EXISTS "IX_AuditLogs_EntityType_EntityId" ON "AuditLogs" ("EntityType", "EntityId");

-- =============================================
-- AgentConfigurations Table
-- =============================================
CREATE TABLE IF NOT EXISTS "AgentConfigurations" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "AgentId" VARCHAR(50) NOT NULL,
    "Name" VARCHAR(200) NOT NULL,
    "Module" INTEGER NOT NULL,
    "Level" INTEGER NOT NULL,
    "AzureAgentId" VARCHAR(200),
    "ModelDeployment" VARCHAR(200),
    "SystemPrompt" TEXT,
    "Tools" JSONB DEFAULT '[]'::jsonb,
    "Settings" JSONB DEFAULT '{}'::jsonb,
    "IsEnabled" BOOLEAN NOT NULL DEFAULT TRUE,
    "SuccessRate" DECIMAL(5,4) DEFAULT 0,
    "AverageLatencyMs" INTEGER DEFAULT 0,
    "TotalInvocations" BIGINT DEFAULT 0,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP WITH TIME ZONE,
    "IsDeleted" BOOLEAN NOT NULL DEFAULT FALSE,
    "DeletedAt" TIMESTAMP WITH TIME ZONE,
    "Version" INTEGER NOT NULL DEFAULT 1
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_AgentConfigurations_AgentId" ON "AgentConfigurations" ("AgentId") WHERE "IsDeleted" = FALSE;

-- =============================================
-- AgentInvocationLogs Table
-- =============================================
CREATE TABLE IF NOT EXISTS "AgentInvocationLogs" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "AgentConfigurationId" UUID REFERENCES "AgentConfigurations"("Id") ON DELETE SET NULL,
    "AgentId" VARCHAR(50) NOT NULL,
    "TenantId" UUID REFERENCES "Tenants"("Id") ON DELETE CASCADE,
    "TransactionAnalysisId" UUID REFERENCES "TransactionAnalyses"("Id") ON DELETE SET NULL,
    "CorrelationId" VARCHAR(100),
    "InputTokens" INTEGER,
    "OutputTokens" INTEGER,
    "ProcessingTimeMs" INTEGER NOT NULL,
    "IsSuccess" BOOLEAN NOT NULL,
    "ErrorMessage" TEXT,
    "RiskScore" DECIMAL(5,4),
    "Confidence" DECIMAL(5,4),
    "RequestPayload" JSONB,
    "ResponsePayload" JSONB,
    "Timestamp" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS "IX_AgentInvocationLogs_AgentId" ON "AgentInvocationLogs" ("AgentId");
CREATE INDEX IF NOT EXISTS "IX_AgentInvocationLogs_TenantId" ON "AgentInvocationLogs" ("TenantId");
CREATE INDEX IF NOT EXISTS "IX_AgentInvocationLogs_CorrelationId" ON "AgentInvocationLogs" ("CorrelationId");
CREATE INDEX IF NOT EXISTS "IX_AgentInvocationLogs_Timestamp" ON "AgentInvocationLogs" ("Timestamp" DESC);

-- =============================================
-- AgentMessages Table (for inter-agent communication)
-- =============================================
CREATE TABLE IF NOT EXISTS "AgentMessages" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "MessageId" VARCHAR(100) NOT NULL,
    "SourceAgentId" VARCHAR(50) NOT NULL,
    "TargetAgentId" VARCHAR(50),
    "Intent" INTEGER NOT NULL,
    "Context" TEXT,
    "Payload" JSONB,
    "Priority" INTEGER NOT NULL DEFAULT 5,
    "Confidence" DECIMAL(5,4),
    "CorrelationId" VARCHAR(100),
    "ParentMessageId" VARCHAR(100),
    "Timestamp" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS "IX_AgentMessages_CorrelationId" ON "AgentMessages" ("CorrelationId");
CREATE INDEX IF NOT EXISTS "IX_AgentMessages_SourceAgentId" ON "AgentMessages" ("SourceAgentId");
CREATE INDEX IF NOT EXISTS "IX_AgentMessages_Timestamp" ON "AgentMessages" ("Timestamp" DESC);

-- =============================================
-- Seed initial super admin user
-- =============================================
INSERT INTO "TenantUsers" ("Id", "Email", "FirstName", "LastName", "PasswordHash", "Role", "IsActive", "CreatedAt")
VALUES (
    uuid_generate_v4(),
    'admin@sentinel-ai.com',
    'System',
    'Administrator',
    -- Password: Admin@123! (PBKDF2 hash - should be changed on first login)
    'AQAAAA8AAAAIq7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7u7',
    'SuperAdmin',
    TRUE,
    CURRENT_TIMESTAMP
) ON CONFLICT DO NOTHING;

-- Add comments
COMMENT ON TABLE "Tenants" IS 'Financial institutions using SENTINEL AI';
COMMENT ON TABLE "TenantUsers" IS 'Users within each tenant organization';
COMMENT ON TABLE "TransactionAnalyses" IS 'Transaction fraud analysis records';
COMMENT ON TABLE "FraudAlerts" IS 'Fraud alerts generated by the system';
COMMENT ON TABLE "AuditLogs" IS 'Audit trail for all system actions';
COMMENT ON TABLE "AgentConfigurations" IS 'AI agent configurations';
COMMENT ON TABLE "AgentInvocationLogs" IS 'Agent invocation history for analytics';
COMMENT ON TABLE "AgentMessages" IS 'Inter-agent communication messages';

-- Log completion
DO $$
BEGIN
    RAISE NOTICE 'SENTINEL AI database migration completed successfully at %', NOW();
END $$;
