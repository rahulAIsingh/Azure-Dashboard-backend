using AzureFinOps.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AzureFinOps.API.Controllers
{
    public class ManualPricingRequestDto
    {
        public IFormFile? File { get; set; }
        public string? SubscriptionName { get; set; }
        public string? EffectiveUsageDate { get; set; }
    }

    [Authorize(Roles = "Super Admin")]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly IManualPricingImportService _manualPricingImportService;
        private readonly ILogger<SettingsController> _logger;

        public SettingsController(
            IManualPricingImportService manualPricingImportService,
            ILogger<SettingsController> logger)
        {
            _manualPricingImportService = manualPricingImportService;
            _logger = logger;
        }

        [HttpPost("manual-pricing-import")]
        [RequestFormLimits(MultipartBodyLengthLimit = 50_000_000)]
        public async Task<IActionResult> ImportManualPricing()
        {
            if (!Request.HasFormContentType)
            {
                return BadRequest(new { message = "Request must be a multipart/form-data upload." });
            }

            var form = await Request.ReadFormAsync();
            var file = form.Files["file"];
            var subscriptionName = form["subscriptionName"].ToString();
            var effectiveUsageDate = form["effectiveUsageDate"].ToString();

            _logger.LogInformation("Direct Form Access - SubscriptionName: {Sub}, EffectiveDate: {Date}, File: {File}", 
                subscriptionName, effectiveUsageDate, file?.FileName);

            if (file is null || file.Length == 0)
            {
                return BadRequest(new { message = "An .xlsx workbook is required (form field 'file')." });
            }

            if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Only .xlsx workbooks are supported for manual pricing import." });
            }

            if (string.IsNullOrWhiteSpace(subscriptionName))
            {
                return BadRequest(new { message = "Subscription name is required (form field 'subscriptionName')." });
            }

            await using var stream = file.OpenReadStream();
            try
            {
                var summary = await _manualPricingImportService.ImportWorkbookAsync(new ManualPricingUploadRequest
                {
                    WorkbookStream = stream,
                    WorkbookName = file.FileName,
                    SubscriptionName = subscriptionName,
                    EffectiveUsageDate = string.IsNullOrWhiteSpace(effectiveUsageDate) ? null : effectiveUsageDate,
                    Format = "SgBillingExport",
                    Currency = "INR"
                }, HttpContext.RequestAborted);

                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import manual pricing workbook.");
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
