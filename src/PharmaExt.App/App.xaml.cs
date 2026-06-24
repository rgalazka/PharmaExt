using System.Globalization;
using System.Threading;
using System.Windows;
using QuestPDF.Infrastructure;

namespace PharmaExt.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("pl-PL").Clone();
        culture.DateTimeFormat.ShortDatePattern = "dd/MM/yyyy";
        culture.DateTimeFormat.DateSeparator = "/";
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        QuestPDF.Settings.License = LicenseType.Community;
        base.OnStartup(e);
    }
}
