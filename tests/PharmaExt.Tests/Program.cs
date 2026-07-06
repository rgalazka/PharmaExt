using PharmaExt.App.Models;
using PharmaExt.App.Services;

var tests = new (string Name, Action Test)[]
{
    ("Ustawienia zapisuja sie i odczytuja z lokalnej bazy", SettingsArePersisted),
    ("Formularz zapisuje skladniki w lokalnej bazie", ImportedFormIngredientsArePersisted),
    ("Formularz rozpoznaje status IR i PDF", ImportedFormStatusFlagsWork),
    ("Pusty domyslny tekst instrukcji nie zaznacza IR", EmptyInstructionTemplateDoesNotSetIrFlag),
    ("Analiza recepty liczy netto brutto i roznice VAT", AnalysisPrescriptionCalculatesVatDifference),
    ("Dokument IR zapisuje tytul i tresc w lokalnej bazie", QualityDocumentContentIsPersisted),
    ("Wyszukiwanie instrukcji IR dziala po kilku slowach", RecipeInstructionSearchMatchesAllTerms),
    ("Wyszukiwanie instrukcji IR ignoruje polskie znaki", RecipeInstructionSearchNormalizesPolishCharacters)
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Test();
        Console.WriteLine($"OK   {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.WriteLine($"FAIL {test.Name}");
        Console.WriteLine($"     {exception.Message}");
    }
}

Console.WriteLine();
Console.WriteLine(failed == 0
    ? $"Wszystkie testy przeszly: {tests.Length}"
    : $"Testy nieudane: {failed}/{tests.Length}");

return failed == 0 ? 0 : 1;

static void SettingsArePersisted()
{
    WithDatabase(service =>
    {
        var settings = new AppSettings
        {
            Pharmacy =
            {
                PharmacyName = "Apteka Testowa",
                PharmacyAddress = "99-100 Leczyca, Rynek 1"
            },
            Firebird =
            {
                Host = "192.168.1.10",
                DatabasePath = @"D:\BazaApteka\WAPTEKA.FDB",
                User = "SYSDBA",
                Password = "masterkey",
                Charset = "WIN1250"
            },
            OutputDirectory = @"C:\PharmaExt\output",
            DefaultLabelType = LabelType.Wewnetrznie,
            DefaultLabelSize = LabelSize.Mala,
            MaxUsedQuantityDeviationPercent = 0.45m
        };

        service.SaveSettings(settings);
        var loaded = service.LoadSettings();

        AssertEqual("Apteka Testowa", loaded.Pharmacy.PharmacyName, "nazwa apteki");
        AssertEqual("99-100 Leczyca, Rynek 1", loaded.Pharmacy.PharmacyAddress, "adres apteki");
        AssertEqual("192.168.1.10", loaded.Firebird.Host, "host Firebird");
        AssertEqual(@"D:\BazaApteka\WAPTEKA.FDB", loaded.Firebird.DatabasePath, "sciezka Firebird");
        AssertEqual("masterkey", loaded.Firebird.Password, "haslo Firebird");
        AssertEqual(LabelType.Wewnetrznie, loaded.DefaultLabelType, "typ etykiety");
        AssertEqual(LabelSize.Mala, loaded.DefaultLabelSize, "rozmiar etykiety");
        AssertEqual(0.45m, loaded.MaxUsedQuantityDeviationPercent, "maksymalny odchyl");
    });
}

