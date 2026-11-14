# ResourceMonitor

Кросплатформний застосунок на C# і Avalonia UI для моніторингу CPU, RAM, дисків, мережі та процесів із графіками й журналом змін навантаження.

## Вимоги

- .NET 10 SDK для збірки.
- Windows x64, Linux x64 або macOS ARM64 (Apple Silicon).
- На Linux — графічна сесія та бібліотеки X11 і fontconfig.

## Локальний запуск

Відкрийте термінал у корені проєкту:

```sh
dotnet restore ResourceMonitor.sln
dotnet run --project src/ResourceMonitor.Desktop -c Release
```

## Збірка застосунку

Виконайте команду для потрібної ОС. Готова збірка містить .NET Runtime; передавати потрібно всю її папку.

### Linux x64

```sh
dotnet publish src/ResourceMonitor.Desktop -p:PublishProfile=linux-x64
./artifacts/linux-x64/ResourceMonitor.Desktop
```

### Windows x64 (PowerShell)

```powershell
dotnet publish src/ResourceMonitor.Desktop -p:PublishProfile=win-x64
.\artifacts\win-x64\ResourceMonitor.Desktop.exe
```

### macOS ARM64

```sh
dotnet publish src/ResourceMonitor.Desktop -p:PublishProfile=osx-arm64
./artifacts/osx-arm64/ResourceMonitor.Desktop
```

Усі профілі створюють Release-збірку. Запускайте її на відповідній ОС; macOS-команда створює папку з програмою, а не `.app` bundle.

## Тести

```sh
dotnet test ResourceMonitor.sln -c Release
dotnet run --project src/ResourceMonitor.Desktop -c Release -- --smoke-test
dotnet run --project src/ResourceMonitor.Desktop -c Release -- --ui-smoke-test
```
