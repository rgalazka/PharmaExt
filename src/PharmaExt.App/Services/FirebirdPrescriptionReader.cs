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
        // TODO: Uzupelnic po poznaniu struktury tabel Firebird.
        // Ta klasa celowo wykonuje tylko odczyt z bazy zrodlowej.
        var builder = new FbConnectionStringBuilder
        {
            DataSource = _settings.Firebird.Host,
            Database = _settings.Firebird.DatabasePath,
            UserID = _settings.Firebird.User,
            Password = _settings.Firebird.Password,
            Charset = "UTF8"
        };

        using var connection = new FbConnection(builder.ConnectionString);
        connection.Open();

        return [];
    }
}
