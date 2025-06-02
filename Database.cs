
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
                bool usePostgres = Environment.GetEnvironmentVariable("CLIPTOK_POSTGRES") is not null;
                if (usePostgres)
                    optionsBuilder.UseNpgsql(Environment.GetEnvironmentVariable("CLIPTOK_POSTGRES"));
                else
                {
                    Console.WriteLine("WARNING: Falling back to Sqlite DB for efcore. You probably don't want this, make sure db/ is persisted or configure Postgres");
                    optionsBuilder.UseSqlite("Data Source=db/Cliptok.db;Cache=Shared;Pooling=true;");
                }
            }
        }
    }
}
