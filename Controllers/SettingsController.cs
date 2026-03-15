using AzureFinOps.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AzureFinOps.API.Controllers
{
    [AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class SettingsController : ControllerBase
    {
        private readonly IManualPricingImportService _manualPricingImportService;

        public SettingsController(IManualPricingImportService manualPricingImportService)
        {
            _manualPricingImportService = manualPricingImportService;
        }

        [HttpPost("manual-pricing-import")]
        [RequestFormLimits(MultipartBodyLengthLimit = 50_000_000)]
        public async Task<IActionResult> ImportManualPricing(IFormFile? file, string subscriptionName, string? effectiveUsageDate = null)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest(new { message = "An .xlsx workbook is required." });
            }

            if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Only .xlsx workbooks are supported for manual pricing import." });
            }

            if (string.IsNullOrWhiteSpace(subscriptionName))
            {
                return BadRequest(new { message = "Subscription name is required." });
            }

            await using var stream = file.OpenReadStream();
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
    }
}
