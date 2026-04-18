using System;
using System.IO;
using System.Windows;
using MinecraftMobsAnalyzer.Data;

namespace MinecraftMobsAnalyzer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // SQLite-файл создаётся рядом с exe в папке App_Data. Никаких SQL Server/LocalDB не нужно.
        var appDataFolder = Path.Combine(AppContext.BaseDirectory, "App_Data");
        Directory.CreateDirectory(appDataFolder);
        MobsDbContext.DatabasePath = Path.Combine(appDataFolder, "MinecraftData.sqlite");

        // Глобальный обработчик — любые необработанные исключения в UI-потоке
        // превращаем в MessageBox вместо молчаливого крэша.
        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show(
                $"Необработанная ошибка:\n{args.Exception.GetBaseException().Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                MessageBox.Show(
                    $"Критическая ошибка:\n{ex.GetBaseException().Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
    }
}
