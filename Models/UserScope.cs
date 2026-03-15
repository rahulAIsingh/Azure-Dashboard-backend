using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AzureFinOps.API.Models
{
    public class UserScope
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [Required]
        [MaxLength(50)]
        public string ScopeType { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string ScopeValue { get; set; } = string.Empty;

        // Navigation property
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;
    }
}
