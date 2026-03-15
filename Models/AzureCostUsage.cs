using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AzureFinOps.API.Models
{
    public class AzureCostUsage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        [Required]
        public DateTime UsageDate { get; set; }

        [Required]
        [MaxLength(255)]
        public string SubscriptionName { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string ResourceGroup { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? ResourceName { get; set; }

        [MaxLength(255)]
        public string? ResourceType { get; set; }

        [MaxLength(255)]
        public string? ServiceName { get; set; }

        /// <summary>Meter name — e.g. "B4ms", "P1v2", "General Purpose". Displayed as ServiceName (ResourcePlan).</summary>
        [MaxLength(255)]
        public string? ResourcePlan { get; set; }

        [MaxLength(255)]
        public string? MeterCategory { get; set; }

        [MaxLength(100)]
        public string? Location { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 4)")]
        public decimal Cost { get; set; }

        [Required]
        [MaxLength(10)]
        public string Currency { get; set; } = string.Empty;
    }
}
