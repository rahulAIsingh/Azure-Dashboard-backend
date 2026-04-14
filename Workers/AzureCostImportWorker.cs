using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using AzureFinOps.API.Data;
using AzureFinOps.API.Models;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AzureFinOps.API.Workers
{
    public class AzureCostImportWorker : BackgroundService
    {
        private readonly ILogger<AzureCostImportWorker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        // Run every 6 hours
        private readonly TimeSpan _period = TimeSpan.FromHours(6);

        public AzureCostImportWorker(ILogger<AzureCostImportWorker> logger, IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AzureCostImportWorker starting executing...");
            
            try 
            {
                // Initial run immediately
                await ImportCostsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Initial cost import run failed. Will retry on next schedule.");
            }

            using PeriodicTimer timer = new PeriodicTimer(_period);
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                try 
                {
                    await ImportCostsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Scheduled cost import run failed.");
                }
            }
        }

        private async Task ImportCostsAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AzureCostImportWorker checking for new cost exports at {time}", DateTimeOffset.Now);

            var exportConfigs = _configuration.GetSection("AzureCostExports").Get<AzureCostExportConfig[]>();

            if (exportConfigs == null || exportConfigs.Length == 0)
            {
                _logger.LogWarning("AzureCostExports configuration is missing or empty. Skipping import.");
                return;
            }

            foreach (var exportConfig in exportConfigs)
            {
                var connectionString = exportConfig.StorageConnectionString;
                var containerName = exportConfig.ContainerName;

                if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(containerName) || connectionString.Contains("CONNECTION_STRING_HERE"))
                {
                    _logger.LogInformation("Skipping incomplete or placeholder configuration for container: {Container}", containerName ?? "Unknown");
                    continue;
                }

                await ProcessContainerAsync(connectionString, containerName, exportConfig.SubscriptionNameOverride, stoppingToken);
            }
        }

        private async Task ProcessContainerAsync(string connectionString, string containerName, string? subscriptionNameOverride, CancellationToken stoppingToken)
        {
            _logger.LogInformation("Processing Azure Cost Export for container: {Container}", containerName);

            try
            {
                var blobServiceClient = new BlobServiceClient(connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

                // Check if container exists before proceeding
                if (!await containerClient.ExistsAsync(stoppingToken))
                {
                    _logger.LogWarning("Blob container '{Container}' does not exist.", containerName);
                    return;
                }

                // Need scope to use DbContext which is registered as Scoped
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                await foreach (var blobItem in containerClient.GetBlobsAsync(cancellationToken: stoppingToken))
                {
                    if (!blobItem.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) continue;

                    // Check if file already processed
                    bool alreadyProcessed = dbContext.ProcessedFiles.Any(pf => pf.FileName == blobItem.Name);
                    if (alreadyProcessed)
                    {
                        _logger.LogInformation("Skipping already processed file: {fileName}", blobItem.Name);
                        continue;
                    }

                    _logger.LogInformation("Processing new export file: {fileName}", blobItem.Name);
                    var blobClient = containerClient.GetBlobClient(blobItem.Name);

                    // Download to memory stream
                    var response = await blobClient.DownloadStreamingAsync(cancellationToken: stoppingToken);
                    using var streamReader = new StreamReader(response.Value.Content);
                    
                    var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                    {
                        HasHeaderRecord = true,
                        MissingFieldFound = null,
                        HeaderValidated = null // Don't throw if header fields are missing, but mapped
                    };

                    using var csv = new CsvReader(streamReader, config);
                    var records = csv.GetRecords<AzureCostUsageRecord>().ToList();
                    
                    int importedCount = 0;

                    foreach (var record in records)
                    {
                        var usage = new AzureCostUsage
                        {
                            Id = Guid.NewGuid(),
                            UsageDate = record.UsageDate,
                            SubscriptionName = !string.IsNullOrWhiteSpace(subscriptionNameOverride)
                                ? subscriptionNameOverride
                                : record.SubscriptionName,
                            ResourceGroup = record.ResourceGroup,
                            // ResourceName: actual Azure resource name (last segment of ResourceId, or ProductName fallback)
                            ResourceName = !string.IsNullOrWhiteSpace(record.ActualResourceName)
                                ? record.ActualResourceName
                                : (!string.IsNullOrWhiteSpace(record.ResourceId)
                                    ? System.IO.Path.GetFileName(record.ResourceId.TrimEnd('/'))
                                    : record.ProductName ?? "Unknown"),
                            // ResourcePlan: meter name — e.g. "B4ms", "P1v2", "General Purpose"
                            ResourcePlan = record.MeterName ?? string.Empty,
                            // ResourceType: meter sub-category — e.g. "Premium SSD Managed Disks"
                            ResourceType = record.MeterSubCategory ?? record.ServiceFamily ?? string.Empty,
                            // ServiceName: meter category — e.g. "Virtual Machines", "Storage"
                            ServiceName = record.MeterCategory ?? record.ConsumedService ?? string.Empty,
                            MeterCategory = record.MeterCategory,
                            Location = record.Location,
                            Cost = record.Cost,
                            Currency = record.Currency
                        };

                        dbContext.AzureCostUsage.Add(usage);
                        importedCount++;
                    }

                    // Log the processed file
                    dbContext.ProcessedFiles.Add(new ProcessedFile
                    {
                        FileName = blobItem.Name,
                        RecordsImported = importedCount
                    });

                    await dbContext.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Successfully imported {Count} records from {fileName} in {Container}", importedCount, blobItem.Name, containerName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during Azure Cost import process for container: {Container}", containerName);
            }
        }
        
        // Private DTO mapped specifically to Azure Cost Export CSV columns
        private class AzureCostUsageRecord
        {
            [Name("date")]
            public DateTime UsageDate { get; set; }

            [Name("subscriptionName")]
            public string SubscriptionName { get; set; } = string.Empty;

            [Name("resourceGroupName")]
            public string ResourceGroup { get; set; } = string.Empty;

            // Stored for reference but NOT used for display
            [Name("ResourceId")]
            public string? ResourceId { get; set; }

            // Actual Azure resource name (e.g. "myvm-prod", "sql-server-01")
            [Name("resourceName", "ResourceName")]
            public string? ActualResourceName { get; set; }

            // ──── Friendly display fields ────

            // ResourcePlan → meterName  (e.g. "B4ms", "P1v2", "General Purpose")
            [Name("meterName")]
            public string? MeterName { get; set; }

            // Fallback product name if meterName is absent
            [Name("ProductName", "productName")]
            public string? ProductName { get; set; }

            // ResourceType → meterSubCategory (e.g. "Premium SSD Managed Disks")
            [Name("meterSubCategory")]
            public string? MeterSubCategory { get; set; }

            // Fallback serviceFamily
            [Name("serviceFamily")]
            public string? ServiceFamily { get; set; }

            // ServiceName → meterCategory (e.g. "Storage", "Virtual Machines")
            [Name("meterCategory")]
            public string? MeterCategory { get; set; }

            // Fallback consumedService
            [Name("consumedService")]
            public string? ConsumedService { get; set; }

            [Name("resourceLocation", "location")]
            public string? Location { get; set; }

            [Name("costInBillingCurrency", "costInUsd", "PreTaxCost")]
            public decimal Cost { get; set; }

            [Name("billingCurrency")]
            public string Currency { get; set; } = string.Empty;
        }

        private class AzureCostExportConfig
        {
            public string? StorageConnectionString { get; set; }
            public string? ContainerName { get; set; }
            public string? SubscriptionNameOverride { get; set; }
        }
    }
}
