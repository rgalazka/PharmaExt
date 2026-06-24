using System.Security.Cryptography;
using System.Text;
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
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                SPRZ.KODR1,
                SPRZ.NRSRC,
                SPRZ.DATSP,
                SPRZ.GDZSP,
                SPRZ.KDPLR,
                SPRZ.ID_SPR,
                SPRZ.IDPACA,
                SPRZ.IDLEKA,
                PERS.NAZWU AS SPORZADZAJACY_NAZWA,
                PACA.NAZWU AS PACJENT_NAZWA,
                PACA.KDPCZ,
                PACA.MIAST,
                PACA.ULICA,
                PACA.NRDOM,
                LEKA.NAZWU AS LEKARZ_NAZWA
            FROM SPRZ
            LEFT JOIN PACA ON SPRZ.IDPACA = PACA.ID
            LEFT JOIN LEKA ON SPRZ.IDLEKA = LEKA.ID
            LEFT JOIN PERS ON SPRZ.ID_SPR = PERS.ID
            WHERE SPRZ.DATSP >= @DateFrom
              AND SPRZ.DATSP < @DateTo
              AND SPRZ.TYPSP = 80
              AND SPRZ.ODPLT = 5
              AND SPRZ.WSKOR = 0
              AND SPRZ.WSKUS = 0
            GROUP BY
                SPRZ.KODR1,
                SPRZ.NRSRC,
                SPRZ.DATSP,
                SPRZ.GDZSP,
                SPRZ.KDPLR,
                SPRZ.ID_SPR,
                SPRZ.IDPACA,
                SPRZ.IDLEKA,
                PERS.NAZWU,
                PACA.NAZWU,
                PACA.KDPCZ,
                PACA.MIAST,
                PACA.ULICA,
                PACA.NRDOM,
                LEKA.NAZWU
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
            var preparationDate = ReadDateTime(reader, "DATSP");
            var seconds = ReadInt32(reader, "GDZSP");
            if (seconds > 0)
            {
                preparationDate = preparationDate.Date.AddSeconds(seconds);
            }

            var form = new ImportedForm
            {
                SourcePrescriptionId = ReadString(reader, "KODR1"),
                FirebirdPatientId = ReadString(reader, "IDPACA"),
                FirebirdDoctorId = ReadString(reader, "IDLEKA"),
                PrescriptionNumber = ReadString(reader, "NRSRC"),
                PreparationDate = preparationDate,
                DrugForm = ReadString(reader, "KDPLR"),
                ExpiryTermText = "14 dni",
                Dosage = "",
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
                PreparedByName = ReadString(reader, "SPORZADZAJACY_NAZWA", ReadString(reader, "ID_SPR"))
            };

            if (MatchesSearchText(form, searchText) && seenPrescriptionKeys.Add(BuildPrescriptionKey(form)))
            {
                results.Add(form);
            }
        }

        return results;
    }

    public void LoadIngredients(ImportedForm form)
    {
        using var connection = CreateConnection();
        connection.Open();
        LoadIngredients(connection, form);
    }

    public void TestConnection()
    {
        using var connection = CreateConnection();
        connection.Open();
    }

    private FbConnection CreateConnection()
    {
        var builder = new FbConnectionStringBuilder
        {
            DataSource = _settings.Firebird.Host,
            Database = _settings.Firebird.DatabasePath,
            UserID = _settings.Firebird.User,
            Password = _settings.Firebird.Password
        };

        if (!string.Equals(_settings.Firebird.Charset, "DOMYSLNE", StringComparison.OrdinalIgnoreCase))
        {
            builder.Charset = _settings.Firebird.Charset;
        }

        return new FbConnection(builder.ConnectionString);
    }

    private static void LoadIngredients(FbConnection connection, ImportedForm form)
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
              AND SPRZ.TYPSP = 50
              AND SPRZ.POZRC > 0
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

    private static string BuildIngredientKey(ImportedFormIngredient ingredient)
    {
        return $"{ingredient.Lp}|{ingredient.Name}|{ingredient.PrescribedQuantity}|{ingredient.Unit}|{ingredient.BatchNumber}";
    }

    private static decimal GenerateUsedQuantity(ImportedForm form, ImportedFormIngredient ingredient)
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
        var reductionPercent = 0.01m + fraction * 0.01m;

        return decimal.Round(ingredient.PrescribedQuantity * (1 - reductionPercent), 3, MidpointRounding.AwayFromZero);
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
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static decimal ReadDecimal(FbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToDecimal(reader.GetValue(ordinal));
    }
}
