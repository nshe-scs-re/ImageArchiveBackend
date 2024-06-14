using Microsoft.EntityFrameworkCore;
using ImageProjectBackend.Models;

namespace ImageProjectBackend.Data
{
    /// <summary>
    /// Represents a database context for the 'Images' table in the 'ImageDatabase' database for use with Entity Framework Core
    /// </summary>
    /// <param name="options">The options to be used by the database context.</param>
    public class ImageDbContext(DbContextOptions<ImageDbContext> options) : DbContext(options)
    {
        public DbSet<Image> Images { get; set; }
    }
}
