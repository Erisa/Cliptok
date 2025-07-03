
using Microsoft.EntityFrameworkCore;
using System;

namespace Cliptok
{
    public class CliptokDbContext : DbContext
    {
        private readonly string _connectionString;
        private readonly bool _usePostgres;

        public DbSet<Models.CachedDiscordMessage> Messages { get; set; }
        public DbSet<Models.CachedDiscordUser> Users { get; set; }

        public CliptokDbContext()
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
#if DEBUG
                optionsBuilder.EnableSensitiveDataLogging(true);
                optionsBuilder.EnableDetailedErrors(true);
#endif

                bool usePostgres = Environment.GetEnvironmentVariable("CLIPTOK_POSTGRES") is not null;
                if (usePostgres)
                    optionsBuilder.UseNpgsql(Environment.GetEnvironmentVariable("CLIPTOK_POSTGRES"));
                else
                {
                    throw new ArgumentException("Persistent database enabled but no database connection string provided. Set CLIPTOK_POSTGRES environment variable.");
                } 
            }
        }
    }
}
