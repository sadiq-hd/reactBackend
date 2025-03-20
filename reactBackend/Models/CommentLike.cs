using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace reactBackend.Models
{
    public class CommentLike
    {
        public int Id { get; set; }

        [Required]
        public int CommentId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("CommentId")]
        [JsonIgnore]
        public virtual ProductComment? Comment { get; set; }

        [ForeignKey("UserId")]
        [JsonIgnore]
        public virtual ApplicationUser? User { get; set; }
    }
}