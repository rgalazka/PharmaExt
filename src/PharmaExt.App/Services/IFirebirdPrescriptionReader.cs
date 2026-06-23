using PharmaExt.App.Models;

namespace PharmaExt.App.Services;

public interface IFirebirdPrescriptionReader
{
    IReadOnlyList<ImportedForm> Search(DateTime? dateFrom, DateTime? dateTo, string address);
}
