using reactBackend.Models;
using System.Text.Json.Serialization;

public class ProductImage
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;

    [JsonIgnore]
    public virtual Product? Product { get; set; }
}
