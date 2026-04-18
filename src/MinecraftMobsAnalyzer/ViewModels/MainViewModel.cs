using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using MinecraftMobsAnalyzer.Commands;
using MinecraftMobsAnalyzer.Data;
using MinecraftMobsAnalyzer.Models;
using MinecraftMobsAnalyzer.Services;

namespace MinecraftMobsAnalyzer.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public ObservableCollection<Mob> MobList { get; } = new();

    private Mob? _selectedMob;
    public Mob? SelectedMob
    {
        get => _selectedMob;
        set { _selectedMob = value; OnPropertyChanged(); }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    public ICommand ParseCommand { get; }
    public ICommand LoadFromDbCommand { get; }
    public ICommand DeleteSelectedCommand { get; }
    public ICommand DeleteAllCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand GenerateReportCommand { get; }
    public ICommand GenerateChartCommand { get; }

    public MainViewModel()
    {
        ParseCommand = new RelayCommand(async _ => await ParseDataAsync(), _ => !IsBusy);
        LoadFromDbCommand = new RelayCommand(async _ => await LoadDataFromDatabaseAsync(), _ => !IsBusy);
        DeleteSelectedCommand = new RelayCommand(async _ => await DeleteSelectedMobAsync(), _ => SelectedMob is not null && !IsBusy);
        DeleteAllCommand = new RelayCommand(async _ => await DeleteAllMobsAsync(), _ => MobList.Any() && !IsBusy);
        UpdateCommand = new RelayCommand(async _ => await UpdateMobAsync(), _ => SelectedMob is not null && !IsBusy);
        GenerateReportCommand = new RelayCommand(_ => GenerateWordReport(), _ => SelectedMob is not null);
        GenerateChartCommand = new RelayCommand(_ => GenerateExcelChart(), _ => MobList.Any());
    }

    private async Task ParseDataAsync()
    {
        IsBusy = true;
        try
        {
            var result = await ParserService.ParseMobsAsync().ConfigureAwait(true);

            string summary = $"Парсинг завершён. Успешно: {result.Processed}";
            if (result.Failed > 0)
            {
                summary += $", с ошибками: {result.Failed}";
                if (result.FirstError is not null)
                    summary += $"\n\nПервая ошибка ({result.FirstErrorSlug}):\n{result.FirstError.GetBaseException().Message}";
            }

            MessageBox.Show(summary, "Готово", MessageBoxButton.OK,
                result.Failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            await LoadDataFromDatabaseAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.GetBaseException().Message, "Ошибка парсинга", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadDataFromDatabaseAsync()
    {
        try
        {
            MobList.Clear();
            await using var db = new MobsDbContext();
            await db.Database.EnsureCreatedAsync().ConfigureAwait(true);

            var mobs = await db.Mobs
                .Include(m => m.Locations)
                .Include(m => m.Drops)
                .AsNoTracking()
                .ToListAsync()
                .ConfigureAwait(true);

            foreach (var mob in mobs)
                MobList.Add(mob);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.GetBaseException().Message, "Ошибка загрузки БД", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task DeleteSelectedMobAsync()
    {
        if (SelectedMob is null) return;
        try
        {
            await using var db = new MobsDbContext();
            var mob = await db.Mobs.FindAsync(SelectedMob.MobId).ConfigureAwait(true);
            if (mob is not null)
            {
                db.Mobs.Remove(mob);
                await db.SaveChangesAsync().ConfigureAwait(true);
            }
            await LoadDataFromDatabaseAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.GetBaseException().Message, "Ошибка удаления", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task DeleteAllMobsAsync()
    {
        try
        {
            await using var db = new MobsDbContext();
            db.Drops.RemoveRange(db.Drops);
            db.Locations.RemoveRange(db.Locations);
            db.Mobs.RemoveRange(db.Mobs);
            await db.SaveChangesAsync().ConfigureAwait(true);
            await LoadDataFromDatabaseAsync().ConfigureAwait(true);
            MessageBox.Show("Все данные удалены", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.GetBaseException().Message, "Ошибка удаления", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task UpdateMobAsync()
    {
        if (SelectedMob is null) return;
        try
        {
            await using var db = new MobsDbContext();
            var mob = await db.Mobs.FindAsync(SelectedMob.MobId).ConfigureAwait(true);
            if (mob is not null)
            {
                mob.MobName = SelectedMob.MobName;
                mob.MobHealth = SelectedMob.MobHealth;
                await db.SaveChangesAsync().ConfigureAwait(true);
                MessageBox.Show("Данные обновлены", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            await LoadDataFromDatabaseAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.GetBaseException().Message, "Ошибка обновления", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void GenerateWordReport()
    {
        if (SelectedMob is null) return;
        var dialog = new SaveFileDialog
        {
            Filter = "Документ Word (*.docx)|*.docx",
            FileName = $"Report_{SelectedMob.MobName}.docx",
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            ReportService.CreateMobWordReport(SelectedMob, dialog.FileName);
            MessageBox.Show("Отчёт создан", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.GetBaseException().Message, "Ошибка отчёта", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void GenerateExcelChart()
    {
        if (!MobList.Any()) return;
        var dialog = new SaveFileDialog
        {
            Filter = "Книга Excel (*.xlsx)|*.xlsx",
            FileName = "MobsHealthChart.xlsx",
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            ReportService.CreateHealthChartExcel(MobList, dialog.FileName);
            MessageBox.Show("Файл создан", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.GetBaseException().Message, "Ошибка графика", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
