using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using MinecraftMobsAnalyzer.Models;

namespace MinecraftMobsAnalyzer.Converters;

public class ListToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            IEnumerable<Location> locations => string.Join(", ", locations.Select(l => l.SpawnName)),
            IEnumerable<Drop> drops => string.Join(", ", drops.Select(d => d.DropName)),
            IEnumerable enumerable => string.Join(", ", enumerable.Cast<object?>().Select(o => o?.ToString())),
            _ => string.Empty,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