static void ImportedFormIngredientsArePersisted()
{
    WithDatabase(service =>
    {
        var form = new ImportedForm
        {
            SourcePrescriptionId = "FB-1001",
            PrescriptionNumber = "2334/17",
            PrescriptionOrderNumber = "2334",
            PrescriptionBarcode = "1001781583130704613137444105385820330320860",
            PatientName = "OLCZYK WIESLAWA",
            PatientAddress = "99-220 Wartkowice Stary Gostkow 41",
            DoctorName = "JABLONSKI JERZY",
            PreparedByName = "GALAZKA KATARZYNA",
            AcceptanceDate = new DateTime(2026, 6, 12),
            PreparationDate = new DateTime(2026, 6, 12),
            SaleDate = new DateTime(2026, 6, 12),
            DrugForm = "Solutio",
            ExpiryTermText = "26.06.2026",
            Dosage = "2 x dziennie",
            StorageConditions = "w suchym i chlodnym miejscu",
            LabelType = LabelType.Zewnetrznie,
            LabelSize = LabelSize.Duza,
            LabelMedicineForm = "Solutio",
            ManualCalculations = "Zgodnie z instrukcja numer: IR-001",
            ManualPreparationDescription = "Zgodnie z instrukcja numer: IR-001",
            ManualFinalAssessment = "Nieprawidlowosci nie stwierdzono",
            ManualNotes = "Oliwka natluszczajaca",
            MixBeforeUse = true,
            IngredientsLoaded = true
        };

        form.Ingredients.Add(new ImportedFormIngredient
        {
            Lp = 1,
            Name = "Sapo Kalinus",
            PrescribedQuantity = 25m,
            Unit = "g",
            UsedQuantity = 24.947m,
            BatchNumber = "022913",
            ExpiryDate = new DateTime(2027, 5, 12),
            ManufacturerSupplier = "FAGRON SP. Z O.O."
        });
        form.Ingredients.Add(new ImportedFormIngredient
        {
            Lp = 2,
            Name = "Eprus Butelka 250 ml",
            PrescribedQuantity = 1m,
            Unit = "szt",
            UsedQuantity = 1m,
            BatchNumber = "202602507",
            ManufacturerSupplier = "EPRUS SP. Z O.O."
        });

        service.UpsertImportedForm(form);
        var loaded = service.SearchImportedForms(new DateTime(2026, 6, 12), new DateTime(2026, 6, 12), "Olczyk");
        var saved = Single(loaded, "formularz po pacjencie");

        AssertEqual("2334/17", saved.PrescriptionNumber, "numer recepty");
        AssertEqual("OLCZYK WIESLAWA", saved.PatientName, "pacjent");
        AssertEqual("Solutio", saved.LabelMedicineForm, "M.f.");
        Assert(saved.MixBeforeUse, "checkbox ZMIESZAC PRZED UZYCIEM powinien byc zapisany");
        Assert(saved.HasInstruction, "formularz powinien miec status IR po wpisaniu instrukcji");
        AssertEqual(2, saved.Ingredients.Count, "liczba skladnikow");
        AssertEqual("Sapo Kalinus", saved.Ingredients[0].Name, "pierwszy skladnik");
        AssertEqual(24.947m, saved.Ingredients[0].UsedQuantity, "ilosc uzyta");
        AssertEqual("szt", saved.Ingredients[1].Unit, "jednostka opakowania");
    });
}

static void ImportedFormStatusFlagsWork()
{
    WithDatabase(service =>
    {
        var form = new ImportedForm
        {
            SourcePrescriptionId = "FB-1002",
            PrescriptionNumber = "2335/18",
            PatientName = "PACJENT TESTOWY",
            PreparationDate = new DateTime(2026, 6, 13),
            SaleDate = new DateTime(2026, 6, 13),
            ManualCalculations = "",
            ManualPreparationDescription = "Zgodnie z instrukcją numer: IR-123",
            Status = FormStatus.PdfGenerated
        };

        service.UpsertImportedForm(form);
        var saved = Single(service.SearchImportedForms(new DateTime(2026, 6, 13), new DateTime(2026, 6, 13), "TESTOWY"), "formularz ze statusem");

        Assert(saved.HasInstruction, "powinien rozpoznac przypisana instrukcje");
        Assert(saved.HasGeneratedPdf, "powinien rozpoznac wygenerowany PDF");
    });
}

static void EmptyInstructionTemplateDoesNotSetIrFlag()
{
    var form = new ImportedForm
    {
        ManualCalculations = "Zgodnie z instrukcją numer: ____________________",
        ManualPreparationDescription = "Zgodnie z instrukcją numer: "
    };

    Assert(!form.HasInstruction, "domyslny pusty tekst instrukcji nie powinien zaznaczac IR");

    form.ManualCalculations = "Zgodnie z instrukcją numer: IR-001";
    Assert(form.HasInstruction, "kod IR-001 powinien zaznaczac IR");
}

