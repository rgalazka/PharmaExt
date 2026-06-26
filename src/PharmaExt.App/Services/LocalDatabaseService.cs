using System.IO;
using Microsoft.Data.Sqlite;
using PharmaExt.App.Models;

namespace PharmaExt.App.Services;

public sealed class LocalDatabaseService
{
    private readonly string _databasePath;

    public LocalDatabaseService(string? databasePath = null)
    {
        _databasePath = databasePath ?? GetDefaultDatabasePath();
    }

    public void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS AppSettings (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                FirebirdHost TEXT NOT NULL,
                FirebirdDatabasePath TEXT NOT NULL,
                FirebirdUser TEXT NOT NULL,
                FirebirdPasswordEncrypted TEXT NOT NULL,
                FirebirdCharset TEXT NOT NULL DEFAULT 'WIN1250',
                OutputDirectory TEXT NOT NULL,
                DefaultLabelType TEXT NOT NULL,
                DefaultLabelSize TEXT NOT NULL,
                MaxUsedQuantityDeviationPercent REAL NOT NULL DEFAULT 0.6,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS PharmacySettings (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                PharmacyName TEXT NOT NULL,
                PharmacyAddress TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ImportedForms (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SourcePrescriptionId TEXT NOT NULL UNIQUE,
                PrescriptionNumber TEXT NOT NULL,
                PrescriptionOrderNumber TEXT NOT NULL DEFAULT '',
                PrescriptionBarcode TEXT NOT NULL DEFAULT '',
                PatientName TEXT NOT NULL,
                PatientAddress TEXT NOT NULL,
                DoctorName TEXT NOT NULL,
                PreparedByName TEXT NOT NULL,
                AcceptanceDate TEXT NULL,
                PreparationDate TEXT NOT NULL,
                SaleDate TEXT NULL,
                DrugForm TEXT NOT NULL,
                ExpiryTermText TEXT NOT NULL,
                Dosage TEXT NOT NULL,
                StorageConditions TEXT NOT NULL,
                LabelType TEXT NOT NULL,
                LabelSize TEXT NOT NULL,
                LabelMedicineForm TEXT NOT NULL DEFAULT 'Solutio',
                ManualCalculations TEXT NOT NULL,
                ManualPreparationDescription TEXT NOT NULL,
                ManualQualityControl TEXT NOT NULL,
                ManualFinalAssessment TEXT NOT NULL,
                ManualNotes TEXT NOT NULL,
                MixBeforeUse INTEGER NOT NULL DEFAULT 0,
                Status TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ImportedFormIngredients (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ImportedFormId INTEGER NOT NULL,
                Lp INTEGER NOT NULL,
                Name TEXT NOT NULL,
                PrescribedQuantity REAL NOT NULL,
                Unit TEXT NOT NULL,
                UsedQuantity REAL NULL,
                BatchNumber TEXT NOT NULL,
                ExpiryDate TEXT NULL,
                ManufacturerSupplier TEXT NOT NULL,
                FOREIGN KEY (ImportedFormId) REFERENCES ImportedForms(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS GeneratedDocuments (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DocumentType TEXT NOT NULL,
                FilePath TEXT NOT NULL,
                GeneratedAt TEXT NOT NULL,
                ImportedFormId INTEGER NULL,
                FOREIGN KEY (ImportedFormId) REFERENCES ImportedForms(Id) ON DELETE SET NULL
            );
            """;
        command.ExecuteNonQuery();
        AddColumnIfMissing(connection, "AppSettings", "FirebirdCharset", "TEXT NOT NULL DEFAULT 'WIN1250'");
        AddColumnIfMissing(connection, "AppSettings", "MaxUsedQuantityDeviationPercent", "REAL NOT NULL DEFAULT 0.6");
        AddColumnIfMissing(connection, "ImportedForms", "PrescriptionOrderNumber", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "ImportedForms", "PrescriptionBarcode", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "ImportedForms", "AcceptanceDate", "TEXT NULL");
        AddColumnIfMissing(connection, "ImportedForms", "SaleDate", "TEXT NULL");
        AddColumnIfMissing(connection, "ImportedForms", "LabelMedicineForm", "TEXT NOT NULL DEFAULT 'Solutio'");
        AddColumnIfMissing(connection, "ImportedForms", "MixBeforeUse", "INTEGER NOT NULL DEFAULT 0");
    }

    public void SaveSettings(AppSettings settings)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        Execute(connection, transaction, """
            INSERT INTO AppSettings (
                Id, FirebirdHost, FirebirdDatabasePath, FirebirdUser, FirebirdPasswordEncrypted,
                FirebirdCharset, OutputDirectory, DefaultLabelType, DefaultLabelSize, MaxUsedQuantityDeviationPercent, CreatedAt, UpdatedAt
            )
            VALUES (1, $host, $databasePath, $user, $password, $charset, $output, $labelType, $labelSize, $maxDeviationPercent, $now, $now)
            ON CONFLICT(Id) DO UPDATE SET
                FirebirdHost = excluded.FirebirdHost,
                FirebirdDatabasePath = excluded.FirebirdDatabasePath,
                FirebirdUser = excluded.FirebirdUser,
                FirebirdPasswordEncrypted = excluded.FirebirdPasswordEncrypted,
                FirebirdCharset = excluded.FirebirdCharset,
                OutputDirectory = excluded.OutputDirectory,
                DefaultLabelType = excluded.DefaultLabelType,
                DefaultLabelSize = excluded.DefaultLabelSize,
                MaxUsedQuantityDeviationPercent = excluded.MaxUsedQuantityDeviationPercent,
                UpdatedAt = excluded.UpdatedAt;
            """,
            ("$host", settings.Firebird.Host),
            ("$databasePath", settings.Firebird.DatabasePath),
            ("$user", settings.Firebird.User),
            ("$password", settings.Firebird.Password),
            ("$charset", settings.Firebird.Charset),
            ("$output", settings.OutputDirectory),
            ("$labelType", settings.DefaultLabelType.ToString()),
            ("$labelSize", settings.DefaultLabelSize.ToString()),
            ("$maxDeviationPercent", settings.MaxUsedQuantityDeviationPercent),
            ("$now", DateTime.Now.ToString("O")));

        Execute(connection, transaction, """
            INSERT INTO PharmacySettings (Id, PharmacyName, PharmacyAddress, CreatedAt, UpdatedAt)
            VALUES (1, $name, $address, $now, $now)
            ON CONFLICT(Id) DO UPDATE SET
                PharmacyName = excluded.PharmacyName,
                PharmacyAddress = excluded.PharmacyAddress,
                UpdatedAt = excluded.UpdatedAt;
            """,
            ("$name", settings.Pharmacy.PharmacyName),
            ("$address", settings.Pharmacy.PharmacyAddress),
            ("$now", DateTime.Now.ToString("O")));

        transaction.Commit();
    }

    public AppSettings LoadSettings()
    {
        var settings = new AppSettings();
        using var connection = OpenConnection();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT
                    FirebirdHost,
                    FirebirdDatabasePath,
                    FirebirdUser,
                    FirebirdPasswordEncrypted,
                    FirebirdCharset,
                    OutputDirectory,
                    DefaultLabelType,
                    DefaultLabelSize,
                    MaxUsedQuantityDeviationPercent
                FROM AppSettings
                WHERE Id = 1;
                """;

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                settings.Firebird.Host = ReadString(reader, "FirebirdHost", settings.Firebird.Host);
                settings.Firebird.DatabasePath = ReadString(reader, "FirebirdDatabasePath", settings.Firebird.DatabasePath);
                settings.Firebird.User = ReadString(reader, "FirebirdUser", settings.Firebird.User);
                settings.Firebird.Password = ReadString(reader, "FirebirdPasswordEncrypted", settings.Firebird.Password);
                settings.Firebird.Charset = ReadString(reader, "FirebirdCharset", settings.Firebird.Charset);
                settings.OutputDirectory = ReadString(reader, "OutputDirectory", settings.OutputDirectory);
                settings.DefaultLabelType = ReadEnum(reader, "DefaultLabelType", settings.DefaultLabelType);
                settings.DefaultLabelSize = ReadEnum(reader, "DefaultLabelSize", settings.DefaultLabelSize);
                settings.MaxUsedQuantityDeviationPercent = ReadDecimal(reader, "MaxUsedQuantityDeviationPercent", settings.MaxUsedQuantityDeviationPercent);
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT PharmacyName, PharmacyAddress
                FROM PharmacySettings
                WHERE Id = 1;
                """;

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                settings.Pharmacy.PharmacyName = ReadString(reader, "PharmacyName", settings.Pharmacy.PharmacyName);
                settings.Pharmacy.PharmacyAddress = ReadString(reader, "PharmacyAddress", settings.Pharmacy.PharmacyAddress);
            }
        }

        return settings;
    }

    public void UpsertImportedForm(ImportedForm form)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.Now.ToString("O");

        Execute(connection, transaction, """
            INSERT INTO ImportedForms (
                SourcePrescriptionId, PrescriptionNumber, PrescriptionOrderNumber, PrescriptionBarcode, PatientName, PatientAddress, DoctorName,
                PreparedByName, AcceptanceDate, PreparationDate, SaleDate, DrugForm, ExpiryTermText, Dosage, StorageConditions,
                LabelType, LabelSize, LabelMedicineForm, ManualCalculations, ManualPreparationDescription,
                ManualQualityControl, ManualFinalAssessment, ManualNotes, MixBeforeUse, Status, CreatedAt, UpdatedAt
            )
            VALUES (
                $sourcePrescriptionId, $prescriptionNumber, $prescriptionOrderNumber, $prescriptionBarcode, $patientName, $patientAddress, $doctorName,
                $preparedByName, $acceptanceDate, $preparationDate, $saleDate, $drugForm, $expiryTermText, $dosage, $storageConditions,
                $labelType, $labelSize, $labelMedicineForm, $manualCalculations, $manualPreparationDescription,
                $manualQualityControl, $manualFinalAssessment, $manualNotes, $mixBeforeUse, $status, $now, $now
            )
            ON CONFLICT(SourcePrescriptionId) DO UPDATE SET
                PrescriptionNumber = excluded.PrescriptionNumber,
                PrescriptionOrderNumber = excluded.PrescriptionOrderNumber,
                PrescriptionBarcode = excluded.PrescriptionBarcode,
                PatientName = excluded.PatientName,
                PatientAddress = excluded.PatientAddress,
                DoctorName = excluded.DoctorName,
                PreparedByName = excluded.PreparedByName,
                AcceptanceDate = excluded.AcceptanceDate,
                PreparationDate = excluded.PreparationDate,
                SaleDate = excluded.SaleDate,
                DrugForm = excluded.DrugForm,
                ExpiryTermText = excluded.ExpiryTermText,
                Dosage = excluded.Dosage,
                StorageConditions = excluded.StorageConditions,
                LabelType = excluded.LabelType,
                LabelSize = excluded.LabelSize,
                LabelMedicineForm = excluded.LabelMedicineForm,
                ManualCalculations = excluded.ManualCalculations,
                ManualPreparationDescription = excluded.ManualPreparationDescription,
                ManualQualityControl = excluded.ManualQualityControl,
                ManualFinalAssessment = excluded.ManualFinalAssessment,
                ManualNotes = excluded.ManualNotes,
                MixBeforeUse = excluded.MixBeforeUse,
                Status = excluded.Status,
                UpdatedAt = excluded.UpdatedAt;
            """,
            ("$sourcePrescriptionId", form.SourcePrescriptionId),
            ("$prescriptionNumber", form.PrescriptionNumber),
            ("$prescriptionOrderNumber", form.PrescriptionOrderNumber),
            ("$prescriptionBarcode", form.PrescriptionBarcode),
            ("$patientName", form.PatientName),
            ("$patientAddress", form.PatientAddress),
            ("$doctorName", form.DoctorName),
            ("$preparedByName", form.PreparedByName),
            ("$acceptanceDate", form.AcceptanceDate?.ToString("O")),
            ("$preparationDate", form.PreparationDate.ToString("O")),
            ("$saleDate", form.SaleDate?.ToString("O")),
            ("$drugForm", form.DrugForm),
            ("$expiryTermText", form.ExpiryTermText),
            ("$dosage", form.Dosage),
            ("$storageConditions", form.StorageConditions),
            ("$labelType", form.LabelType.ToString()),
            ("$labelSize", form.LabelSize.ToString()),
            ("$labelMedicineForm", form.LabelMedicineForm),
            ("$manualCalculations", form.ManualCalculations),
            ("$manualPreparationDescription", form.ManualPreparationDescription),
            ("$manualQualityControl", form.ManualQualityControl),
            ("$manualFinalAssessment", form.ManualFinalAssessment),
            ("$manualNotes", form.ManualNotes),
            ("$mixBeforeUse", form.MixBeforeUse ? 1 : 0),
            ("$status", form.Status.ToString()),
            ("$now", now));

        using var idCommand = connection.CreateCommand();
        idCommand.Transaction = transaction;
        idCommand.CommandText = "SELECT Id FROM ImportedForms WHERE SourcePrescriptionId = $sourcePrescriptionId;";
        idCommand.Parameters.AddWithValue("$sourcePrescriptionId", form.SourcePrescriptionId);
        var formId = Convert.ToInt32(idCommand.ExecuteScalar());

        if (form.IngredientsLoaded)
        {
            Execute(connection, transaction, "DELETE FROM ImportedFormIngredients WHERE ImportedFormId = $formId;", ("$formId", formId));

            foreach (var ingredient in form.Ingredients)
            {
                Execute(connection, transaction, """
                    INSERT INTO ImportedFormIngredients (
                        ImportedFormId, Lp, Name, PrescribedQuantity, Unit, UsedQuantity,
                        BatchNumber, ExpiryDate, ManufacturerSupplier
                    )
                    VALUES (
                        $formId, $lp, $name, $prescribedQuantity, $unit, $usedQuantity,
                        $batchNumber, $expiryDate, $manufacturerSupplier
                    );
                    """,
                    ("$formId", formId),
                    ("$lp", ingredient.Lp),
                    ("$name", ingredient.Name),
                    ("$prescribedQuantity", ingredient.PrescribedQuantity),
                    ("$unit", ingredient.Unit),
                    ("$usedQuantity", ingredient.UsedQuantity),
                    ("$batchNumber", ingredient.BatchNumber),
                    ("$expiryDate", ingredient.ExpiryDate?.ToString("O")),
                    ("$manufacturerSupplier", ingredient.ManufacturerSupplier));
            }
        }

        transaction.Commit();
    }

    public void ResetLocalData()
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        Execute(connection, transaction, "DELETE FROM ImportedFormIngredients;");
        Execute(connection, transaction, "DELETE FROM GeneratedDocuments;");
        Execute(connection, transaction, "DELETE FROM ImportedForms;");

        transaction.Commit();
    }

    public IReadOnlyList<ImportedForm> SearchImportedForms(DateTime? dateFrom, DateTime? dateTo, string searchText)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                Id,
                SourcePrescriptionId,
                PrescriptionNumber,
                PrescriptionOrderNumber,
                PrescriptionBarcode,
                PatientName,
                PatientAddress,
                DoctorName,
                PreparedByName,
                AcceptanceDate,
                PreparationDate,
                SaleDate,
                DrugForm,
                ExpiryTermText,
                Dosage,
                StorageConditions,
                LabelType,
                LabelSize,
                LabelMedicineForm,
                ManualCalculations,
                ManualPreparationDescription,
                ManualQualityControl,
                ManualFinalAssessment,
                ManualNotes,
                MixBeforeUse,
                Status
            FROM ImportedForms
            WHERE PreparationDate >= $dateFrom
              AND PreparationDate < $dateTo
              AND (
                  $searchText = ''
                  OR PatientName LIKE $searchPattern
                  OR PatientAddress LIKE $searchPattern
                  OR PrescriptionNumber LIKE $searchPattern
              )
            ORDER BY PreparationDate DESC, PrescriptionNumber;
            """;

        var dateFromValue = (dateFrom?.Date ?? DateTime.Today.AddDays(-7)).ToString("O");
        var dateToValue = (dateTo?.Date ?? DateTime.Today).AddDays(1).ToString("O");
        var trimmedSearchText = searchText.Trim();
        command.Parameters.AddWithValue("$dateFrom", dateFromValue);
        command.Parameters.AddWithValue("$dateTo", dateToValue);
        command.Parameters.AddWithValue("$searchText", trimmedSearchText);
        command.Parameters.AddWithValue("$searchPattern", $"%{trimmedSearchText}%");

        var forms = new List<ImportedForm>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var form = new ImportedForm
            {
                Id = ReadInt32(reader, "Id"),
                SourcePrescriptionId = ReadString(reader, "SourcePrescriptionId", ""),
                PrescriptionNumber = ReadString(reader, "PrescriptionNumber", ""),
                PrescriptionOrderNumber = ReadString(reader, "PrescriptionOrderNumber", ""),
                PrescriptionBarcode = ReadString(reader, "PrescriptionBarcode", ""),
                PatientName = ReadString(reader, "PatientName", ""),
                PatientAddress = ReadString(reader, "PatientAddress", ""),
                DoctorName = ReadString(reader, "DoctorName", ""),
                PreparedByName = ReadString(reader, "PreparedByName", ""),
                AcceptanceDate = ReadNullableDateTime(reader, "AcceptanceDate"),
                PreparationDate = ReadDateTime(reader, "PreparationDate", DateTime.Today),
                SaleDate = ReadNullableDateTime(reader, "SaleDate"),
                DrugForm = ReadString(reader, "DrugForm", ""),
                ExpiryTermText = ReadString(reader, "ExpiryTermText", ""),
                Dosage = ReadString(reader, "Dosage", ""),
                StorageConditions = ReadString(reader, "StorageConditions", ""),
                LabelType = ReadEnum(reader, "LabelType", LabelType.Zewnetrznie),
                LabelSize = ReadEnum(reader, "LabelSize", LabelSize.Duza),
                LabelMedicineForm = ReadString(reader, "LabelMedicineForm", ""),
                ManualCalculations = ReadString(reader, "ManualCalculations", ""),
                ManualPreparationDescription = ReadString(reader, "ManualPreparationDescription", ""),
                ManualQualityControl = ReadString(reader, "ManualQualityControl", ""),
                ManualFinalAssessment = ReadString(reader, "ManualFinalAssessment", ""),
                ManualNotes = ReadString(reader, "ManualNotes", ""),
                MixBeforeUse = ReadBoolean(reader, "MixBeforeUse"),
                Status = ReadEnum(reader, "Status", FormStatus.Imported),
                IngredientsLoaded = true
            };

            LoadIngredients(connection, form);
            forms.Add(form);
        }

        return forms;
    }

    private SqliteConnection OpenConnection()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();
        return connection;
    }

    private static string GetDefaultDatabasePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "PharmaExt", "pharmaext.sqlite");
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        command.ExecuteNonQuery();
    }

