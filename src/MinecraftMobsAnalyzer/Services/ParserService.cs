using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using MinecraftMobsAnalyzer.Data;
using MinecraftMobsAnalyzer.Models;

namespace MinecraftMobsAnalyzer.Services;

public sealed class ParseResult
{
    public int Processed { get; init; }
    public int Failed { get; init; }
    public string? FirstErrorSlug { get; init; }
    public Exception? FirstError { get; init; }
}

/// <summary>
/// Парсит страницы мобов с minecraft.wiki и кладёт в БД.
/// Статический, реентабельный за счёт HttpClient (один экземпляр на процесс).
/// </summary>
public static class ParserService
{
    private const string BaseUrl = "https://minecraft.wiki";

    private static readonly HttpClient Http = CreateHttpClient();

    /// <summary>
    /// Явный список мобов — на сводных страницах вики разметка часто меняется,
    /// а отдельные страницы стабильные.
    /// </summary>
    private static readonly string[] MobSlugs =
    {
        "Zombie", "Skeleton", "Creeper", "Spider", "Enderman",
        "Witch", "Slime", "Blaze", "Ghast", "Pig",
        "Cow", "Sheep", "Chicken", "Horse", "Wolf",
        "Villager", "Pillager", "Drowned", "Husk", "Piglin"
    };

    /// <summary>
    /// Заголовки ссылок, не являющиеся ни биомом, ни предметом.
    /// </summary>
    private static readonly HashSet<string> TitleBlocklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "Java Edition", "Bedrock Edition", "Pocket Edition", "Legacy Console Edition",
        "Health", "Armor", "Mob type", "Monster", "Undead",
        "Light", "Solid block", "Biome", "Spawn egg", "Experience"
    };

    private static HttpClient CreateHttpClient()
    {
        // На .NET 8 TLS 1.2/1.3 включены по умолчанию, но делаем запрос с
        // User-Agent — некоторые CDN wiki блокируют запросы без него.
        var client = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        })
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MinecraftMobsAnalyzer/2.0 (+https://github.com/Arvengooo/MinecraftMobsAnalyzer)");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");
        return client;
    }

    public static async Task<ParseResult> ParseMobsAsync(CancellationToken ct = default)
    {
        int processed = 0;
        int failed = 0;
        Exception? firstError = null;
        string? firstSlug = null;

        await using var db = new MobsDbContext();
        await db.Database.EnsureCreatedAsync(ct).ConfigureAwait(false);

        foreach (var slug in MobSlugs)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await ParseSingleMobAsync(db, slug, ct).ConfigureAwait(false);
                processed++;
            }
            catch (Exception ex)
            {
                failed++;
                firstError ??= ex;
                firstSlug ??= slug;
            }
        }

        return new ParseResult
        {
            Processed = processed,
            Failed = failed,
            FirstError = firstError,
            FirstErrorSlug = firstSlug,
        };
    }

    private static async Task ParseSingleMobAsync(MobsDbContext db, string slug, CancellationToken ct)
    {
        var url = $"{BaseUrl}/w/{slug}";
        var html = await Http.GetStringAsync(url, ct).ConfigureAwait(false);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var nameNode = doc.DocumentNode.SelectSingleNode(
            "//div[contains(concat(' ', normalize-space(@class), ' '), ' infobox-title ')]");
        var mobName = nameNode?.InnerText.Trim() ?? slug.Replace('_', ' ');
        mobName = Regex.Replace(mobName, @"\s+", " ");

        int health = ExtractHealth(doc);

        var mob = await db.Mobs
            .Include(m => m.Locations)
            .Include(m => m.Drops)
            .FirstOrDefaultAsync(m => m.MobName == mobName, ct)
            .ConfigureAwait(false);

        if (mob is null)
        {
            mob = new Mob { MobName = mobName, MobHealth = health };
            db.Mobs.Add(mob);
        }
        else
        {
            mob.MobHealth = health;
        }

        foreach (var locName in CollectSectionLinks(doc, "Spawning").Take(8))
        {
            var location = await db.Locations
                .FirstOrDefaultAsync(l => l.SpawnName == locName, ct)
                .ConfigureAwait(false);
            if (location is null)
            {
                location = new Location { SpawnName = locName };
                db.Locations.Add(location);
            }
            if (!mob.Locations.Contains(location))
                mob.Locations.Add(location);
        }

        foreach (var dropName in CollectSectionLinks(doc, "Drops").Take(10))
        {
            var drop = await db.Drops
                .FirstOrDefaultAsync(d => d.DropName == dropName, ct)
                .ConfigureAwait(false);
            if (drop is null)
            {
                drop = new Drop { DropName = dropName };
                db.Drops.Add(drop);
            }
            if (!mob.Drops.Contains(drop))
                mob.Drops.Add(drop);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    internal static int ExtractHealth(HtmlDocument doc)
    {
        var healthRow = doc.DocumentNode.SelectSingleNode(
            "//table[contains(@class,'infobox-rows')]//tr[th//a[@title='Health']]");
        if (healthRow is null) return 0;

        var td = healthRow.SelectSingleNode("./td");
        if (td is null) return 0;

        var match = Regex.Match(td.InnerText ?? string.Empty, @"\d+");
        return match.Success && int.TryParse(match.Value, out int health) ? health : 0;
    }

    internal static IEnumerable<string> CollectSectionLinks(HtmlDocument doc, string sectionId)
    {
        var heading = doc.DocumentNode.SelectSingleNode($"//h2[@id='{sectionId}']");
        if (heading is null) yield break;

        // На minecraft.wiki h2 обёрнут в <div class="mw-heading mw-heading2">.
        var wrapper = heading.ParentNode is not null &&
                      heading.ParentNode.GetAttributeValue("class", "").Contains("mw-heading2")
            ? heading.ParentNode
            : heading;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var node = wrapper.NextSibling; node is not null; node = node.NextSibling)
        {
            if (node.NodeType != HtmlNodeType.Element) continue;

            // Следующий заголовок того же уровня — останавливаемся.
            if (node.Name == "div" &&
                node.GetAttributeValue("class", "").Contains("mw-heading2"))
                break;

            var links = node.SelectNodes(".//a[@title]");
            if (links is null) continue;

            foreach (var a in links)
            {
                var title = a.GetAttributeValue("title", "").Trim();
                if (string.IsNullOrEmpty(title)) continue;
                if (title.Length > 40) continue;
                if (title.StartsWith("File:", StringComparison.OrdinalIgnoreCase)) continue;
                if (title.StartsWith("Category:", StringComparison.OrdinalIgnoreCase)) continue;
                if (TitleBlocklist.Contains(title)) continue;

                var href = a.GetAttributeValue("href", "");
                if (!href.StartsWith("/w/")) continue;

                var cls = a.GetAttributeValue("class", "");
                if (cls.Contains("mw-editsection") || cls.Contains("mw-file-description")) continue;

                if (seen.Add(title))
                    yield return title;
            }
        }
    }
}
