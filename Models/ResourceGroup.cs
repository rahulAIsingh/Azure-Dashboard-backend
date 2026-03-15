using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AzureFinOps.API.Models
{
    public class ResourceGroup
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string ResourceGroupName { get; set; } = string.Empty;

        [Required]
        public Guid SubscriptionId { get; set; }

        // Navigation property
        [ForeignKey("SubscriptionId")]
        public Subscription Subscription { get; set; } = null!;
    }
}
