-- SENTINEL AI Database Initialization Script
-- PostgreSQL 16+

-- Enable required extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Create schema (optional, using public by default)
-- CREATE SCHEMA IF NOT EXISTS sentinel;

-- Grant permissions
GRANT ALL PRIVILEGES ON DATABASE sentinel_ai TO sentinel_user;

-- Create indexes for common queries (EF Core will create tables via migrations)
-- These are placeholder comments - actual indexes will be created by EF migrations

-- Add comment for documentation
COMMENT ON DATABASE sentinel_ai IS 'SENTINEL AI - Modular AI-Powered Fraud Detection Platform';

-- Log initialization
DO $$
BEGIN
    RAISE NOTICE 'SENTINEL AI database initialized successfully at %', NOW();
END $$;
