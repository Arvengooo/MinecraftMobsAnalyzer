using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MinecraftMobsAnalyzer.Models;

public class Drop
{
    [Key]
    public Guid DropId { get; set; } = Guid.NewGuid();

    public string DropName { get; set; } = string.Empty;

    public virtual ICollection<Mob> Mobs { get; set; } = new HashSet<Mob>();
}
