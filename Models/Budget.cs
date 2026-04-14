using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AzureFinOps.API.Models
{
    public class Budget
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string ResourceGroup { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18, 4)")]
        public decimal MonthlyBudget { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public int AlertAt { get; set; } = 80;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
