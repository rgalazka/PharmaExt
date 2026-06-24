# PharmaExt - mapowanie pol Firebird

Dokument przygotowany na podstawie pliku `powiazania.docx`.

## 1. Dane recepty / formularza

| Pole w PharmaExt | Pole / zrodlo w Firebird | Regula mapowania | Status |
|---|---|---|---|
| `SourcePrescriptionId` | `SPRZ.KODR1` | Przepisac jako techniczne ID recepty / formularza | Ustalone |
| `PrescriptionNumber` | `SPRZ.NRSRC` | Przepisac jako numer recepty | Ustalone |
| `PreparationDate` | `SPRZ.DATSP` + `SPRZ.GDZSP` | `SPRZ.DATSP` to data, `SPRZ.GDZSP` to godzina w sekundach; w aplikacji trzeba zlozyc z tego `DateTime` | Ustalone, do zaimplementowania |
| `DrugForm` | `SPRZ.KDPLR` | Kod postaci leku | Brakuje slownika kodow |
| `ExpiryTermText` | Brak z Firebird | Do wyboru przez uzytkownika, domyslnie `14 dni` | Lokalnie |
| `Dosage` | Brak z Firebird | Do wpisania przez uzytkownika | Lokalnie |
| `StorageConditions` | Brak z Firebird | Do wpisania przez uzytkownika, domyslnie `W suchym i chlodnym miejscu, temp. 2-8 st. C` | Lokalnie |
| `PatientName` | `PACA.NAZWU` | Przepisac jako imie i nazwisko / nazwe pacjenta | Ustalone |
| `PatientAddress` | `PACA.KDPCZ` + `PACA.MIAST` + `PACA.ULICA` + `PACA.NRDOM` | Scalony adres pacjenta | Ustalone |
| `DoctorName` | `LEKA.NAZWU` | Przepisac jako lekarza | Ustalone |
| `PreparedByName` | `PERS.NAZWU` | Laczenie: `SPRZ.ID_SPR = PERS.ID`; awaryjnie pokazac `SPRZ.ID_SPR`, jesli nie ma rekordu w `PERS` | Ustalone |

## 2. Skladniki recepty

Do skladnikow bierzemy pozycje, dla ktorych `SPRZ.POZRC > 0`.

| Pole w PharmaExt | Pole / zrodlo w Firebird | Regula mapowania | Status |
|---|---|---|---|
| `Lp` | `SPRZ.POZRC` | Kolejnosc skladnika; importowac tylko rekordy z wartoscia `> 0` | Ustalone |
| `Name` | `LEKI.NAZWA` | Nazwa skladnika / surowca | Ustalone |
| `PrescribedQuantity` | `SPRZ.ILOSP` | Ilosc przepisana | Ustalone |
| `Unit` | `SPRZ.JDNLR` | Jednostka skladnika | Ustalone |
| `UsedQuantity` | Brak bezposredniego pola | Program moze wygenerowac wartosc losowa +/- 1% z `SPRZ.ILOSP`, pozniej do edycji przez uzytkownika | Lokalnie, do potwierdzenia |
| `BatchNumber` | `KZAK.SERIA` | Numer serii surowca | Ustalone |
| `ExpiryDate` | `KZAK.DATWZ` | Termin waznosci surowca | Ustalone |
| `ManufacturerSupplier` | `LEKI.PRODC` | Producent / dostawca | Ustalone |

## 3. Wyszukiwanie recept

| Parametr w PharmaExt | Pole / zrodlo w Firebird | Regula mapowania | Status |
|---|---|---|---|
| `dateFrom` / `dateTo` | `SPRZ.DATSP` | Szukamy po dacie `SPRZ.DATSP` | Ustalone |
| `address` | Scalony `PatientAddress` | Szukanie po fragmencie scalonego adresu: `PACA.KDPCZ`, `PACA.MIAST`, `PACA.ULICA`, `PACA.NRDOM` | Ustalone |
| recepty na leki robione | `SPRZ.TYPSP`, `SPRZ.ODPLT` | Naglowki recept filtrowac przez `SPRZ.TYPSP = 80 AND SPRZ.ODPLT = 5` | Ustalone |
| skladniki lekow robionych | `SPRZ.TYPSP`, `SPRZ.KODR1`, `SPRZ.POZRC` | Skladniki pobierac po tym samym `SPRZ.KODR1`, z rekordow `SPRZ.TYPSP = 50 AND SPRZ.POZRC > 0` | Ustalone na podstawie przykladowych danych |
| aktywne rekordy | `SPRZ.WSKOR`, `SPRZ.WSKUS` | Pomijac korekty i rekordy usuniete: `SPRZ.WSKOR = 0 AND SPRZ.WSKUS = 0` | Ustalone |

## 4. Powiazania miedzy tabelami

| Relacja | Warunek laczenia |
|---|---|
| Recepta -> pacjent | `SPRZ.IDPACA = PACA.ID` |
| Recepta / pozycja -> zakup / seria | `SPRZ.IDKZAK = KZAK.ID` |
| Recepta / pozycja -> lek / towar | `SPRZ.IDTOWR = LEKI.IDTOWR` |
| Recepta -> lekarz | `SPRZ.IDLEKA = LEKA.ID` |
| Recepta -> osoba sporzadzajaca | `SPRZ.ID_SPR = PERS.ID` |

