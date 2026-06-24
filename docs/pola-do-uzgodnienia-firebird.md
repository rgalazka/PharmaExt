# PharmaExt - pola do uzgodnienia z baza Firebird

Ten dokument sluzy jako lista kontrolna do rozmowy z administratorem lub dostawca systemu z baza Firebird. Celem jest ustalenie, z ktorych tabel i kolumn aplikacja PharmaExt ma pobierac dane recept, pacjentow, lekarzy oraz skladnikow leku recepturowego.

## 1. Dane recepty / formularza

| Pole w aplikacji | Co trzeba ustalic w Firebird | Tabela / kolumna w Firebird | Uwagi |
|---|---|---|---|
| `SourcePrescriptionId` | Techniczne ID recepty, zlecenia lub dokumentu |  | Potrzebne do jednoznacznego powiazania importu ze zrodlem |
| `PrescriptionNumber` | Numer recepty |  | Uwaga na znaki typu `/`, np. `2026/001` |
| `PreparationDate` | Data sporzadzenia, realizacji lub wykonania |  | Trzeba ustalic, ktora data jest wlasciwa |
| `DrugForm` | Postac leku, np. plyn, masc, proszek |  |  |
| `ExpiryTermText` | Termin waznosci, np. `14 dni` |  | Jesli brak w bazie, moze byc uzupelniany lokalnie |
| `Dosage` | Dawkowanie / sposob uzycia |  | Trafia na etykiete |
| `StorageConditions` | Warunki przechowywania |  | Trafia na protokol i etykiete |
| `PatientName` | Imie i nazwisko pacjenta |  |  |
| `PatientAddress` | Adres pacjenta |  | Bedzie tez uzywany do wyszukiwania |
| `DoctorName` | Imie i nazwisko lekarza |  |  |
| `PreparedByName` | Osoba sporzadzajaca lek |  | Farmaceuta / technik / operator |

## 2. Skladniki recepty

Dla kazdej recepty trzeba ustalic tabele pozycji/skladnikow oraz sposob ich powiazania z recepta.

| Pole w aplikacji | Co trzeba ustalic w Firebird | Tabela / kolumna w Firebird | Uwagi |
|---|---|---|---|
| `Lp` | Kolejnosc skladnika na recepcie |  |  |
| `Name` | Nazwa skladnika / surowca |  |  |
| `PrescribedQuantity` | Ilosc przepisana |  |  |
| `Unit` | Jednostka, np. `g`, `ml`, `szt.` |  |  |
| `UsedQuantity` | Ilosc faktycznie uzyta |  | Jesli baza nie ma tej informacji, moze byc uzupelniana lokalnie |
| `BatchNumber` | Numer serii surowca |  | Potrzebne do protokolu |
| `ExpiryDate` | Termin waznosci surowca |  | Potrzebne do protokolu |
| `ManufacturerSupplier` | Producent lub dostawca surowca |  | Jesli dostepne |

## 3. Wyszukiwanie recept

| Parametr w aplikacji | Co trzeba ustalic w Firebird | Tabela / kolumna w Firebird | Uwagi |
|---|---|---|---|
| `dateFrom` / `dateTo` | Po ktorej dacie filtrowac recepty |  | Data recepty, realizacji, sporzadzenia czy sprzedazy |
| `address` | Po ktorym polu adresowym szukac |  | Pelny adres, miejscowosc, ulica, fragment adresu |

## 4. Dane apteki i ustawienia lokalne

Te dane nie musza pochodzic z Firebird. Moga byc ustawieniami aplikacji PharmaExt.

| Pole w aplikacji | Znaczenie | Czy wymagane z Firebird? | Uwagi |
|---|---|---|---|
| `PharmacyName` | Nazwa apteki | Nie | Ustawienie aplikacji |
| `PharmacyAddress` | Adres apteki | Nie | Ustawienie aplikacji |
| `OutputDirectory` | Katalog zapisu PDF | Nie | Ustawienie aplikacji |
| `DefaultLabelType` | Domyslnie `ZEWNETRZNIE` / `WEWNETRZNIE` | Nie | Ustawienie aplikacji |
| `DefaultLabelSize` | Domyslnie `Mala` / `Duza` | Nie | Ustawienie aplikacji |

## 5. Pola uzupelniane lokalnie przez uzytkownika

Te pola moga zostac w aplikacji i nie musza istniec w Firebird.

| Pole w aplikacji | Znaczenie |
|---|---|
| `LabelType` | Typ etykiety: `ZEWNETRZNIE` albo `WEWNETRZNIE` |
| `LabelSize` | Rozmiar etykiety: `Mala` albo `Duza` |
| `ManualCalculations` | Obliczenia i sprawdzenie dawek maksymalnych |
| `ManualPreparationDescription` | Opis wykonania |
| `ManualQualityControl` | Kontrola koncowa |
| `ManualFinalAssessment` | Ocena koncowa |
| `ManualNotes` | Uwagi |
| `Status` | Status formularza w aplikacji |

## 6. Najwazniejsze pytania do administratora Firebird

1. W ktorej tabeli znajduje sie glowny rekord recepty lub zlecenia?
2. Jaka kolumna jest unikalnym ID recepty?
3. Gdzie znajduje sie numer recepty widoczny dla uzytkownika?
4. Ktora data powinna byc uzywana do wyszukiwania: data recepty, realizacji, sporzadzenia czy sprzedazy?
5. W ktorej tabeli sa dane pacjenta i jak lacza sie z recepta?
6. W ktorej tabeli sa dane lekarza i jak lacza sie z recepta?
7. Gdzie zapisana jest osoba sporzadzajaca lek?
8. W ktorej tabeli sa pozycje/skladniki recepty?
9. Jak pozycje/skladniki lacza sie z recepta?
10. Gdzie sa ilosci, jednostki i kolejnosc skladnikow?
11. Czy baza przechowuje numery serii, terminy waznosci oraz producentow/dostawcow surowcow?
12. Czy baza przechowuje dawkowanie i warunki przechowywania?
13. Czy baza przechowuje postac leku i termin waznosci gotowego leku?
14. Czy dane Firebird mozna czytac jednym zapytaniem, czy trzeba uzyc kilku zapytan?
15. Czy uzytkownik aplikacji bedzie mial konto tylko do odczytu?

## 7. Minimalny zestaw potrzebny do pierwszego importu

Do pierwszej dzialajacej wersji importu wystarczy ustalic:

- ID recepty,
- numer recepty,
- date wyszukiwania,
- pacjenta,
- adres pacjenta,
- lekarza,
- osobe sporzadzajaca,
- dawkowanie,
- skladniki,
- ilosci skladnikow,
- jednostki skladnikow.

Pozostale pola, takie jak serie surowcow, terminy waznosci, warunki przechowywania i szczegoly kontroli, mozna dodac w kolejnym etapie albo uzupelniac lokalnie w aplikacji.