    private static void AddColumnIfMissing(SqliteConnection connection, string tableName, string columnName, string definition)
    {
        using var schemaCommand = connection.CreateCommand();
        schemaCommand.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = schemaCommand.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(Convert.ToString(reader["name"]), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition};";
        command.ExecuteNonQuery();
    }

    private static string ReadString(SqliteDataReader reader, string name, string fallback)
    {
        var ordinal = reader.GetOrdinal(name);
        if (reader.IsDBNull(ordinal))
        {
            return fallback;
        }

        var value = Convert.ToString(reader.GetValue(ordinal));
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static TEnum ReadEnum<TEnum>(SqliteDataReader reader, string name, TEnum fallback)
        where TEnum : struct
    {
        var value = ReadString(reader, name, "");
        return Enum.TryParse<TEnum>(value, out var parsedValue) ? parsedValue : fallback;
    }

    private static decimal ReadDecimal(SqliteDataReader reader, string name, decimal fallback)
    {
        var ordinal = reader.GetOrdinal(name);
        if (reader.IsDBNull(ordinal))
        {
            return fallback;
        }

        return decimal.TryParse(Convert.ToString(reader.GetValue(ordinal)), out var value) ? value : fallback;
    }

    private static int ReadInt32(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static bool ReadBoolean(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return !reader.IsDBNull(ordinal) && Convert.ToInt32(reader.GetValue(ordinal)) != 0;
    }

    private static DateTime ReadDateTime(SqliteDataReader reader, string name, DateTime fallback)
    {
        var value = ReadString(reader, name, "");
        return DateTime.TryParse(value, out var parsedValue) ? parsedValue : fallback;
    }

    private static DateTime? ReadNullableDateTime(SqliteDataReader reader, string name)
    {
        var value = ReadString(reader, name, "");
        return DateTime.TryParse(value, out var parsedValue) ? parsedValue : null;
    }

    private static decimal ReadNullableDecimalAsZero(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        if (reader.IsDBNull(ordinal))
        {
            return 0;
        }

        return Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private static decimal? ReadNullableDecimal(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private static void LoadIngredients(SqliteConnection connection, ImportedForm form)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                Id,
                ImportedFormId,
                Lp,
                Name,
                PrescribedQuantity,
                Unit,
                UsedQuantity,
                BatchNumber,
                ExpiryDate,
                ManufacturerSupplier
            FROM ImportedFormIngredients
            WHERE ImportedFormId = $formId
            ORDER BY Lp, Id;
            """;
        command.Parameters.AddWithValue("$formId", form.Id);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            form.Ingredients.Add(new ImportedFormIngredient
            {
                Id = ReadInt32(reader, "Id"),
                ImportedFormId = ReadInt32(reader, "ImportedFormId"),
                Lp = ReadInt32(reader, "Lp"),
                Name = ReadString(reader, "Name", ""),
                PrescribedQuantity = ReadNullableDecimalAsZero(reader, "PrescribedQuantity"),
                Unit = ReadString(reader, "Unit", ""),
                UsedQuantity = ReadNullableDecimal(reader, "UsedQuantity"),
                BatchNumber = ReadString(reader, "BatchNumber", ""),
                ExpiryDate = ReadNullableDateTime(reader, "ExpiryDate"),
                ManufacturerSupplier = ReadString(reader, "ManufacturerSupplier", "")
            });
        }
    }
}
