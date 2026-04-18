using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using MinecraftMobsAnalyzer.Models;

namespace MinecraftMobsAnalyzer.Data;

public class MobsDbContext : DbContext
{
    /// <summary>
    /// Путь к .sqlite-файлу. По умолчанию — рядом с exe в папке App_Data.
    /// Переопределяется в App.xaml.cs если нужно.
    /// </summary>
    public static string DatabasePath { get; set; } =
        Path.Combine(AppContext.BaseDirectory, "App_Data", "MinecraftData.sqlite");

    public DbSet<Mob> Mobs => Set<Mob>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Drop> Drops => Set<Drop>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var dir = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            optionsBuilder.UseSqlite($"Data Source={DatabasePath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // M2M: Mob <-> Location. Явно задаём имя join-таблицы, чтобы
        // схема была предсказуемой и переносимой.
        modelBuilder.Entity<Mob>()
            .HasMany(m => m.Locations)
            .WithMany(l => l.Mobs)
            .UsingEntity(j => j.ToTable("MobLocation"));

        // M2M: Mob <-> Drop.
        modelBuilder.Entity<Mob>()
            .HasMany(m => m.Drops)
            .WithMany(d => d.Mobs)
            .UsingEntity(j => j.ToTable("MobDrop"));

        base.OnModelCreating(modelBuilder);
    }
}
