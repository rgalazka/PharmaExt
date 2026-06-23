# PharmaExt - specyfikacja MVP

## Cel aplikacji

PharmaExt bedzie graficzna aplikacja Windows do tworzenia protokolow sporzadzenia leku recepturowego oraz etykiet informacyjnych leku.

Aplikacja pobiera dane recept z bazy Firebird w trybie tylko do odczytu, zapisuje lokalny snapshot w lekkiej bazie aplikacji, pozwala uzytkownikowi uzupelnic pola robocze, a nastepnie generuje PDF-y gotowe do wydruku.

## Zrodla danych

### Firebird

Baza Firebird jest zrodlem danych tylko do odczytu. Aplikacja nie moze zapisywac ani modyfikowac danych w Firebirdzie.

Z Firebirda pobierane beda kompletne dane bazowe, m.in.:

- dane recepty,
- numer recepty,
- dane pacjenta i adres,
- lekarz,
- osoba sporzadzajaca lek,
- data sporzadzenia,
- postac leku,
- skladniki leku,
- ilosci i jednostki,
- dane surowcow, jesli sa dostepne.

Zakladamy, ze pola pobierane z Firebirda sa zawsze uzupelnione.

### Lokalna baza aplikacji

Aplikacja posiada wlasna lekka baze lokalna, najlepiej SQLite.

Lokalna baza przechowuje:

- ustawienia aplikacji,
- dane apteki,
- konfiguracje polaczenia z Firebirdem,
- snapshoty zaimportowanych recept,
- snapshoty skladnikow,
- pola uzupelniane przez uzytkownika,
- statusy formularzy,
- ustawienia etykiety dla kazdej recepty,
- informacje o wygenerowanych PDF-ach.

Snapshot danych z Firebirda jest zapisywany lokalnie po imporcie, zeby pozniejszy protokol byl powtarzalny i niezalezny od zmian w systemie zrodlowym.

### Ustawienia aplikacji

Dane apteki sa jedne dla calej aplikacji i sa zapisywane w ustawieniach.

Przykladowe ustawienia:

- nazwa apteki,
- adres apteki,
- dane polaczenia z Firebirdem,
- katalog zapisu PDF,
- domyslny typ etykiety,
- domyslny rozmiar etykiety.

## Workflow uzytkownika

1. Uzytkownik uruchamia aplikacje.
2. Przy pierwszym uruchomieniu uzupelnia ustawienia aplikacji i dane apteki.
3. Uzytkownik podaje zakres dat oraz adres albo fragment adresu.
4. Aplikacja wyszukuje recepty w Firebirdzie.
5. Uzytkownik importuje znalezione recepty do lokalnych formularzy.
6. Aplikacja zapisuje lokalnie snapshot danych kazdej recepty i jej skladnikow.
7. Uzytkownik przeglada kolejne formularze.
8. Dane bazowe sa juz wypelnione i nie wymagaja edycji.
9. Uzytkownik uzupelnia pola robocze protokolu.
10. Przy kazdej recepcie uzytkownik ustawia typ i rozmiar etykiety.
11. Aplikacja zapisuje zmiany w lokalnej bazie.
12. Uzytkownik generuje PDF-y.

## Pola edytowane przez uzytkownika

Pola pobrane z Firebirda sa traktowane jako kompletne. Uzytkownik uzupelnia tylko dane, ktorych nie ma w bazie zrodlowej lub ktore wymagaja decyzji przy tworzeniu protokolu.

Przykladowe pola robocze:

- obliczenia i sprawdzenie dawek maksymalnych,
- opis wykonania,
- warunki przechowywania,
- kontrola koncowa,
- wyglad ogolny / jednorodnosc,
- barwa,
- zapach,
- konsystencja / postac,
- klarownosc,
- zgodnosc masy lub objetosci z przewidywana,
- ocena koncowa,
- uwagi.

## Etykiety

Dla kazdej recepty/formularza aplikacja przechowuje:

- typ etykiety: `ZEWNETRZNIE` albo `WEWNETRZNIE`,
- rozmiar etykiety: `Mala` albo `Duza`.

Typ etykiety jest wazny przy grupowaniu wydruku samych etykiet. Rozmiar decyduje o fizycznym ukladzie etykiety.

### Rozmiary etykiet

Obie etykiety drukowane sa na ciaglej rolce Brother QL o szerokosci 62 mm.

Duza etykieta:

- tabela etykiety: 55 mm x 120 mm,
- strona PDF dla Brother QL: 62 mm x 120 mm.

Mala etykieta:

- tabela etykiety: 40 mm x 100 mm,
- strona PDF dla Brother QL: 62 mm x 100 mm.

### Pola etykiety

Wzor etykiety zawiera m.in.:

- typ: `ZEWNETRZNIE` albo `WEWNETRZNIE`,
- numer recepty,
- dane apteki,
- imie i nazwisko pacjenta,
- imie i nazwisko lekarza,
- imie i nazwisko osoby sporzadzajacej lek,
- data sporzadzenia,
- termin waznosci,
- dawkowanie,
- warunki przechowywania,
- sklad / `Rp.`,
- uwagi.

## Generowanie PDF

### Protokoly A4

Protokol jest dokumentem A4.

Dla kazdej recepty aplikacja generuje:

