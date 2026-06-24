using Microsoft.Data.Sqlite;
using PharmaExt.App.Models;

namespace PharmaExt.App.Services;

public sealed class LocalDatabaseService
{
    private readonly string _databasePath;

    public LocalDatabaseService(string databasePath = "pharmaext.sqlite")
    {
        _databasePath = databasePath;
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
                PreparationDate TEXT NOT NULL,
                DrugForm TEXT NOT NULL,
                ExpiryTermText TEXT NOT NULL,
                Dosage TEXT NOT NULL,
                StorageConditions TEXT NOT NULL,
                LabelType TEXT NOT NULL,
                LabelSize TEXT NOT NULL,
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
        AddColumnIfMissing(connection, "AppSettings", "MaxUsedQuantityDeviationPercent", "REAL NOT NULL DEFAULT 0.6");
        AddColumnIfMissing(connection, "ImportedForms", "PrescriptionOrderNumber", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "ImportedForms", "PrescriptionBarcode", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "ImportedForms", "MixBeforeUse", "INTEGER NOT NULL DEFAULT 0");
    }

    public void SaveSettings(AppSettings settings)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        Execute(connection, transaction, """
            INSERT INTO AppSettings (
                Id, FirebirdHost, FirebirdDatabasePath, FirebirdUser, FirebirdPasswordEncrypted,
                OutputDirectory, DefaultLabelType, DefaultLabelSize, MaxUsedQuantityDeviationPercent, CreatedAt, UpdatedAt
            )
            VALUES (1, $host, $databasePath, $user, $password, $output, $labelType, $labelSize, $maxDeviationPercent, $now, $now)
            ON CONFLICT(Id) DO UPDATE SET
                FirebirdHost = excluded.FirebirdHost,
                FirebirdDatabasePath = excluded.FirebirdDatabasePath,
                FirebirdUser = excluded.FirebirdUser,
                FirebirdPasswordEncrypted = excluded.FirebirdPasswordEncrypted,
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

    public void UpsertImportedForm(ImportedForm form)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.Now.ToString("O");

        Execute(connection, transaction, """
            INSERT INTO ImportedForms (
                SourcePrescriptionId, PrescriptionNumber, PrescriptionOrderNumber, PrescriptionBarcode, PatientName, PatientAddress, DoctorName,
                PreparedByName, PreparationDate, DrugForm, ExpiryTermText, Dosage, StorageConditions,
                LabelType, LabelSize, ManualCalculations, ManualPreparationDescription,
                ManualQualityControl, ManualFinalAssessment, ManualNotes, MixBeforeUse, Status, CreatedAt, UpdatedAt
            )
            VALUES (
                $sourcePrescriptionId, $prescriptionNumber, $prescriptionOrderNumber, $prescriptionBarcode, $patientName, $patientAddress, $doctorName,
                $preparedByName, $preparationDate, $drugForm, $expiryTermText, $dosage, $storageConditions,
                $labelType, $labelSize, $manualCalculations, $manualPreparationDescription,
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
                PreparationDate = excluded.PreparationDate,
                DrugForm = excluded.DrugForm,
                ExpiryTermText = excluded.ExpiryTermText,
                Dosage = excluded.Dosage,
                StorageConditions = excluded.StorageConditions,
                LabelType = excluded.LabelType,
                LabelSize = excluded.LabelSize,
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
            ("$preparationDate", form.PreparationDate.ToString("O")),
            ("$drugForm", form.DrugForm),
            ("$expiryTermText", form.ExpiryTermText),
            ("$dosage", form.Dosage),
            ("$storageConditions", form.StorageConditions),
            ("$labelType", form.LabelType.ToString()),
            ("$labelSize", form.LabelSize.ToString()),
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

        transaction.Commit();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();
        return connection;
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
}
