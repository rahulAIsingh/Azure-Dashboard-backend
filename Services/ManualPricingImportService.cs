using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Xml;
using AzureFinOps.API.Data;
using AzureFinOps.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AzureFinOps.API.Services
{
    public interface IManualPricingImportService
    {
        Task<ManualPricingImportSummary> ImportAsync(CancellationToken cancellationToken = default);
        Task<ManualPricingImportSummary> ImportWorkbookAsync(ManualPricingUploadRequest request, CancellationToken cancellationToken = default);
    }

    public sealed class ManualPricingImportService : IManualPricingImportService
    {
        private const string ManualImportMarker = "Manual Pricing Import";
        private const string EmptyResourceGroupMarker = "(no-resource-group)";
        private const string LegacyBillingExportFormat = "LegacyBillingExport";
        private const string SgBillingExportFormat = "SgBillingExport";
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ManualPricingImportService> _logger;
        private readonly IWebHostEnvironment _environment;

        public ManualPricingImportService(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<ManualPricingImportService> logger,
            IWebHostEnvironment environment)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _environment = environment;
        }

        public async Task<ManualPricingImportSummary> ImportAsync(CancellationToken cancellationToken = default)
        {
            var config = _configuration.GetSection("ManualPricingImport").Get<ManualPricingImportOptions>()
                ?? throw new InvalidOperationException("ManualPricingImport configuration is missing.");

            if (string.IsNullOrWhiteSpace(config.WorkbookPath))
            {
                throw new InvalidOperationException("ManualPricingImport:WorkbookPath is required.");
            }

            if (!File.Exists(config.WorkbookPath))
            {
                throw new FileNotFoundException("Manual pricing workbook was not found.", config.WorkbookPath);
            }

            await using var workbookStream = File.Open(config.WorkbookPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return await ImportCoreAsync(workbookStream, config, config.WorkbookPath, cancellationToken);
        }

        public async Task<ManualPricingImportSummary> ImportWorkbookAsync(ManualPricingUploadRequest request, CancellationToken cancellationToken = default)
        {
            if (request.WorkbookStream is null)
            {
                throw new InvalidOperationException("Manual pricing workbook stream is required.");
            }

            if (string.IsNullOrWhiteSpace(request.SubscriptionName))
            {
                throw new InvalidOperationException("Subscription name is required for manual pricing import.");
            }

            var defaults = _configuration.GetSection("ManualPricingImport").Get<ManualPricingImportOptions>() ?? new ManualPricingImportOptions();
            var config = new ManualPricingImportOptions
            {
                Enabled = false,
                Format = string.IsNullOrWhiteSpace(request.Format) ? SgBillingExportFormat : request.Format,
                WorkbookPath = request.WorkbookName,
                SubscriptionName = request.SubscriptionName,
                EffectiveUsageDate = request.EffectiveUsageDate ?? string.Empty,
                Currency = string.IsNullOrWhiteSpace(request.Currency) ? defaults.Currency : request.Currency,
                OnlyBenefitType = defaults.OnlyBenefitType,
                ReportDirectory = defaults.ReportDirectory,
                Overrides = defaults.Overrides ?? new List<ManualPricingImportOverride>()
            };

            if (request.WorkbookStream.CanSeek)
            {
                request.WorkbookStream.Position = 0;
            }

            return await ImportCoreAsync(request.WorkbookStream, config, request.WorkbookName, cancellationToken);
        }

        private async Task<ManualPricingImportSummary> ImportCoreAsync(Stream workbookStream, ManualPricingImportOptions config, string workbookLabel, CancellationToken cancellationToken)
        {
            if (!TryResolveEffectiveUsageDate(config, out var configuredEffectiveUsageDate))
            {
                configuredEffectiveUsageDate = null;
            }

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var reportDirectory = ResolveReportDirectory(config.ReportDirectory);
            Directory.CreateDirectory(reportDirectory);

            var rawRows = ReadWorkbookRows(workbookStream, config.Format);
            var eligibleRows = new List<ExcelPricingRow>();
            var skippedRows = new List<SkippedExcelRow>();

            foreach (var rawRow in rawRows)
            {
                var mapped = TryMapRow(rawRow, config, skippedRows);
                if (mapped is not null)
                {
                    eligibleRows.Add(mapped);
                }
            }

            var aggregatedRows = AggregateRows(eligibleRows);
            var effectiveUsageDate = configuredEffectiveUsageDate ?? ResolveEffectiveUsageDateFromRows(aggregatedRows);
            var resourceIndex = await BuildResourceIndexAsync(config.SubscriptionName, cancellationToken);

            var insertedRows = new List<InsertedPricingRowReport>();
            var unmatchedRows = new List<UnmatchedPricingRowReport>();
            var summary = new ManualPricingImportSummary
            {
                WorkbookPath = workbookLabel,
                SubscriptionName = config.SubscriptionName,
                EffectiveUsageDate = effectiveUsageDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Currency = config.Currency,
                TotalExcelRowsRead = rawRows.Count,
                EligibleChargeRows = eligibleRows.Count,
                DistinctAggregatedResources = aggregatedRows.Count,
                SkippedRows = skippedRows.Count
            };

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                foreach (var aggregated in aggregatedRows.Values.OrderBy(r => r.SubscriptionName).ThenBy(r => r.ResourceGroup).ThenBy(r => r.ResourceName))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!resourceIndex.TryGetValue(aggregated.Key, out var canonical))
                    {
                        unmatchedRows.Add(new UnmatchedPricingRowReport
                        {
                            SubscriptionName = aggregated.SubscriptionName,
                            ResourceGroup = aggregated.ResourceGroup,
                            ResourceName = aggregated.ResourceName,
                            TotalAmtInr = aggregated.TotalAmtInr,
                            LineItemCount = aggregated.LineItemCount,
                            Locations = aggregated.Locations.OrderBy(v => v).ToArray(),
                            ProductNames = aggregated.ProductNames.OrderBy(v => v).ToArray(),
                            MeterNames = aggregated.MeterNames.OrderBy(v => v).ToArray(),
                            UsageStartDate = aggregated.MinUsageStartDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                            UsageEndDate = aggregated.MaxUsageEndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                            Reason = "No existing AzureCostUsage resource matched the normalized subscription/resource group/resource name key."
                        });
                        continue;
                    }

                    var duplicateExists = await _context.AzureCostUsage.AnyAsync(
                        row => row.UsageDate == effectiveUsageDate
                            && row.Currency == config.Currency
                            && row.SubscriptionName == canonical.SubscriptionName
                            && row.ResourceGroup == canonical.ResourceGroup
                            && row.ResourceName == canonical.ResourceName
                            && row.MeterCategory == ManualImportMarker,
                        cancellationToken);

                    if (duplicateExists)
                    {
                        skippedRows.Add(new SkippedExcelRow
                        {
                            RowNumber = null,
                            SubscriptionName = aggregated.SubscriptionName,
                            ResourceGroup = aggregated.ResourceGroup,
                            ResourceName = aggregated.ResourceName,
                            AmtInr = aggregated.TotalAmtInr.ToString(CultureInfo.InvariantCulture),
                            Reason = "A synthetic manual pricing row already exists for this resource and month."
                        });
                        continue;
                    }

                    var usage = new AzureCostUsage
                    {
                        Id = Guid.NewGuid(),
                        UsageDate = effectiveUsageDate,
                        SubscriptionName = canonical.SubscriptionName,
                        ResourceGroup = canonical.ResourceGroup,
                        ResourceName = canonical.ResourceName,
                        ResourceType = canonical.ResourceType,
                        ServiceName = canonical.ServiceName,
                        ResourcePlan = canonical.ResourcePlan,
                        MeterCategory = ManualImportMarker,
                        Location = canonical.Location,
                        Cost = aggregated.TotalAmtInr,
                        Currency = config.Currency
                    };

                    _context.AzureCostUsage.Add(usage);

                    insertedRows.Add(new InsertedPricingRowReport
                    {
                        InsertedRowId = usage.Id,
                        SubscriptionName = usage.SubscriptionName,
                        ResourceGroup = usage.ResourceGroup,
                        ResourceName = usage.ResourceName ?? string.Empty,
                        TotalAmtInr = aggregated.TotalAmtInr,
                        LineItemCount = aggregated.LineItemCount,
                        UsageDate = usage.UsageDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        CopiedMetadata = new CanonicalMetadataReport
                        {
                            ResourceType = usage.ResourceType,
                            ServiceName = usage.ServiceName,
                            ResourcePlan = usage.ResourcePlan,
                            MeterCategory = usage.MeterCategory,
                            Location = usage.Location
                        }
                    });
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            summary.MatchedResourcesInserted = insertedRows.Count;
            summary.UnmatchedResources = unmatchedRows.Count;
            summary.SkippedRows = skippedRows.Count;
            summary.TotalImportedAmtInr = insertedRows.Sum(r => r.TotalAmtInr);

            await WriteReportAsync(Path.Combine(reportDirectory, $"matched-inserted-{timestamp}.json"), insertedRows, cancellationToken);
            await WriteReportAsync(Path.Combine(reportDirectory, $"unmatched-excel-{timestamp}.json"), unmatchedRows, cancellationToken);
            await WriteReportAsync(Path.Combine(reportDirectory, $"skipped-invalid-{timestamp}.json"), skippedRows, cancellationToken);
            await WriteReportAsync(Path.Combine(reportDirectory, $"summary-{timestamp}.json"), summary, cancellationToken);

            _logger.LogInformation(
                "Manual pricing import completed. RowsRead={RowsRead}, Eligible={Eligible}, AggregatedResources={Aggregated}, Inserted={Inserted}, Unmatched={Unmatched}, Skipped={Skipped}, TotalImportedAmtInr={TotalImportedAmtInr}",
                summary.TotalExcelRowsRead,
                summary.EligibleChargeRows,
                summary.DistinctAggregatedResources,
                summary.MatchedResourcesInserted,
                summary.UnmatchedResources,
                summary.SkippedRows,
                summary.TotalImportedAmtInr);

            return summary;
        }

        private static bool TryResolveEffectiveUsageDate(ManualPricingImportOptions config, out DateTime? effectiveUsageDate)
        {
            effectiveUsageDate = null;

            if (string.IsNullOrWhiteSpace(config.EffectiveUsageDate))
            {
                return true;
            }

            if (!DateTime.TryParse(config.EffectiveUsageDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
            {
                return false;
            }

            effectiveUsageDate = parsed.Date;
            return true;
        }

        private static DateTime ResolveEffectiveUsageDateFromRows(IReadOnlyDictionary<string, AggregatedPricingRow> aggregatedRows)
        {
            var derived = aggregatedRows.Values
                .Select(row => row.MaxUsageEndDate ?? row.MinUsageStartDate)
                .Where(date => date.HasValue)
                .Select(date => date!.Value.Date)
                .DefaultIfEmpty()
                .Max();

            if (derived == default)
            {
                throw new InvalidOperationException("Manual pricing import could not derive an effective usage date from the workbook. Provide EffectiveUsageDate explicitly.");
            }

            return derived;
        }

        private string ResolveReportDirectory(string? configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return Path.Combine(_environment.ContentRootPath, "import-reports");
            }

            return Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, configuredPath));
        }

        private static async Task WriteReportAsync<T>(string path, T payload, CancellationToken cancellationToken)
        {
            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, payload, JsonOptions, cancellationToken);
        }

        private Dictionary<string, AggregatedPricingRow> AggregateRows(IEnumerable<ExcelPricingRow> rows)
        {
            var map = new Dictionary<string, AggregatedPricingRow>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                if (!map.TryGetValue(row.Key, out var aggregate))
                {
                    aggregate = new AggregatedPricingRow
                    {
                        Key = row.Key,
                        SubscriptionName = row.SubscriptionName,
                        ResourceGroup = row.ResourceGroup,
                        ResourceName = row.ResourceName
                    };
                    map[row.Key] = aggregate;
                }

                aggregate.TotalAmtInr += row.AmtInr;
                aggregate.LineItemCount++;
                aggregate.Locations.Add(row.Location);
                aggregate.ProductNames.Add(row.ProductName);
                aggregate.MeterNames.Add(row.MeterName);

                if (row.UsageStartDate.HasValue && (!aggregate.MinUsageStartDate.HasValue || row.UsageStartDate < aggregate.MinUsageStartDate))
                {
                    aggregate.MinUsageStartDate = row.UsageStartDate;
                }

                if (row.UsageEndDate.HasValue && (!aggregate.MaxUsageEndDate.HasValue || row.UsageEndDate > aggregate.MaxUsageEndDate))
                {
                    aggregate.MaxUsageEndDate = row.UsageEndDate;
                }
            }

            return map;
        }

        private async Task<Dictionary<string, CanonicalResourceRow>> BuildResourceIndexAsync(string subscriptionName, CancellationToken cancellationToken)
        {
            var rows = await _context.AzureCostUsage
                .Where(row => row.SubscriptionName != null && row.SubscriptionName.ToLower() == subscriptionName.ToLower())
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(row => NormalizeKey(row.SubscriptionName, row.ResourceGroup, row.ResourceName))
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var canonical = group
                            .OrderByDescending(row => row.UsageDate)
                            .ThenByDescending(row => !string.IsNullOrWhiteSpace(row.ServiceName))
                            .ThenByDescending(row => !string.IsNullOrWhiteSpace(row.Location))
                            .ThenBy(row => row.Id)
                            .First();

                        return new CanonicalResourceRow
                        {
                            SubscriptionName = canonical.SubscriptionName,
                            ResourceGroup = canonical.ResourceGroup,
                            ResourceName = canonical.ResourceName,
                            ResourceType = canonical.ResourceType,
                            ServiceName = canonical.ServiceName,
                            ResourcePlan = canonical.ResourcePlan,
                            MeterCategory = canonical.MeterCategory,
                            Location = canonical.Location
                        };
                    },
                    StringComparer.OrdinalIgnoreCase);
        }

        private ExcelPricingRow? TryMapRow(RawExcelRow rawRow, ManualPricingImportOptions config, List<SkippedExcelRow> skippedRows)
        {
            return string.Equals(config.Format, SgBillingExportFormat, StringComparison.OrdinalIgnoreCase)
                ? TryMapSgBillingExportRow(rawRow, config, skippedRows)
                : TryMapLegacyBillingExportRow(rawRow, config, skippedRows);
        }

        private ExcelPricingRow? TryMapLegacyBillingExportRow(RawExcelRow rawRow, ManualPricingImportOptions config, List<SkippedExcelRow> skippedRows)
        {
            var subscription = rawRow.Get("Publisher Subscription Name");
            var resourceGroup = rawRow.Get("Resource Group Name");
            var resourceName = rawRow.Get("Resource Name");
            var benefitType = rawRow.Get("BenefitType");
            var amtInrRaw = rawRow.Get("Amt INR");

            if (!string.Equals(subscription, config.SubscriptionName, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!string.Equals(benefitType, config.OnlyBenefitType, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var overrideMatch = ResolveOverride(config, subscription, resourceGroup, resourceName);
            if (overrideMatch is not null)
            {
                resourceGroup = overrideMatch.TargetResourceGroup;
                resourceName = overrideMatch.TargetResourceName;
            }

            if (string.IsNullOrWhiteSpace(resourceName))
            {
                skippedRows.Add(new SkippedExcelRow
                {
                    RowNumber = rawRow.RowNumber,
                    SubscriptionName = subscription,
                    ResourceGroup = resourceGroup,
                    ResourceName = resourceName,
                    AmtInr = amtInrRaw,
                    Reason = "Resource Name is empty."
                });
                return null;
            }

            if (resourceGroup is null)
            {
                resourceGroup = string.Empty;
            }

            if (!TryParseDecimal(amtInrRaw, out var amtInr))
            {
                skippedRows.Add(new SkippedExcelRow
                {
                    RowNumber = rawRow.RowNumber,
                    SubscriptionName = subscription,
                    ResourceGroup = resourceGroup,
                    ResourceName = resourceName,
                    AmtInr = amtInrRaw,
                    Reason = "Amt INR is missing or not a valid decimal."
                });
                return null;
            }

            return new ExcelPricingRow
            {
                RowNumber = rawRow.RowNumber,
                SubscriptionName = subscription.Trim(),
                ResourceGroup = resourceGroup.Trim(),
                ResourceName = resourceName.Trim(),
                ProductName = rawRow.Get("Product Name").Trim(),
                MeterName = rawRow.Get("Meter Name").Trim(),
                Location = rawRow.Get("Location").Trim(),
                UsageStartDate = TryParseExcelDate(rawRow.Get("Usage Start Date")),
                UsageEndDate = TryParseExcelDate(rawRow.Get("Usage End Date")),
                AmtInr = amtInr,
                ManualOverrideName = overrideMatch?.Name
            };
        }

        private ExcelPricingRow? TryMapSgBillingExportRow(RawExcelRow rawRow, ManualPricingImportOptions config, List<SkippedExcelRow> skippedRows)
        {
            var subscription = rawRow.Get("subscriptionFriendlyName");
            var resourceGroup = rawRow.Get("resourceGroup");
            var resourceName = rawRow.Get("resourceName");
            var amtInrRaw = rawRow.Get("INR Amount");

            if (!string.Equals(subscription, config.SubscriptionName, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var overrideMatch = ResolveOverride(config, subscription, resourceGroup, resourceName);
            if (overrideMatch is not null)
            {
                resourceGroup = overrideMatch.TargetResourceGroup;
                resourceName = overrideMatch.TargetResourceName;
            }

            if (string.IsNullOrWhiteSpace(resourceName))
            {
                skippedRows.Add(new SkippedExcelRow
                {
                    RowNumber = rawRow.RowNumber,
                    SubscriptionName = subscription,
                    ResourceGroup = resourceGroup,
                    ResourceName = resourceName,
                    AmtInr = amtInrRaw,
                    Reason = "Resource Name is empty."
                });
                return null;
            }

            if (resourceGroup is null)
            {
                resourceGroup = string.Empty;
            }

            if (!TryParseDecimal(amtInrRaw, out var amtInr))
            {
                skippedRows.Add(new SkippedExcelRow
                {
                    RowNumber = rawRow.RowNumber,
                    SubscriptionName = subscription,
                    ResourceGroup = resourceGroup,
                    ResourceName = resourceName,
                    AmtInr = amtInrRaw,
                    Reason = "INR Amount is missing or not a valid decimal."
                });
                return null;
            }

            return new ExcelPricingRow
            {
                RowNumber = rawRow.RowNumber,
                SubscriptionName = subscription.Trim(),
                ResourceGroup = resourceGroup.Trim(),
                ResourceName = resourceName.Trim(),
                ProductName = rawRow.Get("meterCategory").Trim(),
                MeterName = rawRow.Get("meterName").Trim(),
                Location = rawRow.Get("resourceLocation").Trim(),
                UsageStartDate = TryParseExcelDate(rawRow.Get("usageDate")),
                UsageEndDate = TryParseExcelDate(rawRow.Get("usageDate")),
                AmtInr = amtInr,
                ManualOverrideName = overrideMatch?.Name
            };
        }

        private static ManualPricingImportOverride? ResolveOverride(
            ManualPricingImportOptions config,
            string? subscription,
            string? resourceGroup,
            string? resourceName)
        {
            if (config.Overrides is null || config.Overrides.Count == 0)
            {
                return null;
            }

            foreach (var manualOverride in config.Overrides)
            {
                if (!string.Equals(subscription?.Trim(), manualOverride.SourceSubscriptionName?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.Equals((resourceGroup ?? string.Empty).Trim(), (manualOverride.SourceResourceGroup ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.Equals(resourceName?.Trim(), manualOverride.SourceResourceName?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return manualOverride;
            }

            return null;
        }

        private static bool TryParseDecimal(string? raw, out decimal value)
        {
            return decimal.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
        }

        private static DateTime? TryParseExcelDate(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var oaDate))
            {
                return DateTime.FromOADate(oaDate).Date;
            }

            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
            {
                return parsed.Date;
            }

            return null;
        }

        private static List<RawExcelRow> ReadWorkbookRows(Stream workbookStream, string? format)
        {
            using var buffer = new MemoryStream();
            workbookStream.CopyTo(buffer);
            buffer.Position = 0;

            using var archive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: false);
            var sharedStrings = ReadSharedStrings(archive);
            var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml")
                ?? throw new InvalidOperationException("The workbook does not contain xl/worksheets/sheet1.xml.");

            using var sheetStream = sheetEntry.Open();
            var document = new XmlDocument();
            document.Load(sheetStream);

            var manager = new XmlNamespaceManager(document.NameTable);
            manager.AddNamespace("x", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");

            var rows = document.SelectNodes("//x:sheetData/x:row", manager)
                ?? throw new InvalidOperationException("The workbook sheet contains no rows.");

            if (rows.Count < 2)
            {
                throw new InvalidOperationException("The workbook sheet does not contain any data rows.");
            }

            var headerRow = rows[0]!;
            var headerMap = headerRow.SelectNodes("./x:c", manager)!
                .Cast<XmlNode>()
                .ToDictionary(
                    cell => ExtractColumnName(cell.Attributes?["r"]?.Value),
                    cell => ReadCellValue(cell, sharedStrings),
                    StringComparer.OrdinalIgnoreCase);

            var requiredHeaders = GetRequiredHeaders(format);

            foreach (var requiredHeader in requiredHeaders)
            {
                if (!headerMap.Values.Contains(requiredHeader, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Workbook is missing required header '{requiredHeader}'.");
                }
            }

            var rowsOut = new List<RawExcelRow>();
            foreach (var rowNode in rows.Cast<XmlNode>().Skip(1))
            {
                var cells = rowNode.SelectNodes("./x:c", manager)!.Cast<XmlNode>().ToList();
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var cell in cells)
                {
                    var columnName = ExtractColumnName(cell.Attributes?["r"]?.Value);
                    if (!headerMap.TryGetValue(columnName, out var headerText))
                    {
                        continue;
                    }

                    values[headerText] = ReadCellValue(cell, sharedStrings);
                }

                rowsOut.Add(new RawExcelRow
                {
                    RowNumber = int.TryParse(rowNode.Attributes?["r"]?.Value, out var rowNumber) ? rowNumber : null,
                    Values = values
                });
            }

            return rowsOut;
        }

        private static string[] GetRequiredHeaders(string? format)
        {
            if (string.Equals(format, SgBillingExportFormat, StringComparison.OrdinalIgnoreCase))
            {
                return
                [
                    "subscriptionFriendlyName",
                    "resourceGroup",
                    "resourceName",
                    "usageDate",
                    "INR Amount"
                ];
            }

            return
            [
                "Publisher Subscription Name",
                "Resource Group Name",
                "Resource Name",
                "Amt INR",
                "BenefitType"
            ];
        }

        private static Dictionary<int, string> ReadSharedStrings(ZipArchive archive)
        {
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry is null)
            {
                return new Dictionary<int, string>();
            }

            using var stream = entry.Open();
            var document = new XmlDocument();
            document.Load(stream);

            var manager = new XmlNamespaceManager(document.NameTable);
            manager.AddNamespace("x", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");

            var map = new Dictionary<int, string>();
            var index = 0;
            foreach (var sharedItem in document.SelectNodes("//x:si", manager)!.Cast<XmlNode>())
            {
                var text = string.Concat(sharedItem.SelectNodes(".//x:t", manager)!.Cast<XmlNode>().Select(node => node.InnerText));
                map[index++] = text;
            }

            return map;
        }

        private static string ReadCellValue(XmlNode cell, IReadOnlyDictionary<int, string> sharedStrings)
        {
            var type = cell.Attributes?["t"]?.Value;
            var valueNode = cell.SelectSingleNode("./*[local-name()='v']");
            if (valueNode is null)
            {
                return string.Empty;
            }

            var rawValue = valueNode.InnerText;
            if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sharedIndex)
                && sharedStrings.TryGetValue(sharedIndex, out var sharedString))
            {
                return sharedString;
            }

            return rawValue;
        }

        private static string ExtractColumnName(string? cellReference)
        {
            if (string.IsNullOrWhiteSpace(cellReference))
            {
                return string.Empty;
            }

            return new string(cellReference.TakeWhile(char.IsLetter).ToArray());
        }

        private static string NormalizeKey(string? subscriptionName, string? resourceGroup, string? resourceName)
        {
            if (string.IsNullOrWhiteSpace(subscriptionName)
                || string.IsNullOrWhiteSpace(resourceName))
            {
                return string.Empty;
            }

            return string.Join("|",
                subscriptionName.Trim().ToLowerInvariant(),
                string.IsNullOrWhiteSpace(resourceGroup) ? EmptyResourceGroupMarker : resourceGroup.Trim().ToLowerInvariant(),
                resourceName.Trim().ToLowerInvariant());
        }

        private sealed class RawExcelRow
        {
            public int? RowNumber { get; init; }
            public Dictionary<string, string> Values { get; init; } = new(StringComparer.OrdinalIgnoreCase);

            public string Get(string key) => Values.TryGetValue(key, out var value) ? value : string.Empty;
        }

        private sealed class ExcelPricingRow
        {
            public int? RowNumber { get; init; }
            public string SubscriptionName { get; init; } = string.Empty;
            public string ResourceGroup { get; init; } = string.Empty;
            public string ResourceName { get; init; } = string.Empty;
            public string ProductName { get; init; } = string.Empty;
            public string MeterName { get; init; } = string.Empty;
            public string Location { get; init; } = string.Empty;
            public DateTime? UsageStartDate { get; init; }
            public DateTime? UsageEndDate { get; init; }
            public decimal AmtInr { get; init; }
            public string? ManualOverrideName { get; init; }
            public string Key => NormalizeKey(SubscriptionName, ResourceGroup, ResourceName);
        }

        private sealed class AggregatedPricingRow
        {
            public string Key { get; init; } = string.Empty;
            public string SubscriptionName { get; init; } = string.Empty;
            public string ResourceGroup { get; init; } = string.Empty;
            public string ResourceName { get; init; } = string.Empty;
            public decimal TotalAmtInr { get; set; }
            public int LineItemCount { get; set; }
            public HashSet<string> Locations { get; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> ProductNames { get; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> MeterNames { get; } = new(StringComparer.OrdinalIgnoreCase);
            public DateTime? MinUsageStartDate { get; set; }
            public DateTime? MaxUsageEndDate { get; set; }
        }

        private sealed class CanonicalResourceRow
        {
            public string SubscriptionName { get; init; } = string.Empty;
            public string ResourceGroup { get; init; } = string.Empty;
            public string? ResourceName { get; init; }
            public string? ResourceType { get; init; }
            public string? ServiceName { get; init; }
            public string? ResourcePlan { get; init; }
            public string? MeterCategory { get; init; }
            public string? Location { get; init; }
        }
    }

    public sealed class ManualPricingImportOptions
    {
        public bool Enabled { get; set; }
        public string Format { get; set; } = "LegacyBillingExport";
        public string WorkbookPath { get; set; } = string.Empty;
        public string SubscriptionName { get; set; } = string.Empty;
        public string EffectiveUsageDate { get; set; } = string.Empty;
        public string Currency { get; set; } = "INR";
        public string OnlyBenefitType { get; set; } = "Charge";
        public string ReportDirectory { get; set; } = "import-reports";
        public List<ManualPricingImportOverride> Overrides { get; set; } = new();
    }

    public sealed class ManualPricingUploadRequest
    {
        public required Stream WorkbookStream { get; init; }
        public string WorkbookName { get; init; } = "uploaded-workbook.xlsx";
        public string SubscriptionName { get; init; } = string.Empty;
        public string? EffectiveUsageDate { get; init; }
        public string Format { get; init; } = "SgBillingExport";
        public string Currency { get; init; } = "INR";
    }

    public sealed class ManualPricingImportOverride
    {
        public string Name { get; set; } = string.Empty;
        public string SourceSubscriptionName { get; set; } = string.Empty;
        public string SourceResourceGroup { get; set; } = string.Empty;
        public string SourceResourceName { get; set; } = string.Empty;
        public string TargetResourceGroup { get; set; } = string.Empty;
        public string TargetResourceName { get; set; } = string.Empty;
    }

    public sealed class ManualPricingImportSummary
    {
        public string WorkbookPath { get; set; } = string.Empty;
        public string SubscriptionName { get; set; } = string.Empty;
        public string EffectiveUsageDate { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public int TotalExcelRowsRead { get; set; }
        public int EligibleChargeRows { get; set; }
        public int DistinctAggregatedResources { get; set; }
        public int MatchedResourcesInserted { get; set; }
        public int UnmatchedResources { get; set; }
        public int SkippedRows { get; set; }
        public decimal TotalImportedAmtInr { get; set; }
    }

    public sealed class InsertedPricingRowReport
    {
        public Guid InsertedRowId { get; set; }
        public string SubscriptionName { get; set; } = string.Empty;
        public string ResourceGroup { get; set; } = string.Empty;
        public string ResourceName { get; set; } = string.Empty;
        public decimal TotalAmtInr { get; set; }
        public int LineItemCount { get; set; }
        public string UsageDate { get; set; } = string.Empty;
        public CanonicalMetadataReport CopiedMetadata { get; set; } = new();
    }

    public sealed class CanonicalMetadataReport
    {
        public string? ResourceType { get; set; }
        public string? ServiceName { get; set; }
        public string? ResourcePlan { get; set; }
        public string? MeterCategory { get; set; }
        public string? Location { get; set; }
    }

    public sealed class UnmatchedPricingRowReport
    {
        public string SubscriptionName { get; set; } = string.Empty;
        public string ResourceGroup { get; set; } = string.Empty;
        public string ResourceName { get; set; } = string.Empty;
        public decimal TotalAmtInr { get; set; }
        public int LineItemCount { get; set; }
        public string[] Locations { get; set; } = Array.Empty<string>();
        public string[] ProductNames { get; set; } = Array.Empty<string>();
        public string[] MeterNames { get; set; } = Array.Empty<string>();
        public string? UsageStartDate { get; set; }
        public string? UsageEndDate { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class SkippedExcelRow
    {
        public int? RowNumber { get; set; }
        public string? SubscriptionName { get; set; }
        public string? ResourceGroup { get; set; }
        public string? ResourceName { get; set; }
        public string? AmtInr { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
