using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using FirebirdSql.Data.FirebirdClient;
using PharmaExt.App.Models;

namespace PharmaExt.App.Services;

public sealed class FirebirdPrescriptionReader : IFirebirdPrescriptionReader
{
    private readonly AppSettings _settings;

    public FirebirdPrescriptionReader(AppSettings settings)
    {
        _settings = settings;
    }

    public IReadOnlyList<ImportedForm> Search(DateTime? dateFrom, DateTime? dateTo, string address)
    {
        try
        {
            DiagnosticLogService.LogFirebird("Search", $"Start; from={dateFrom:O}; to={dateTo:O}; filter='{address}'");
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = """
            SELECT
                SPRZ.KODR1,
                SPRZ.NRSRC,
                SPRZ.NRORC,
                SPRZ.EKLCZ,
                SPRZ.KDPLR,
                SPRZ.IDPACA,
                SPRZ.IDLEKA,
                SPRZ.WSKOR AS WSKOR_RECEPTY,
                SPRZ.WSKUS AS WSKUS_RECEPTY,
                COALESCE(
                    CAST(AKSP_LATEST.IDWYC2 AS VARCHAR(30)),
                    CAST(SPRZ.ID_WYC AS VARCHAR(30))
                ) AS ID_WYKONAWCY,
                PERS.NAZWU AS SPORZADZAJACY_NAZWA,
                PACA.NAZWU AS PACJENT_NAZWA,
                PACA.KDPCZ,
                PACA.MIAST,
                PACA.ULICA,
                PACA.NRDOM,
                LEKA.NAZWU AS LEKARZ_NAZWA,
                COALESCE(AKSP_LATEST.DATWZ2, SPRZ.DATWZ) AS TERMIN_WAZNOSCI_LEKU,
                COALESCE(AKSP_LATEST.DGWYC2, SPRZ.DGWYC) AS DATA_WYKONANIA,
                COALESCE(AKSP_LATEST.DGPRZ2, SPRZ.DGPRZ) AS DATA_PRZYJECIA,
                COALESCE(AKSP_LATEST.DATSP1, SPRZ.DATSP) AS DATA_SPRZEDAZY
            FROM SPRZ
            LEFT JOIN AKSP AKSP_LATEST ON AKSP_LATEST.IDSPRZ = SPRZ.ID
                AND AKSP_LATEST.DATAA = (
                    SELECT MAX(AKSP_MAX.DATAA)
                    FROM AKSP AKSP_MAX
                    WHERE AKSP_MAX.IDSPRZ = SPRZ.ID
                )
            LEFT JOIN PACA ON SPRZ.IDPACA = PACA.ID
            LEFT JOIN LEKA ON SPRZ.IDLEKA = LEKA.ID
            LEFT JOIN PERS ON TRIM(COALESCE(
                CAST(AKSP_LATEST.IDWYC2 AS VARCHAR(30)),
                CAST(SPRZ.ID_WYC AS VARCHAR(30))
            )) = TRIM(CAST(PERS.ID AS VARCHAR(30)))
            WHERE SPRZ.DATSP >= @DateFrom
              AND SPRZ.DATSP < @DateTo
              AND SPRZ.TYPSP = '80'
              AND SPRZ.ODPLT = '5'
              AND SPRZ.WSKOR = 0
              AND SPRZ.WSKUS = 0
            ORDER BY SPRZ.DATSP DESC, SPRZ.NRSRC
            """;

            var dateFromValue = dateFrom?.Date ?? DateTime.Today.AddDays(-7);
            var dateToValue = (dateTo?.Date ?? DateTime.Today).AddDays(1);

            command.Parameters.AddWithValue("@DateFrom", dateFromValue);
            command.Parameters.AddWithValue("@DateTo", dateToValue);

            var searchText = address.Trim();
            var results = new List<ImportedForm>();
            var seenPrescriptionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var prescriptionNumber = ReadString(reader, "NRSRC");
                var patientId = ReadString(reader, "IDPACA");
                var drugFormCode = ReadString(reader, "KDPLR");
                var labelMedicineForm = MapDefaultLabelMedicineForm(drugFormCode);

                if (!IsZeroMarker(reader, "WSKOR_RECEPTY") || !IsZeroMarker(reader, "WSKUS_RECEPTY"))
                {
                    continue;
                }

                var preparationDate = ReadNullableDateTime(reader, "DATA_WYKONANIA") ?? DateTime.Today;
                var saleDate = ReadNullableDateTime(reader, "DATA_SPRZEDAZY");

                var form = new ImportedForm
                {
                    SourcePrescriptionId = ReadString(reader, "KODR1"),
                    FirebirdPatientId = patientId,
                    FirebirdDoctorId = ReadString(reader, "IDLEKA"),
                    PrescriptionNumber = prescriptionNumber,
                    PrescriptionOrderNumber = ReadString(reader, "NRORC"),
                    PrescriptionBarcode = ReadString(reader, "EKLCZ"),
                    AcceptanceDate = ReadNullableDateTime(reader, "DATA_PRZYJECIA"),
                    PreparationDate = preparationDate,
                    SaleDate = saleDate,
                    DrugForm = labelMedicineForm,
                    ExpiryTermText = FormatMedicineExpiryDate(ReadNullableDateTime(reader, "TERMIN_WAZNOSCI_LEKU"), preparationDate),
                    Dosage = "",
                    LabelMedicineForm = labelMedicineForm,
                    StorageConditions = "W suchym i chłodnym miejscu, temp. 2-8 st. C",
                    ManualCalculations = "Zgodnie z instrukcją numer: ____________________",
                    ManualPreparationDescription = "Zgodnie z instrukcją numer: ____________________",
                    ManualQualityControl = "Nieprawidłowości nie stwierdzono",
                    PatientName = ReadString(reader, "PACJENT_NAZWA"),
                    PatientAddress = BuildAddress(
                        ReadString(reader, "KDPCZ"),
                        ReadString(reader, "MIAST"),
                        ReadString(reader, "ULICA"),
                        ReadString(reader, "NRDOM")),
                    DoctorName = ReadString(reader, "LEKARZ_NAZWA"),
                    PreparedByName = ReadString(reader, "SPORZADZAJACY_NAZWA", ReadString(reader, "ID_WYKONAWCY"))
                };

                if (MatchesSearchText(form, searchText) && seenPrescriptionKeys.Add(BuildPrescriptionKey(form)))
                {
                    results.Add(form);
                }
            }

            DiagnosticLogService.LogFirebird("Search", $"Done; rows={results.Count}; from={dateFromValue:yyyy-MM-dd}; to={dateToValue:yyyy-MM-dd}");
            return results;
        }
        catch (Exception exception)
        {
            DiagnosticLogService.LogFirebird("Search", $"Error; from={dateFrom:O}; to={dateTo:O}; filter='{address}'", exception);
            throw;
        }
    }

    public void LoadIngredients(ImportedForm form)
    {
        using var connection = CreateConnection();
        connection.Open();
        LoadIngredients(connection, form);
    }

    public void LoadIngredients(IReadOnlyList<ImportedForm> forms)
    {
        using var connection = CreateConnection();
        connection.Open();

        foreach (var form in forms.Where(form => !form.IngredientsLoaded))
        {
            LoadIngredients(connection, form);
            form.IngredientsLoaded = true;
        }
    }

    public IReadOnlyDictionary<string, string> LoadCompositionKeys(IReadOnlyList<ImportedForm> forms)
    {
        using var connection = CreateConnection();
        connection.Open();

        var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var form in forms.Where(form => !string.IsNullOrWhiteSpace(form.SourcePrescriptionId)))
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    LEKI.NAZWA AS SKLADNIK_NAZWA,
                    SPRZ.JDNLR
                FROM SPRZ
                LEFT JOIN LEKI ON SPRZ.IDTOWR = LEKI.IDTOWR
                WHERE SPRZ.KODR1 = @SourcePrescriptionId
                  AND SPRZ.TYPSP = '50'
                  AND SPRZ.POZRC <> '0'
                  AND SPRZ.ILOSP > 0
                  AND SPRZ.WSKOR = 0
                  AND SPRZ.WSKUS = 0
                ORDER BY SPRZ.POZRC
                """;
            command.Parameters.AddWithValue("@SourcePrescriptionId", form.SourcePrescriptionId);

            var ingredientNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (IsCompositionUnit(ReadString(reader, "JDNLR")))
                {
                    var name = NormalizeCompositionText(ReadString(reader, "SKLADNIK_NAZWA"));
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        ingredientNames.Add(name);
                    }
                }
            }

            var key = string.Join("|", ingredientNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(key))
            {
                keys[form.SourcePrescriptionId] = key;
            }
        }

        return keys;
    }

    public IReadOnlyList<SettlementReportRow> LoadSettlementReport(DateTime? dateFrom, DateTime? dateTo, string address)
    {
        try
        {
            DiagnosticLogService.LogFirebird("Settlement", $"Start; from={dateFrom:O}; to={dateTo:O}; filter='{address}'");
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = """
            SELECT
                d.id AS IDDOKF,
                d.dataw,
                d.datas,
                d.datap,
                s.datsp,
                s.KDPLR,
                s.WSFIS,
                COALESCE(CAST(s.NRSRC AS VARCHAR(30)), '') || '/' || COALESCE(CAST(s.NRORC AS VARCHAR(30)), '') AS NRRECEPTY,
                PACA.NAZWU AS PACJENT_NAZWA,
                PACA.KDPCZ,
                PACA.MIAST,
                PACA.ULICA,
                PACA.NRDOM,
                PACA.NRNIP AS PACJENT_NIP,
                PACA.PESEL AS PACJENT_PESEL,
                d.symzr,
                d.idpltn,
                d.idpaca AS DOKF_IDPACA,
                d.uwagi AS DOKF_UWAGI,
                PLTN.NAZWU AS PLATNIK_NAZWA,
                PLTN.KDPCZ AS PLATNIK_KDPCZ,
                PLTN.MIAST AS PLATNIK_MIAST,
                PLTN.ULICA AS PLATNIK_ULICA,
                PLTN.NRDOM AS PLATNIK_NRDOM,
                PLTN.NRNIP AS PLATNIK_NIP,
                PLTN.PESEL AS PLATNIK_PESEL,
                F.NAZW1 AS FIRM_NAZW1,
                F.NAZW2 AS FIRM_NAZW2,
                F.KDPCZ AS FIRM_KDPCZ,
                F.MIAST AS FIRM_MIAST,
                F.ULICA AS FIRM_ULICA,
                F.NRDOM AS FIRM_NRDOM,
                F.NRLOK AS FIRM_NRLOK,
                F.NRNIP AS FIRM_NIP,
                F.REGON AS FIRM_REGON,
                F.TELEF AS FIRM_TELEF,
                F.NRFAX AS FIRM_FAX,
                F.EMAIL AS FIRM_EMAIL,
                F.BNK11 AS FIRM_BANK,
                F.KONT1 AS FIRM_KONTO,
                COALESCE(
                    NULLIF(s.CENUN, 0),
                    NULLIF(s.CENDN, 0),
                    NULLIF(ROUND(COALESCE(s.CENAU, 0) / (1 + COALESCE(NULLIF(s.VATSP, 0), 8) / 100.0000), 2), 0),
                    NULLIF(ROUND(COALESCE(s.CENAD, 0) / (1 + COALESCE(NULLIF(s.VATSP, 0), 8) / 100.0000), 2), 0),
                    ROUND(COALESCE((
                        SELECT SUM(COALESCE(i.CENDN, 0) * COALESCE(i.ILOSP, 0))
                        FROM SPRZ i
                        WHERE i.KODR1 = s.KODR1
                          AND TRIM(CAST(i.TYPSP AS VARCHAR(10))) = '50'
                          AND TRIM(CAST(i.POZRC AS VARCHAR(10))) <> '0'
                          AND COALESCE(i.ILOSP, 0) > 0
                          AND TRIM(CAST(i.WSKOR AS VARCHAR(10))) = '0'
                          AND TRIM(CAST(i.WSKUS AS VARCHAR(10))) = '0'
                    ), 0)
                    + CASE
                        WHEN COALESCE(s.TAXAL, 0) > 0 THEN ROUND(COALESCE(s.TAXAL, 0) / (1 + COALESCE(NULLIF(s.VATSP, 0), 8) / 100.0000), 2) + 0.01
                        ELSE 0
                      END
                    + CASE
                        WHEN COALESCE(s.WRTMR, 0) > 0 THEN ROUND(COALESCE(s.WRTMR, 0) / (1 + COALESCE(NULLIF(s.VATSP, 0), 8) / 100.0000), 2) + 0.01
                        ELSE 0
                      END
                    + CASE
                        WHEN ABS(COALESCE(s.TAXAL, 0) - 31.81) < 0.01
                        THEN ROUND(0.40 / (1 + COALESCE(NULLIF(s.VATSP, 0), 8) / 100.0000), 2)
                        ELSE 0
                      END, 2)
                ) AS RECEPTA_NETTO,
                COALESCE(
                    NULLIF(ROUND(COALESCE(s.CENUN, 0) * (1 + COALESCE(NULLIF(s.VATSP, 0), 8) / 100.0000), 2), 0),
                    NULLIF(ROUND(COALESCE(s.CENDN, 0) * (1 + COALESCE(NULLIF(s.VATSP, 0), 8) / 100.0000), 2), 0),
                    NULLIF(s.CENAU, 0),
                    NULLIF(s.CENAD, 0),
                    COALESCE((
                    SELECT SUM(CEIL(COALESCE(i.CENAD, 0) * COALESCE(i.ILOSP, 0) * 100) / 100.0000)
                    FROM SPRZ i
                    WHERE i.KODR1 = s.KODR1
                      AND TRIM(CAST(i.TYPSP AS VARCHAR(10))) = '50'
                      AND TRIM(CAST(i.POZRC AS VARCHAR(10))) <> '0'
                      AND COALESCE(i.ILOSP, 0) > 0
                      AND TRIM(CAST(i.WSKOR AS VARCHAR(10))) = '0'
                      AND TRIM(CAST(i.WSKUS AS VARCHAR(10))) = '0'
                    ), 0) + COALESCE(s.TAXAL, 0) + COALESCE(s.WRTMR, 0)) AS RECEPTA_BRUTTO,
                COALESCE((
                    SELECT SUM(COALESCE(i.CENDN, 0) * COALESCE(i.ILOSP, 0))
                    FROM SPRZ i
                    WHERE i.KODR1 = s.KODR1
                      AND TRIM(CAST(i.TYPSP AS VARCHAR(10))) = '50'
                      AND TRIM(CAST(i.POZRC AS VARCHAR(10))) <> '0'
                      AND COALESCE(i.ILOSP, 0) > 0
                      AND TRIM(CAST(i.WSKOR AS VARCHAR(10))) = '0'
                      AND TRIM(CAST(i.WSKUS AS VARCHAR(10))) = '0'
                ), 0) AS DBG_SKL_NETTO,
                COALESCE((
                    SELECT SUM(COALESCE(i.CENAD, 0) * COALESCE(i.ILOSP, 0))
                    FROM SPRZ i
                    WHERE i.KODR1 = s.KODR1
                      AND TRIM(CAST(i.TYPSP AS VARCHAR(10))) = '50'
                      AND TRIM(CAST(i.POZRC AS VARCHAR(10))) <> '0'
                      AND COALESCE(i.ILOSP, 0) > 0
                      AND TRIM(CAST(i.WSKOR AS VARCHAR(10))) = '0'
                      AND TRIM(CAST(i.WSKUS AS VARCHAR(10))) = '0'
                ), 0) AS DBG_SKL_BRUTTO,
                COALESCE((
                    SELECT SUM(ROUND(COALESCE(i.CENAD, 0) * COALESCE(i.ILOSP, 0), 2))
                    FROM SPRZ i
                    WHERE i.KODR1 = s.KODR1
                      AND TRIM(CAST(i.TYPSP AS VARCHAR(10))) = '50'
                      AND TRIM(CAST(i.POZRC AS VARCHAR(10))) <> '0'
                      AND COALESCE(i.ILOSP, 0) > 0
                      AND TRIM(CAST(i.WSKOR AS VARCHAR(10))) = '0'
                      AND TRIM(CAST(i.WSKUS AS VARCHAR(10))) = '0'
                ), 0) AS DBG_SKL_BRUTTO_ROUND_LINE,
                COALESCE((
                    SELECT SUM(CEIL(COALESCE(i.CENAD, 0) * COALESCE(i.ILOSP, 0) * 100) / 100.0000)
                    FROM SPRZ i
                    WHERE i.KODR1 = s.KODR1
                      AND TRIM(CAST(i.TYPSP AS VARCHAR(10))) = '50'
                      AND TRIM(CAST(i.POZRC AS VARCHAR(10))) <> '0'
                      AND COALESCE(i.ILOSP, 0) > 0
                      AND TRIM(CAST(i.WSKOR AS VARCHAR(10))) = '0'
                      AND TRIM(CAST(i.WSKUS AS VARCHAR(10))) = '0'
                ), 0) AS DBG_SKL_BRUTTO_CEIL_LINE,
                COALESCE((
                    SELECT SUM((CEIL(COALESCE(i.CENAD, 0) * 100) / 100.0000) * COALESCE(i.ILOSP, 0))
                    FROM SPRZ i
                    WHERE i.KODR1 = s.KODR1
                      AND TRIM(CAST(i.TYPSP AS VARCHAR(10))) = '50'
                      AND TRIM(CAST(i.POZRC AS VARCHAR(10))) <> '0'
                      AND COALESCE(i.ILOSP, 0) > 0
                      AND TRIM(CAST(i.WSKOR AS VARCHAR(10))) = '0'
                      AND TRIM(CAST(i.WSKUS AS VARCHAR(10))) = '0'
                ), 0) AS DBG_SKL_BRUTTO_CEIL_UNIT,
                COALESCE((
                    SELECT COUNT(*)
                    FROM SPRZ i
                    WHERE i.KODR1 = s.KODR1
                      AND TRIM(CAST(i.TYPSP AS VARCHAR(10))) = '50'
                      AND TRIM(CAST(i.POZRC AS VARCHAR(10))) <> '0'
                      AND COALESCE(i.ILOSP, 0) > 0
                      AND TRIM(CAST(i.WSKOR AS VARCHAR(10))) = '0'
                      AND TRIM(CAST(i.WSKUS AS VARCHAR(10))) = '0'
                ), 0) AS DBG_SKL_COUNT,
                COALESCE((
                    SELECT SUM(COALESCE(i.CENDN, 0) * COALESCE(i.ILOSP, 0))
                    FROM SPRZ i
                    WHERE i.KODR1 = s.KODR1
                      AND TRIM(CAST(i.TYPSP AS VARCHAR(10))) = '40'
                      AND TRIM(CAST(i.POZRC AS VARCHAR(10))) <> '0'
                      AND COALESCE(i.ILOSP, 0) > 0
                      AND TRIM(CAST(i.WSKOR AS VARCHAR(10))) = '0'
                      AND TRIM(CAST(i.WSKUS AS VARCHAR(10))) = '0'
                ), 0) AS DBG_40_NETTO,
                COALESCE((
                    SELECT SUM(COALESCE(i.CENAD, 0) * COALESCE(i.ILOSP, 0))
                    FROM SPRZ i
                    WHERE i.KODR1 = s.KODR1
                      AND TRIM(CAST(i.TYPSP AS VARCHAR(10))) = '40'
                      AND TRIM(CAST(i.POZRC AS VARCHAR(10))) <> '0'
                      AND COALESCE(i.ILOSP, 0) > 0
                      AND TRIM(CAST(i.WSKOR AS VARCHAR(10))) = '0'
                      AND TRIM(CAST(i.WSKUS AS VARCHAR(10))) = '0'
                ), 0) AS DBG_40_BRUTTO,
                COALESCE((
                    SELECT COUNT(*)
                    FROM SPRZ i
                    WHERE i.KODR1 = s.KODR1
                      AND TRIM(CAST(i.TYPSP AS VARCHAR(10))) = '40'
                      AND TRIM(CAST(i.POZRC AS VARCHAR(10))) <> '0'
                      AND COALESCE(i.ILOSP, 0) > 0
                      AND TRIM(CAST(i.WSKOR AS VARCHAR(10))) = '0'
                      AND TRIM(CAST(i.WSKUS AS VARCHAR(10))) = '0'
                ), 0) AS DBG_40_COUNT,
                COALESCE(s.CENUN, 0) AS DBG_CENUN,
                COALESCE(s.CENDN, 0) AS DBG_CENDN,
                COALESCE(s.CENAU, 0) AS DBG_CENAU,
                COALESCE(s.CENAD, 0) AS DBG_CENAD,
                COALESCE(s.TAXAL, 0) AS DBG_TAXAL,
                COALESCE(s.WRTMR, 0) AS DBG_WRTMR,
                COALESCE(s.VATSP, 0) AS DBG_VATSP,
                s.zpldl,
                d.zppoz - s.zpldl AS NAD_LIM,
                d.zpcal,
                d.zppoz
            FROM sprz s
            LEFT JOIN DOKF d ON s.iddokf = d.id
            LEFT JOIN PACA ON s.IDPACA = PACA.ID
            LEFT JOIN PACA PLTN ON CASE WHEN COALESCE(d.IDPLTN, 0) > 0 THEN d.IDPLTN ELSE d.IDPACA END = PLTN.ID
            LEFT JOIN FIRM F ON s.IDFIRM = F.ID
            WHERE s.datsp >= @DateFrom
              AND s.datsp < @DateTo
              AND TRIM(CAST(s.wskus AS VARCHAR(10))) = '0'
              AND TRIM(CAST(s.wskor AS VARCHAR(10))) = '0'
              AND TRIM(CAST(s.brwrc AS VARCHAR(10))) = '1'
              AND TRIM(CAST(s.typsp AS VARCHAR(10))) = '80'
              AND TRIM(CAST(s.odplt AS VARCHAR(10))) = '5'
            ORDER BY s.datsp, s.NRSRC
            """;

            var dateFromValue = dateFrom?.Date ?? DateTime.Today.AddDays(-7);
            var dateToValue = (dateTo?.Date ?? DateTime.Today).AddDays(1);
            DiagnosticLogService.LogFirebird("Settlement", $"SQL range; from={dateFromValue:yyyy-MM-dd}; toExclusive={dateToValue:yyyy-MM-dd}");
            command.Parameters.AddWithValue("@DateFrom", dateFromValue);
            command.Parameters.AddWithValue("@DateTo", dateToValue);

            var rows = new List<SettlementReportRow>();
            var searchText = address.Trim();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var invoiceNumber = ReadString(reader, "SYMZR").Trim();
                var row = new SettlementReportRow
                {
                    InvoiceDocumentId = ReadString(reader, "IDDOKF"),
                    InvoiceIssueDate = ReadDateTime(reader, "DATAW"),
                    InvoicePaymentDate = ReadDateTime(reader, "DATAP"),
                    SaleDate = ReadDateTime(reader, "DATSP"),
                    PrescriptionNumber = ReadString(reader, "NRRECEPTY"),
                    PatientName = ReadString(reader, "PACJENT_NAZWA"),
                    PatientAddress = BuildAddress(
                        ReadString(reader, "KDPCZ"),
                        ReadString(reader, "MIAST"),
                        ReadString(reader, "ULICA"),
                        ReadString(reader, "NRDOM")),
                    PatientTaxId = ReadString(reader, "PACJENT_NIP"),
                    PatientPersonalId = ReadString(reader, "PACJENT_PESEL"),
                    InvoiceNumber = string.IsNullOrWhiteSpace(invoiceNumber) ? "brak" : invoiceNumber,
                    InvoiceItemName = GetInvoiceItemName(
                        ReadString(reader, "KDPLR"),
                        ReadDecimal(reader, "DBG_TAXAL"),
                        ReadDecimal(reader, "DBG_WRTMR")),
                    PayerId = ReadString(reader, "IDPLTN") == "0" ? ReadString(reader, "DOKF_IDPACA") : ReadString(reader, "IDPLTN"),
                    PayerName = ReadString(reader, "PLATNIK_NAZWA"),
                    PayerAddress = BuildAddress(
                        ReadString(reader, "PLATNIK_KDPCZ"),
                        ReadString(reader, "PLATNIK_MIAST"),
                        ReadString(reader, "PLATNIK_ULICA"),
                        ReadString(reader, "PLATNIK_NRDOM")),
                    PayerTaxId = ReadString(reader, "PLATNIK_NIP"),
                    PayerPersonalId = ReadString(reader, "PLATNIK_PESEL"),
                    InvoiceNotes = ReadString(reader, "DOKF_UWAGI"),
                    SellerName = ReadString(reader, "FIRM_NAZW1"),
                    SellerOwnerName = ReadString(reader, "FIRM_NAZW2"),
                    SellerAddress = BuildAddress(
                        ReadString(reader, "FIRM_KDPCZ"),
                        ReadString(reader, "FIRM_MIAST"),
                        ReadString(reader, "FIRM_ULICA"),
                        JoinHouseAndFlat(ReadString(reader, "FIRM_NRDOM"), ReadString(reader, "FIRM_NRLOK"))),
                    SellerTaxId = ReadString(reader, "FIRM_NIP"),
                    SellerRegon = ReadString(reader, "FIRM_REGON"),
                    SellerPhone = ReadString(reader, "FIRM_TELEF"),
                    SellerFax = ReadString(reader, "FIRM_FAX"),
                    SellerEmail = ReadString(reader, "FIRM_EMAIL"),
                    SellerBankName = ReadString(reader, "FIRM_BANK"),
                    SellerBankAccount = ReadString(reader, "FIRM_KONTO"),
                    LimitAmount = ReadDecimal(reader, "ZPLDL"),
                    OverLimitAmount = ReadDecimal(reader, "NAD_LIM"),
                    PaymentAmount = ReadDecimal(reader, "ZPPOZ"),
                    InvoiceNetAmount = ReadDecimal(reader, "RECEPTA_NETTO"),
                    InvoiceGrossAmount = ReadDecimal(reader, "RECEPTA_BRUTTO")
                };

                if (string.IsNullOrWhiteSpace(searchText)
                    || Contains(row.PatientName, searchText)
                    || Contains(row.PatientAddress, searchText))
                {
                    DiagnosticLogService.LogFirebird(
                        "Settlement",
                        $"Row; invoice={row.InvoiceNumber}; prescription={row.PrescriptionNumber}; item={row.InvoiceItemName}; kdplr={ReadString(reader, "KDPLR")}; wsfis={ReadString(reader, "WSFIS")}; net={row.InvoiceNetAmount:0.00}; gross={row.InvoiceGrossAmount:0.00}; payment={row.PaymentAmount:0.00}; dbgSklNet={ReadDecimal(reader, "DBG_SKL_NETTO"):0.0000}; dbgSklGross={ReadDecimal(reader, "DBG_SKL_BRUTTO"):0.0000}; dbgSklRoundLine={ReadDecimal(reader, "DBG_SKL_BRUTTO_ROUND_LINE"):0.0000}; dbgSklCeilLine={ReadDecimal(reader, "DBG_SKL_BRUTTO_CEIL_LINE"):0.0000}; dbgSklCeilUnit={ReadDecimal(reader, "DBG_SKL_BRUTTO_CEIL_UNIT"):0.0000}; dbgSklCount={ReadDecimal(reader, "DBG_SKL_COUNT"):0}; dbg40Net={ReadDecimal(reader, "DBG_40_NETTO"):0.0000}; dbg40Gross={ReadDecimal(reader, "DBG_40_BRUTTO"):0.0000}; dbg40Count={ReadDecimal(reader, "DBG_40_COUNT"):0}; dbgCenun={ReadDecimal(reader, "DBG_CENUN"):0.0000}; dbgCendn={ReadDecimal(reader, "DBG_CENDN"):0.0000}; dbgCenau={ReadDecimal(reader, "DBG_CENAU"):0.0000}; dbgCenad={ReadDecimal(reader, "DBG_CENAD"):0.0000}; dbgTaxal={ReadDecimal(reader, "DBG_TAXAL"):0.0000}; dbgWrtmr={ReadDecimal(reader, "DBG_WRTMR"):0.0000}; dbgVatsp={ReadDecimal(reader, "DBG_VATSP"):0.0000}");
                    rows.Add(row);
                }
            }

            if (rows.Count == 0)
            {
                LogSettlementDiagnostics(connection, dateFromValue, dateToValue);
            }

            DiagnosticLogService.LogFirebird("Settlement", $"Done; rows={rows.Count}");
            return rows;
        }
        catch (Exception exception)
        {
            DiagnosticLogService.LogFirebird("Settlement", "Error", exception);
            throw;
        }
    }

    public IReadOnlyList<AnalysisPrescriptionRow> LoadAnalysisReport(DateTime? dateFrom, DateTime? dateTo, string address)
    {
        try
        {
            DiagnosticLogService.LogFirebird("Analysis", $"Start; from={dateFrom:O}; to={dateTo:O}; filter='{address}'");
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = """
            SELECT
                s.KODR1,
                s.DATSP,
                COALESCE(CAST(s.NRSRC AS VARCHAR(30)), '') || '/' || COALESCE(CAST(s.NRORC AS VARCHAR(30)), '') AS NRRECEPTY,
                PACA.NAZWU AS PACJENT_NAZWA,
                PACA.KDPCZ,
                PACA.MIAST,
                PACA.ULICA,
                PACA.NRDOM,
                s.TAXAL,
                s.WRTMR,
                s.VATSP,
                COALESCE(d.ZPPOZ, s.KWDZP, s.ZPLDL, s.ZPLCL, 0) AS ZAPLATA_PACJENTA,
                SUM(COALESCE(i.CENDN, 0) * COALESCE(i.ILOSP, 0)) AS SKLADNIKI_NETTO,
                SUM(COALESCE(i.CENAD, 0) * COALESCE(i.ILOSP, 0)) AS SKLADNIKI_BRUTTO,
                SUM(COALESCE(NULLIF(i.CENJM, 0), NULLIF(i.CENAZ, 0), 0) * COALESCE(i.ILOSP, 0)) AS ZAKUP_NETTO,
                SUM(COALESCE(NULLIF(i.CENJM, 0), NULLIF(i.CENAZ, 0), 0) * COALESCE(i.ILOSP, 0) * COALESCE(i.VATKZ, 0) / 100) AS ZAKUP_VAT
            FROM SPRZ s
            LEFT JOIN SPRZ i ON i.KODR1 = s.KODR1
                AND TRIM(CAST(i.TYPSP AS VARCHAR(10))) = '50'
                AND TRIM(CAST(i.POZRC AS VARCHAR(10))) <> '0'
                AND COALESCE(i.ILOSP, 0) > 0
                AND TRIM(CAST(i.WSKOR AS VARCHAR(10))) = '0'
                AND TRIM(CAST(i.WSKUS AS VARCHAR(10))) = '0'
            LEFT JOIN DOKF d ON s.IDDOKF = d.ID
            LEFT JOIN PACA ON s.IDPACA = PACA.ID
            WHERE s.DATSP >= @DateFrom
              AND s.DATSP < @DateTo
              AND TRIM(CAST(s.TYPSP AS VARCHAR(10))) = '80'
              AND TRIM(CAST(s.ODPLT AS VARCHAR(10))) = '5'
              AND TRIM(CAST(s.WSKOR AS VARCHAR(10))) = '0'
              AND TRIM(CAST(s.WSKUS AS VARCHAR(10))) = '0'
            GROUP BY
                s.KODR1,
                s.DATSP,
                s.NRSRC,
                s.NRORC,
                PACA.NAZWU,
                PACA.KDPCZ,
                PACA.MIAST,
                PACA.ULICA,
                PACA.NRDOM,
                s.TAXAL,
                s.WRTMR,
                s.VATSP,
                d.ZPPOZ,
                s.KWDZP,
                s.ZPLDL,
                s.ZPLCL
            ORDER BY s.DATSP, s.NRSRC
            """;

            var dateFromValue = dateFrom?.Date ?? DateTime.Today.AddDays(-7);
            var dateToValue = (dateTo?.Date ?? DateTime.Today).AddDays(1);
            command.Parameters.AddWithValue("@DateFrom", dateFromValue);
            command.Parameters.AddWithValue("@DateTo", dateToValue);

            var rows = new List<AnalysisPrescriptionRow>();
            var searchText = address.Trim();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var row = new AnalysisPrescriptionRow
                {
                    SourcePrescriptionId = ReadString(reader, "KODR1"),
                    SaleDate = ReadDateTime(reader, "DATSP"),
                    PrescriptionNumber = ReadString(reader, "NRRECEPTY"),
                    PatientName = ReadString(reader, "PACJENT_NAZWA"),
                    PatientAddress = BuildAddress(
                        ReadString(reader, "KDPCZ"),
                        ReadString(reader, "MIAST"),
                        ReadString(reader, "ULICA"),
                        ReadString(reader, "NRDOM")),
                    IngredientsNet = ReadDecimal(reader, "SKLADNIKI_NETTO"),
                    IngredientsGross = ReadDecimal(reader, "SKLADNIKI_BRUTTO"),
                    PurchaseNet = ReadDecimal(reader, "ZAKUP_NETTO"),
                    PurchaseVat = ReadDecimal(reader, "ZAKUP_VAT"),
                    TaxaLaborumGross = ReadDecimal(reader, "TAXAL"),
                    MarginGross = ReadDecimal(reader, "WRTMR"),
                    SaleVatRate = ReadDecimal(reader, "VATSP"),
                    PatientPayment = ReadDecimal(reader, "ZAPLATA_PACJENTA")
                };

                if (string.IsNullOrWhiteSpace(searchText)
                    || Contains(row.PatientName, searchText)
                    || Contains(row.PatientAddress, searchText)
                    || Contains(row.PrescriptionNumber, searchText))
                {
                    row.DisplayNumber = rows.Count + 1;
                    rows.Add(row);
                }
            }

            DiagnosticLogService.LogFirebird("Analysis", $"Done; rows={rows.Count}");
            return rows;
        }
        catch (Exception exception)
        {
            DiagnosticLogService.LogFirebird("Analysis", "Error", exception);
            throw;
        }
    }

    private static void LogSettlementDiagnostics(FbConnection connection, DateTime dateFromValue, DateTime dateToValue)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
            SELECT
                COUNT(*) AS ALL_ROWS,
                SUM(CASE WHEN TRIM(CAST(s.typsp AS VARCHAR(10))) = '80' THEN 1 ELSE 0 END) AS TYPE_80,
                SUM(CASE WHEN TRIM(CAST(s.typsp AS VARCHAR(10))) = '80' AND TRIM(CAST(s.odplt AS VARCHAR(10))) = '5' THEN 1 ELSE 0 END) AS TYPE_80_ODPLT_5,
                SUM(CASE WHEN TRIM(CAST(s.typsp AS VARCHAR(10))) = '80' AND TRIM(CAST(s.odplt AS VARCHAR(10))) = '5' AND TRIM(CAST(s.brwrc AS VARCHAR(10))) = '1' THEN 1 ELSE 0 END) AS TYPE_80_ODPLT_5_BRWRC_1,
                SUM(CASE WHEN TRIM(CAST(s.typsp AS VARCHAR(10))) = '80' AND TRIM(CAST(s.odplt AS VARCHAR(10))) = '5' AND TRIM(CAST(s.brwrc AS VARCHAR(10))) = '1' AND COALESCE(s.iddokf, 0) > 0 THEN 1 ELSE 0 END) AS WITH_DOKF,
                SUM(CASE WHEN TRIM(CAST(s.typsp AS VARCHAR(10))) = '80' AND TRIM(CAST(s.odplt AS VARCHAR(10))) = '5' AND TRIM(CAST(s.brwrc AS VARCHAR(10))) = '1' AND TRIM(CAST(s.wskus AS VARCHAR(10))) = '0' AND TRIM(CAST(s.wskor AS VARCHAR(10))) = '0' THEN 1 ELSE 0 END) AS ACTIVE_MATCH
            FROM sprz s
            WHERE s.datsp >= @DateFrom
              AND s.datsp < @DateTo
            """;
            command.Parameters.AddWithValue("@DateFrom", dateFromValue);
            command.Parameters.AddWithValue("@DateTo", dateToValue);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                DiagnosticLogService.LogFirebird(
                    "Settlement",
                    "Diagnostics; "
                    + $"all={ReadInt32(reader, "ALL_ROWS")}; "
                    + $"type80={ReadInt32(reader, "TYPE_80")}; "
                    + $"type80_odplt5={ReadInt32(reader, "TYPE_80_ODPLT_5")}; "
                    + $"type80_odplt5_brwrc1={ReadInt32(reader, "TYPE_80_ODPLT_5_BRWRC_1")}; "
                    + $"withDokf={ReadInt32(reader, "WITH_DOKF")}; "
                    + $"activeMatch={ReadInt32(reader, "ACTIVE_MATCH")}");
            }
        }
        catch (Exception exception)
        {
            DiagnosticLogService.LogFirebird("Settlement", "Diagnostics failed", exception);
        }
    }

    public IReadOnlyList<InventoryItemRow> LoadInventoryItems(string searchText)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                T.ID,
                T.NRTOW,
                T.NAZWA,
                T.BLOZ07,
                (SELECT SUM(K.ILAKT - COALESCE(K.ILOWS, 0))
                 FROM KZAK K
                 WHERE K.ID > 0
                   AND K.IDFIRM = T.IDFIRM
                   AND K.IDTOWR = T.ID
                   AND K.STNKR <> 'W'
                   AND K.BUFOR = 0
                   AND K.WSKUS = 0
                 PLAN (K INDEX (KZAK_IDTOWR))) AS ILOSC
            FROM TOWR T, BLOZ B
            WHERE T.ID > 0
              AND T.BLOZ07 <> '0000000'
              AND NOT T.BLOZ07 IS NULL
              AND T.BLOZ07 = B.KOD07
              AND NOT (
                  SUBSTRING(B.STATS FROM 53 FOR 1) = 'x'
                  OR SUBSTRING(B.STATS FROM 54 FOR 1) = 'x'
              )
            ORDER BY T.NAZWU
            """;

        var rows = new List<InventoryItemRow>();
        var trimmedSearchText = searchText.Trim();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var row = new InventoryItemRow
            {
                FirebirdId = ReadInt32(reader, "ID"),
                Number = ReadString(reader, "NRTOW"),
                Name = ReadString(reader, "NAZWA"),
                Bloz7 = ReadString(reader, "BLOZ07"),
                TotalQuantity = ReadDecimal(reader, "ILOSC")
            };

            if (string.IsNullOrWhiteSpace(trimmedSearchText)
                || Contains(row.Name, trimmedSearchText)
                || Contains(row.Number, trimmedSearchText)
                || Contains(row.Bloz7, trimmedSearchText))
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    public IReadOnlyList<InventoryDeliveryRow> LoadInventoryDeliveries(int itemId)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                D.SYMZR AS NUMER_FV,
                K.DATWZ,
                K.SERIA,
                K.ILAKT - COALESCE(K.ILOWS, 0) AS ILOSC
            FROM KZAK K
            LEFT JOIN DOKF D ON K.IDDOKF = D.ID
            WHERE K.ID > 0
              AND K.IDTOWR = @ItemId
              AND K.STNKR <> 'W'
              AND K.BUFOR = 0
              AND K.WSKUS = 0
              AND K.ILAKT - COALESCE(K.ILOWS, 0) <> 0
            ORDER BY K.DATWZ, K.SERIA
            """;
        command.Parameters.AddWithValue("@ItemId", itemId);

        var rows = new List<InventoryDeliveryRow>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new InventoryDeliveryRow
            {
                InvoiceNumber = ReadString(reader, "NUMER_FV", "brak"),
                ExpiryDate = ReadNullableDateTime(reader, "DATWZ"),
                BatchNumber = ReadString(reader, "SERIA"),
                RemainingQuantity = ReadDecimal(reader, "ILOSC")
            });
        }

        return rows;
    }

    public void TestConnection()
    {
        using var connection = CreateConnection();
        connection.Open();
    }

    private FbConnection CreateConnection()
    {
        var charset = CleanConnectionValue(_settings.Firebird.Charset);
        var builder = new FbConnectionStringBuilder
        {
            DataSource = CleanConnectionValue(_settings.Firebird.Host),
            Database = CleanConnectionValue(_settings.Firebird.DatabasePath),
            UserID = CleanConnectionValue(_settings.Firebird.User),
            Password = CleanConnectionValue(_settings.Firebird.Password)
        };

        if (!string.IsNullOrWhiteSpace(charset) && !string.Equals(charset, "DOMYSLNE", StringComparison.OrdinalIgnoreCase))
        {
            builder.Charset = charset;
        }

        return new FbConnection(builder.ConnectionString);
    }

    private static string CleanConnectionValue(string value)
    {
        var normalizedValue = value
            .Replace("\u200E", "")
            .Replace("\u200F", "")
            .Replace("\u202A", "")
            .Replace("\u202B", "")
            .Replace("\u202C", "")
            .Replace("\u202D", "")
            .Replace("\u202E", "")
            .Trim();

        var builder = new StringBuilder(normalizedValue.Length);
        foreach (var character in normalizedValue)
        {
            if (!char.IsControl(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private void LoadIngredients(FbConnection connection, ImportedForm form)
    {
        form.Ingredients.Clear();

        using var command = connection.CreateCommand();
        command.CommandText = """
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
              AND SPRZ.TYPSP = '50'
              AND SPRZ.POZRC <> '0'
              AND SPRZ.ILOSP > 0
              AND SPRZ.WSKOR = 0
              AND SPRZ.WSKUS = 0
            ORDER BY SPRZ.POZRC
            """;
        command.Parameters.AddWithValue("@SourcePrescriptionId", form.SourcePrescriptionId);

        var seenIngredientKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var prescribedQuantity = ReadDecimal(reader, "ILOSP");
            var ingredient = new ImportedFormIngredient
            {
                Lp = ReadInt32(reader, "POZRC"),
                Name = ReadString(reader, "SKLADNIK_NAZWA"),
                PrescribedQuantity = prescribedQuantity,
                Unit = ReadString(reader, "JDNLR"),
                BatchNumber = ReadString(reader, "SERIA"),
                ExpiryDate = ReadNullableDateTime(reader, "DATWZ"),
                ManufacturerSupplier = ReadString(reader, "PRODC")
            };
            ingredient.UsedQuantity = GenerateUsedQuantity(form, ingredient);

            if (seenIngredientKeys.Add(BuildIngredientKey(ingredient)))
            {
                form.Ingredients.Add(ingredient);
            }
        }
    }

    private static string BuildAddress(params string[] parts)
    {
        return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string JoinHouseAndFlat(string houseNumber, string flatNumber)
    {
        if (string.IsNullOrWhiteSpace(flatNumber))
        {
            return houseNumber;
        }

        if (string.IsNullOrWhiteSpace(houseNumber))
        {
            return flatNumber;
        }

        return $"{houseNumber}/{flatNumber}";
    }

    private static string GetInvoiceItemName(string recipeFormCode)
    {
        return recipeFormCode.Trim() == "11" ? "Lek jałowy" : "Lek niejałowy";
    }

    private static string GetInvoiceItemName(string recipeFormCode, string sterileFlag)
    {
        var code = recipeFormCode.Trim();
        var isSterile = code == "11" || sterileFlag.Trim().Equals("X", StringComparison.OrdinalIgnoreCase);
        return isSterile ? "Lek ja\u0142owy" : "Lek nieja\u0142owy";
    }

    private static string GetInvoiceItemName(string recipeFormCode, decimal taxaLaborumGross, decimal marginGross)
    {
        var code = recipeFormCode.Trim();
        var hasSterileTaxa = Math.Abs(taxaLaborumGross - 63.63m) < 0.01m;

        return code == "11" || hasSterileTaxa ? "Lek ja\u0142owy" : "Lek nieja\u0142owy";
    }

    private static bool MatchesSearchText(ImportedForm form, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        return Contains(form.PatientName, searchText)
            || Contains(form.PatientAddress, searchText)
            || Contains(form.PrescriptionNumber, searchText);
    }

    private static string BuildPrescriptionKey(ImportedForm form)
    {
        if (!string.IsNullOrWhiteSpace(form.SourcePrescriptionId))
        {
            return form.SourcePrescriptionId;
        }

        return $"{form.PrescriptionNumber}|{form.PreparationDate:O}|{form.PatientName}";
    }

    private static bool Contains(string value, string searchText)
    {
        return value.IndexOf(searchText, StringComparison.CurrentCultureIgnoreCase) >= 0;
    }

    private static bool IsZeroMarker(FbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        if (reader.IsDBNull(ordinal))
        {
            return true;
        }

        var value = reader.GetValue(ordinal);
        if (value is int intValue)
        {
            return intValue == 0;
        }

        if (value is short shortValue)
        {
            return shortValue == 0;
        }

        if (value is long longValue)
        {
            return longValue == 0;
        }

        if (value is decimal decimalValue)
        {
            return decimalValue == 0;
        }

        var text = Convert.ToString(value)?.Trim();
        return string.IsNullOrWhiteSpace(text)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var currentCultureValue) && currentCultureValue == 0
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantCultureValue) && invariantCultureValue == 0;
    }

    private static string MapDrugForm(string code)
    {
        return code.Trim() switch
        {
            "1" => "dla proszków dzielonych - do 20 sztuk",
            "2" => "dla proszków niedzielonych (prostych i złożonych - do 80 gramów)",
            "3" => "dla czopków, globulek oraz pręcików - do 12 sztuk",
            "4" => "dla roztworów, mikstur, zawiesin oraz emulsji do użytku wewnętrznego - do 250 gramów",
            "5" => "dla płynnych leków do stosowania zewnętrznego - do 500 gramów",
            "6" => "dla maści, kremów, mazideł, past oraz żeli - do 100 gramów",
            "7" => "dla kropli do użytku wewnętrznego i zewnętrznego - do 40 gramów",
            "8" => "dla mieszanek ziołowych - do 100 gramów",
            "9" => "dla pigułek - do 30 sztuk",
            "10" => "dla klein - do 500 gramów",
            "11" => "dla kropli do oczu, uszu i nosa w warunkach aseptycznych - do 10 gramów",
            _ => code
        };
    }

    private static string MapDefaultLabelMedicineForm(string code)
    {
        return code.Trim() switch
        {
            "1" => "Pulveres (Pulv.)",
            "3" => "Suppositoria",
            "4" => "Mixtura",
            "5" => "Solutio",
            "6" => "Unguentum",
            "7" => "Guttae",
            "9" => "Pulveres",
            "11" => "Guttae ophthalmicae",
            _ => ""
        };
    }

    private static string FormatMedicineExpiryDate(DateTime? expiryDate, DateTime preparationDate)
    {
        return (expiryDate?.Date ?? preparationDate.Date.AddDays(14)).ToString("dd.MM.yyyy");
    }

    private static string BuildIngredientKey(ImportedFormIngredient ingredient)
    {
        return $"{ingredient.Lp}|{ingredient.Name}|{ingredient.PrescribedQuantity}|{ingredient.Unit}|{ingredient.BatchNumber}";
    }

    private static bool IsCompositionUnit(string unit)
    {
        var normalizedUnit = NormalizeCompositionText(unit);
        return normalizedUnit is "g" or "op";
    }

    private static string NormalizeCompositionText(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        while (normalized.Contains("  ", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("  ", " ");
        }

        return normalized;
    }

    private decimal GenerateUsedQuantity(ImportedForm form, ImportedFormIngredient ingredient)
    {
        if (ingredient.PrescribedQuantity <= 0)
        {
            return ingredient.PrescribedQuantity;
        }

        if (string.Equals(ingredient.Unit.Trim(), "szt", StringComparison.OrdinalIgnoreCase))
        {
            return ingredient.PrescribedQuantity;
        }

        var seed = $"{form.SourcePrescriptionId}|{ingredient.Lp}|{ingredient.Name}|{ingredient.PrescribedQuantity}|{ingredient.Unit}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var fraction = BitConverter.ToUInt32(hash, 0) / (decimal)uint.MaxValue;
        var maxDeviationPercent = Math.Clamp(_settings.MaxUsedQuantityDeviationPercent, 0m, 100m) / 100m;
        var biasedFraction = fraction * fraction;
        var reductionPercent = biasedFraction * maxDeviationPercent;

        var usedQuantity = decimal.Round(ingredient.PrescribedQuantity * (1 - reductionPercent), 3, MidpointRounding.AwayFromZero);
        return Math.Min(usedQuantity, ingredient.PrescribedQuantity);
    }

    private static string ReadString(FbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? "" : Convert.ToString(reader.GetValue(ordinal)) ?? "";
    }

    private static string ReadString(FbDataReader reader, string name, string fallback)
    {
        var value = ReadString(reader, name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static DateTime ReadDateTime(FbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? DateTime.Today : Convert.ToDateTime(reader.GetValue(ordinal));
    }

    private static DateTime? ReadNullableDateTime(FbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }

    private static int ReadInt32(FbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        if (reader.IsDBNull(ordinal))
        {
            return 0;
        }

        var value = reader.GetValue(ordinal);
        if (value is int intValue)
        {
            return intValue;
        }

        if (value is short shortValue)
        {
            return shortValue;
        }

        if (value is long longValue)
        {
            return longValue > int.MaxValue || longValue < int.MinValue ? 0 : (int)longValue;
        }

        if (int.TryParse(Convert.ToString(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue))
        {
            return parsedValue;
        }

        return 0;
    }

    private static decimal ReadDecimal(FbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        if (reader.IsDBNull(ordinal))
        {
            return 0;
        }

        var value = reader.GetValue(ordinal);
        if (value is decimal decimalValue)
        {
            return decimalValue;
        }

        if (value is double doubleValue)
        {
            return Convert.ToDecimal(doubleValue);
        }

        if (value is float floatValue)
        {
            return Convert.ToDecimal(floatValue);
        }

        var text = Convert.ToString(value)?.Trim();
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var currentCultureValue))
        {
            return currentCultureValue;
        }

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantCultureValue))
        {
            return invariantCultureValue;
        }

        return 0;
    }
}