## 5. Brakuje / do potwierdzenia

1. Slownik dla `SPRZ.KDPLR`, czyli mapowanie kodu postaci leku na opis widoczny w aplikacji, np. `Plyn`, `Masc`, `Proszek`.
2. Czy `PERS.NAZWU` zawsze zawiera pelne imie i nazwisko osoby sporzadzajacej.
3. Czy `SPRZ.KODR1` jednoznacznie grupuje wszystkie pozycje jednej recepty.
4. Czy rekord glowny recepty i rekordy skladnikow sa w tej samej tabeli `SPRZ`, rozrozniane przez `SPRZ.POZRC`.
5. Czy `SPRZ.POZRC = 0` oznacza naglowek recepty, a `SPRZ.POZRC > 0` oznacza skladniki.
6. Czy `SPRZ.GDZSP` zawsze jest liczba sekund od polnocy.
7. Czy `PACA.NAZWU` zawsze zawiera pelne imie i nazwisko pacjenta w gotowej postaci.
8. Czy `LEKA.NAZWU` zawsze zawiera pelne imie i nazwisko lekarza w gotowej postaci.
9. Czy `LEKI.PRODC` oznacza producenta, dostawce, czy pole mieszane.
10. Czy `KZAK.DATWZ` jest terminem waznosci konkretnej serii surowca.
11. Czy `SPRZ.JDNLR` jest gotowym tekstem jednostki, czy kodem wymagajacym slownika.

## 6. Proponowany szkic zapytania

Ponizszy SQL jest szkicem orientacyjnym. Nazwy kolumn sa oparte na dostarczonym mapowaniu, ale trzeba go sprawdzic na realnej strukturze bazy.

```sql
SELECT
    SPRZ.KODR1,
    SPRZ.NRSRC,
    SPRZ.DATSP,
    SPRZ.GDZSP,
    SPRZ.KDPLR,
    SPRZ.ID_SPR,
    PERS.NAZWU AS SPORZADZAJACY_NAZWA,
    PACA.NAZWU AS PACJENT_NAZWA,
    PACA.KDPCZ,
    PACA.MIAST,
    PACA.ULICA,
    PACA.NRDOM,
    LEKA.NAZWU AS LEKARZ_NAZWA,
    SPRZ.POZRC,
    LEKI.NAZWA AS SKLADNIK_NAZWA,
    SPRZ.ILOSP,
    SPRZ.JDNLR,
    KZAK.SERIA,
    KZAK.DATWZ,
    LEKI.PRODC
FROM SPRZ
LEFT JOIN PACA ON SPRZ.IDPACA = PACA.ID
LEFT JOIN KZAK ON SPRZ.IDKZAK = KZAK.ID
LEFT JOIN LEKI ON SPRZ.IDTOWR = LEKI.IDTOWR
LEFT JOIN LEKA ON SPRZ.IDLEKA = LEKA.ID
LEFT JOIN PERS ON SPRZ.ID_SPR = PERS.ID
WHERE SPRZ.DATSP BETWEEN @DateFrom AND @DateTo
  AND SPRZ.TYPSP = 80
  AND SPRZ.ODPLT = 5
  AND SPRZ.WSKOR = 0
  AND SPRZ.WSKUS = 0
ORDER BY SPRZ.KODR1, SPRZ.POZRC;
```

Skladniki dla jednej recepty:

```sql
SELECT
    SPRZ.POZRC,
    LEKI.NAZWA AS SKLADNIK_NAZWA,
    SPRZ.ILOSP,
    SPRZ.JDNLR,
    KZAK.SERIA,
    KZAK.DATWZ,
    LEKI.PRODC
FROM SPRZ
LEFT JOIN KZAK ON SPRZ.IDKZAK = KZAK.ID
LEFT JOIN LEKI ON SPRZ.IDTOWR = LEKI.IDTOWR
WHERE SPRZ.KODR1 = @SourcePrescriptionId
  AND SPRZ.TYPSP = 50
  AND SPRZ.POZRC > 0
  AND SPRZ.WSKOR = 0
  AND SPRZ.WSKUS = 0
ORDER BY SPRZ.POZRC;
```

## 7. Minimalne dane wystarczajace do implementacji importu

Z obecnego mapowania wystarczy do pierwszego importu:

- `SPRZ.KODR1`
- `SPRZ.NRSRC`
- `SPRZ.DATSP`
- `SPRZ.GDZSP`
- `PACA.NAZWU`
- `PACA.KDPCZ`
- `PACA.MIAST`
- `PACA.ULICA`
- `PACA.NRDOM`
- `LEKA.NAZWU`
- `SPRZ.ID_SPR`
- `SPRZ.POZRC`
- `LEKI.NAZWA`
- `SPRZ.ILOSP`
- `SPRZ.JDNLR`
- `KZAK.SERIA`
- `KZAK.DATWZ`
- `LEKI.PRODC`
