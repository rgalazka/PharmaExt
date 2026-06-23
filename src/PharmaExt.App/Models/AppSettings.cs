namespace PharmaExt.App.Models;

public sealed class AppSettings
{
    public PharmacySettings Pharmacy { get; set; } = new();
    public FirebirdSettings Firebird { get; set; } = new();
    public string OutputDirectory { get; set; } = "output";
    public LabelType DefaultLabelType { get; set; } = LabelType.Zewnetrznie;
    public LabelSize DefaultLabelSize { get; set; } = LabelSize.Duza;
}

public sealed class PharmacySettings
{
    public string PharmacyName { get; set; } = "Apteka Zdrowie Przy Targu";
    public string PharmacyAddress { get; set; } = "99-100 Leczyca, Przedrynek 6";
}

public sealed class FirebirdSettings
{
    public string Host { get; set; } = "localhost";
    public string DatabasePath { get; set; } = "";
    public string User { get; set; } = "SYSDBA";
    public string Password { get; set; } = "";
}
