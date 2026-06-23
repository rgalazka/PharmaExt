# PharmaExt

Desktopowa aplikacja Windows do tworzenia protokolow sporzadzenia leku recepturowego i etykiet informacyjnych.

## Zalozenia

- Firebird jest uzywany tylko do odczytu.
- Dane zaimportowanych recept sa zapisywane jako snapshot w lokalnej bazie SQLite.
- Protokol jest generowany jako A4.
- Do kazdego protokolu moze byc dodana druga strona A4 z etykieta.
- Osobne PDF-y z samymi etykietami sa generowane pod Brother QL na rolce 62 mm.

## Technologia

- .NET 8
- WPF
- SQLite
- FirebirdSql.Data.FirebirdClient
- QuestPDF

## Uruchomienie na Windows

Wymagane jest .NET SDK 8 albo nowsze.

```powershell
dotnet restore .\src\PharmaExt.App\PharmaExt.App.csproj
dotnet build .\src\PharmaExt.App\PharmaExt.App.csproj
dotnet run --project .\src\PharmaExt.App\PharmaExt.App.csproj
```

Aktualny szkielet uzywa danych testowych. Prawdziwe zapytania Firebird trzeba dodac po poznaniu tabel i pol bazy zrodlowej.
