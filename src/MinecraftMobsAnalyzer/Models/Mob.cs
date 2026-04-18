using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MinecraftMobsAnalyzer.Models;

public class Mob
{
    [Key]
    public Guid MobId { get; set; } = Guid.NewGuid();

    public string MobName { get; set; } = string.Empty;

    public int MobHealth { get; set; }

    public virtual ICollection<Location> Locations { get; set; } = new HashSet<Location>();
    public virtual ICollection<Drop> Drops { get; set; } = new HashSet<Drop>();
}