- strona 1: protokol sporzadzenia leku recepturowego A4,
- strona 2: wzor etykiety na stronie A4.

Druga strona A4 sluzy do druku duplexem na odwrocie protokolu. Etykieta na stronie A4 powinna byc renderowana w rzeczywistym rozmiarze wybranym dla danej recepty.

### Same etykiety dla Brother QL

Aplikacja musi miec opcje generowania oddzielnych PDF-ow z samymi etykietami zgodnymi z rozmiarem papieru Brother QL.

Wydruk samych etykiet jest grupowany po typie:

- `naklejki_zewnetrzne.pdf`,
- `naklejki_wewnetrzne.pdf`.

W jednym pliku moga wystepowac etykiety male i duze, bo sa drukowane na tej samej rolce 62 mm. Kazda strona PDF ma szerokosc 62 mm, a wysokosc zalezy od rozmiaru etykiety.

Jesli sterownik Brother QL bedzie wymagal stalych rozmiarow stron w jednym PDF-ie, aplikacja moze pozniej dostac dodatkowy tryb eksportu dzielony po typie i rozmiarze.

## Proponowane ekrany

### Ustawienia

- dane apteki,
- polaczenie z Firebirdem,
- katalog zapisu PDF,
- domyslny typ etykiety,
- domyslny rozmiar etykiety.

### Wyszukiwanie recept

- data od,
- data do,
- adres lub fragment adresu,
- przycisk wyszukiwania,
- lista znalezionych recept,
- import znalezionych recept.

### Lista formularzy

- numer recepty,
- pacjent,
- data,
- adres,
- typ etykiety,
- rozmiar etykiety,
- status formularza.

### Edycja formularza

- dane recepty,
- tabela skladnikow,
- pola robocze protokolu,
- wybor `ZEWNETRZNIE` / `WEWNETRZNIE`,
- wybor `Mala` / `Duza`,
- nawigacja poprzedni / nastepny,
- zapis,
- podglad PDF.

### Generowanie dokumentow

- generuj protokoly A4 z etykieta na drugiej stronie,
- generuj same etykiety `ZEWNETRZNIE`,
- generuj same etykiety `WEWNETRZNIE`.

## Proponowane tabele SQLite

### AppSettings

Przechowuje ustawienia techniczne aplikacji.

Przykladowe pola:

- `Id`,
- `FirebirdHost`,
- `FirebirdDatabasePath`,
- `FirebirdUser`,
- `FirebirdPasswordEncrypted`,
- `OutputDirectory`,
- `DefaultLabelType`,
- `DefaultLabelSize`,
- `CreatedAt`,
- `UpdatedAt`.

### PharmacySettings

Przechowuje dane apteki.

Przykladowe pola:

- `Id`,
- `PharmacyName`,
- `PharmacyAddress`,
- `CreatedAt`,
- `UpdatedAt`.

### ImportedForms

Przechowuje snapshot recepty i pola robocze formularza.

Przykladowe pola:

- `Id`,
- `SourcePrescriptionId`,
- `PrescriptionNumber`,
- `PatientName`,
- `PatientAddress`,
- `DoctorName`,
- `PreparedByName`,
- `PreparationDate`,
- `DrugForm`,
- `ExpiryTermText`,
- `Dosage`,
- `StorageConditions`,
- `LabelType`,
- `LabelSize`,
- `ManualCalculations`,
- `ManualPreparationDescription`,
- `ManualQualityControl`,
- `ManualFinalAssessment`,
- `ManualNotes`,
- `Status`,
- `CreatedAt`,
- `UpdatedAt`.

### ImportedFormIngredients

Przechowuje snapshot skladnikow recepty.

Przykladowe pola:

- `Id`,
- `ImportedFormId`,
- `Lp`,
- `Name`,
- `PrescribedQuantity`,
- `Unit`,
- `UsedQuantity`,
- `BatchNumber`,
- `ExpiryDate`,
- `ManufacturerSupplier`.

### GeneratedDocuments

Przechowuje informacje o wygenerowanych plikach.

Przykladowe pola:

- `Id`,
- `DocumentType`,
- `FilePath`,
- `GeneratedAt`,
- `ImportedFormId`.

## Proponowana technologia

- C# / .NET jako aplikacja desktopowa Windows,
- WPF albo WinForms jako interfejs graficzny,
- FirebirdSql.Data.FirebirdClient do odczytu Firebirda,
- SQLite jako lokalna baza aplikacji,
- QuestPDF do generowania protokolow A4 i etykiet PDF.

## Moduly aplikacji

Proponowany podzial:

- `FirebirdReadService` - wyszukiwanie i odczyt danych z Firebirda,
- `LocalDatabaseService` - zapis snapshotow, ustawien i pol roboczych,
- `ProtocolPdfGenerator` - generowanie protokolu A4,
- `LabelOnA4Generator` - generowanie etykiety na drugiej stronie A4,
- `BrotherLabelGenerator` - generowanie PDF-ow dla Brother QL,
- `FormWorkflowService` - import recept, statusy, przechodzenie miedzy formularzami.

## Statusy formularza

Proponowane statusy:

- `Imported` - formularz zaimportowany,
- `InProgress` - formularz w trakcie uzupelniania,
- `Completed` - formularz uzupelniony,
- `PdfGenerated` - wygenerowano PDF.
