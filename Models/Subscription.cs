using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AzureFinOps.API.Models
{
    public class Subscription
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string SubscriptionId { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string SubscriptionName { get; set; } = string.Empty;

        // Navigation property
        public ICollection<ResourceGroup> ResourceGroups { get; set; } = new List<ResourceGroup>();
    }
}
