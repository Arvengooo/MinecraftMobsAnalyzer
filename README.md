# MinecraftMobsAnalyzer (v2)

WPF-приложение на **.NET 8** для парсинга мобов с `minecraft.wiki` и хранения их в SQLite.

## Что обновлено по сравнению с предыдущей версией
- **.NET Framework 4.7.2 → .NET 8** (`net8.0-windows`).
- **Entity Framework 6 + LocalDB → EF Core 8 + SQLite**. Больше не нужен SQL Server — база лежит одним файлом `App_Data/MinecraftData.sqlite` рядом с exe.
- **HttpWebRequest (TLS-проблемы) → HttpClient** с явным User-Agent.
- **Office Interop (Word/Excel) → DocumentFormat.OpenXml + ClosedXML**. Office на машине не требуется, работает на любой Windows 10/11.
- Все ошибки EF/парсера выводятся в MessageBox (больше нет тихих крэшей).

## Требования
- Windows 10/11.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) для сборки **или** .NET 8 Desktop Runtime (x64) для запуска готового exe.
- Visual Studio 2022 17.8+ или Rider.

## Сборка из командной строки
```powershell
cd src\MinecraftMobsAnalyzer
dotnet restore
dotnet build -c Release
dotnet run -c Release
```

Либо откройте `MinecraftMobsAnalyzer.sln` в Visual Studio и нажмите F5.

## Структура
```
src/MinecraftMobsAnalyzer/
├── App.xaml(.cs)          — точка входа, глобальный обработчик ошибок
├── MainWindow.xaml(.cs)   — главное окно
├── Commands/RelayCommand.cs
├── Converters/ListToStringConverter.cs
├── Data/MobsDbContext.cs  — EF Core, SQLite
├── Models/                — Mob, Drop, Location (Guid PK, m2m)
├── Services/
│   ├── ParserService.cs   — HtmlAgilityPack + HttpClient
│   └── ReportService.cs   — OpenXml (Word) + ClosedXML (Excel)
└── ViewModels/MainViewModel.cs
```

## Как это работает
1. При первом запуске `EnsureCreated()` создаёт `App_Data/MinecraftData.sqlite`.
2. Кнопка **«Спарсить данные»** качает 20 страниц мобов из `minecraft.wiki`, извлекает имя/здоровье/биомы/дроп и кладёт в БД.
3. **«Загрузить из БД»** — перезагрузка списка.
4. **«Создать отчёт (Word)»** — .docx по выбранному мобу (OpenXml).
5. **«Таблица (Excel)»** — .xlsx с распределением мобов по здоровью (ClosedXML).
