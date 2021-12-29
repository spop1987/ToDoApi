using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ToDo.Api.Models;

namespace ToDo.Api.Data
{
    public class ApiDbContext : IdentityDbContext
    {
        public virtual DbSet<ItemData> Items {get; set;}
        public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options) {}
    }
}