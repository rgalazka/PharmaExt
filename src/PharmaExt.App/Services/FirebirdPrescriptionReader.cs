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
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                SPRZ.KODR1,
                SPRZ.NRSRC,
                SPRZ.NRORC,
                SPRZ.EKLCZ,
                SPRZ.DATSP,
                SPRZ.GDZSP,
                SPRZ.KDPLR,
                SPRZ.ID_WYC,
                SPRZ.IDPACA,
                SPRZ.IDLEKA,
                PERS.NAZWU AS SPORZADZAJACY_NAZWA,
                PACA.NAZWU AS PACJENT_NAZWA,
                PACA.KDPCZ,
                PACA.MIAST,
                PACA.ULICA,
                PACA.NRDOM,
                LEKA.NAZWU AS LEKARZ_NAZWA,
                MAX(AKSP.DATWZ2) AS TERMIN_WAZNOSCI_LEKU
            FROM SPRZ
            LEFT JOIN PACA ON SPRZ.IDPACA = PACA.ID
            LEFT JOIN LEKA ON SPRZ.IDLEKA = LEKA.ID
            LEFT JOIN PERS ON TRIM(SPRZ.ID_WYC) = TRIM(CAST(PERS.ID AS VARCHAR(30)))
            LEFT JOIN AKSP ON AKSP.IDSPRZ = SPRZ.ID
            WHERE SPRZ.DATSP >= @DateFrom
              AND SPRZ.DATSP < @DateTo
              AND SPRZ.TYPSP = '80'
              AND SPRZ.ODPLT = '5'
              AND SPRZ.WSKOR = '0'
              AND SPRZ.WSKUS = '0'
            GROUP BY
                SPRZ.KODR1,
                SPRZ.NRSRC,
                SPRZ.NRORC,
                SPRZ.EKLCZ,
                SPRZ.DATSP,
                SPRZ.GDZSP,
                SPRZ.KDPLR,
                SPRZ.ID_WYC,
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
                PrescriptionOrderNumber = ReadString(reader, "NRORC"),
                PrescriptionBarcode = ReadString(reader, "EKLCZ"),
                PreparationDate = preparationDate,
                DrugForm = MapDrugForm(ReadString(reader, "KDPLR")),
                ExpiryTermText = FormatMedicineExpiryDate(ReadNullableDateTime(reader, "TERMIN_WAZNOSCI_LEKU"), preparationDate),
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
                PreparedByName = ReadString(reader, "SPORZADZAJACY_NAZWA", ReadString(reader, "ID_WYC"))
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
              AND SPRZ.WSKOR = '0'
              AND SPRZ.WSKUS = '0'
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

    private static string FormatMedicineExpiryDate(DateTime? expiryDate, DateTime preparationDate)
    {
        return (expiryDate?.Date ?? preparationDate.Date.AddDays(14)).ToString("dd.MM.yyyy");
    }

    private static string BuildIngredientKey(ImportedFormIngredient ingredient)
    {
        return $"{ingredient.Lp}|{ingredient.Name}|{ingredient.PrescribedQuantity}|{ingredient.Unit}|{ingredient.BatchNumber}";
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