static void AnalysisPrescriptionCalculatesVatDifference()
{
    var row = new AnalysisPrescriptionRow
    {
        IngredientsNet = 233.95m,
        IngredientsGross = 252.67m,
        TaxaLaborumGross = 31.81m,
        MarginGross = 63.63m,
        SaleVatRate = 8m,
        PurchaseNet = 233.95m,
        PurchaseVat = 52.16m,
        PatientPayment = 26.55m
    };

    AssertEqual(348.11m, Math.Round(row.TotalGross, 2), "wartosc brutto recepty");
    AssertEqual(322.32m, Math.Round(row.TotalNet, 2), "wartosc netto recepty");
    AssertEqual(25.79m, Math.Round(row.SalesVat, 2), "VAT sprzedazy");
    AssertEqual(26.37m, Math.Round(row.VatDifference, 2), "roznica VAT zakup minus sprzedaz");
    AssertEqual(88.37m, Math.Round(row.NetProfit, 2), "zysk netto taxa plus marza");
    AssertEqual(62.00m, Math.Round(row.ProfitMinusVat, 2), "zysk netto minus roznica VAT");
}

static void QualityDocumentContentIsPersisted()
{
    WithDatabase(service =>
    {
        var document = new QualityDocument
        {
            Code = "IR-901",
            CategoryCode = "IR",
            CategoryName = "Instrukcje wykonania leku",
            Title = "Masc z ketoprofenem",
            Version = "1.0",
            Status = "Obowiazujacy",
            FilePath = @"Data\QualityDocuments\IR-901_v1.0.docx",
            PreviewHtmlPath = @"Data\QualityDocuments\IR-901_v1.0.html",
            ContentText = "Rp.\nKetoprofenum 10,0\nLekobaza ad 100,0\nM.f. Unguentum",
            IssueDate = new DateTime(2026, 6, 1),
            EffectiveDate = new DateTime(2026, 6, 1)
        };

        service.UpsertQualityDocument(document);
        var loaded = Single(service.LoadQualityDocuments().Where(item => item.Code == "IR-901"), "dokument IR");

        AssertEqual("Masc z ketoprofenem", loaded.Title, "tytul dokumentu");
        Assert(loaded.ContentText.Contains("Ketoprofenum", StringComparison.OrdinalIgnoreCase), "tresc dokumentu powinna zawierac sklad");
        AssertEqual(@"Data\QualityDocuments\IR-901_v1.0.html", loaded.PreviewHtmlPath, "sciezka podgladu");
    });
}

static void RecipeInstructionSearchMatchesAllTerms()
{
    var document = new QualityDocument
    {
        Code = "IR-010",
        Title = "Masc przeciwgrzybicza",
        ContentText = "Rp.\nClotrimazolum 1,0\nLekobaza ad 100,0"
    };

    Assert(RecipeInstructionSearchService.Matches(document, "clotrimazol lekobaza"), "powinno znalezc dokument po dwoch skladnikach");
    Assert(!RecipeInstructionSearchService.Matches(document, "clotrimazol boricum"), "nie powinno znalezc, gdy brakuje jednego slowa");
}

static void RecipeInstructionSearchNormalizesPolishCharacters()
{
    var document = new QualityDocument
    {
        Code = "IR-011",
        Title = "Maść z kwasem borowym",
        ContentText = "Rp.\nAcidum boricum 3,0\nAqua purificata ad 100,0"
    };

    Assert(RecipeInstructionSearchService.Matches(document, "masc kwasem"), "wyszukiwanie powinno ignorowac polskie znaki");
    Assert(RecipeInstructionSearchService.Matches(document, "maść boricum"), "wyszukiwanie powinno dzialac tez z polskimi znakami w zapytaniu");
}

static void WithDatabase(Action<LocalDatabaseService> test)
{
    var root = Path.Combine(Path.GetTempPath(), "PharmaExt.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var databasePath = Path.Combine(root, "pharmaext-test.sqlite");

    try
    {
        var service = new LocalDatabaseService(databasePath);
        service.Initialize();
        test(service);
    }
    finally
    {
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch
        {
            // Test cleanup must not hide the real assertion failure.
        }
    }
}

static T Single<T>(IEnumerable<T> items, string context)
{
    var list = items.ToList();
    AssertEqual(1, list.Count, context);
    return list[0];
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertEqual<T>(T expected, T actual, string name)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{name}: oczekiwano '{expected}', otrzymano '{actual}'");
    }
}
