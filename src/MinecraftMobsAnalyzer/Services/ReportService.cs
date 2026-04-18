using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MinecraftMobsAnalyzer.Models;

namespace MinecraftMobsAnalyzer.Services;

/// <summary>
/// Генерация Word/Excel отчётов без Office Interop — только кросс-платформенные
/// пакеты DocumentFormat.OpenXml и ClosedXML. Файлы .docx / .xlsx открываются
/// в любом редакторе (Word, LibreOffice, онлайн-просмотрщики).
/// </summary>
public static class ReportService
{
    public static void CreateMobWordReport(Mob mob, string path)
    {
        if (mob is null) throw new ArgumentNullException(nameof(mob));

        using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = document.AddMainDocumentPart();
        main.Document = new Document(new Body(
            Heading($"Отчёт по мобу: {mob.MobName}"),
            Para($"Здоровье: {mob.MobHealth}"),
            Para($"Локации спавна: {string.Join(", ", mob.Locations.Select(l => l.SpawnName))}"),
            Para($"Добыча: {string.Join(", ", mob.Drops.Select(d => d.DropName))}")
        ));
        main.Document.Save();
    }

    public static void CreateHealthChartExcel(IEnumerable<Mob> mobs, string path)
    {
        if (mobs is null) throw new ArgumentNullException(nameof(mobs));

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Мобы");

        ws.Cell(1, 1).Value = "Здоровье";
        ws.Cell(1, 2).Value = "Количество мобов";
        ws.Range("A1:B1").Style.Font.Bold = true;

        var groups = mobs
            .GroupBy(m => m.MobHealth)
            .OrderBy(g => g.Key)
            .ToList();

        int row = 2;
        foreach (var g in groups)
        {
            ws.Cell(row, 1).Value = g.Key;
            ws.Cell(row, 2).Value = g.Count();
            row++;
        }

        ws.Columns().AdjustToContents();
        workbook.SaveAs(path);
    }

    private static Paragraph Heading(string text)
    {
        var run = new Run(new Text(text));
        var runProps = new RunProperties(
            new Bold(),
            new FontSize { Val = "32" });
        run.PrependChild(runProps);
        return new Paragraph(run);
    }

    private static Paragraph Para(string text)
        => new(new Run(new Text(text)));
}
