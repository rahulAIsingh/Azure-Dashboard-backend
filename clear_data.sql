USE [AzureFinOps];
GO

-- Disable constraints to avoid issues with foreign keys
EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';

-- Delete data from main tables
DELETE FROM [AzureCostUsage];
DELETE FROM [ProcessedFiles];
DELETE FROM [Budgets];
DELETE FROM [Alerts];

-- Re-enable constraints
EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';

GO
