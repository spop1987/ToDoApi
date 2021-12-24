using Microsoft.EntityFrameworkCore;
using ToDo.Api.Models;

namespace ToDo.Api.Data
{
    public class ApiDbContext : DbContext
    {
        public virtual DbSet<ItemData> Items {get; set;}
        public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options) {}
    }
}