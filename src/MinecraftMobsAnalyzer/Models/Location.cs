using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MinecraftMobsAnalyzer.Models;

public class Location
{
    [Key]
    public Guid SpawnId { get; set; } = Guid.NewGuid();

    public string SpawnName { get; set; } = string.Empty;

    public virtual ICollection<Mob> Mobs { get; set; } = new HashSet<Mob>();
}
