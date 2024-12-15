using System.ComponentModel.DataAnnotations;

namespace reactBackend.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Username { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        [MaxLength(20)]
        public string Role { get; set; }
        [Required]
        [MaxLength(10)]
        public string PhoneNumber { get; set; }


    }



public class ProductModel
    {
        [Required]
        [StringLength(20, MinimumLength = 1)]
        [RegularExpression(@"^[A-Z][a-zA-Z\s]*$")]
        public string ProductName { get; set; }

        [Range(0, 200)]
        public int? ProductCount { get; set; }
    }

}
