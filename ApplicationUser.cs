using Microsoft.AspNetCore.Identity;

namespace reactBackend.Models
{
    public class ApplicationUser : IdentityUser
    {
        public ApplicationUser()
        {
            Orders = new List<Order>();
        }

        public virtual List<Order> Orders { get; set; }
    }
}