using PharmaExt.App.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PharmaExt.App.Pdf;

public sealed class SettlementInvoicePdfGenerator
{
    private const decimal VatRate = 8m;

    public void Generate(
        IReadOnlyList<SettlementReportRow> rows,
        PharmacySettings pharmacy,
        string filePath)
    {
        GenerateMany(new[] { rows }, pharmacy, filePath);
    }

    public void GenerateMany(
        IReadOnlyList<IReadOnlyList<SettlementReportRow>> invoices,
        PharmacySettings pharmacy,
        string filePath)
    {
        var invoiceGroups = invoices.Where(rows => rows.Count > 0).ToList();
        if (invoiceGroups.Count == 0)
        {
            throw new InvalidOperationException("Brak pozycji faktury.");
        }

        Document.Create(document =>
        {
            foreach (var rows in invoiceGroups)
            {
                ComposeInvoicePage(document, rows, pharmacy);
            }
        }).GeneratePdf(filePath);
    }

    private static void ComposeInvoicePage(
        IDocumentContainer document,
        IReadOnlyList<SettlementReportRow> rows,
        PharmacySettings pharmacy)
    {
        var first = rows[0];
        var sellerName = DefaultIfEmpty(first.SellerName, pharmacy.PharmacyName);
        var sellerAddress = DefaultIfEmpty(first.SellerAddress, pharmacy.PharmacyAddress);
        var netTotal = first.InvoiceNetAmount > 0
            ? first.InvoiceNetAmount
            : (first.InvoiceGrossAmount > 0 ? first.InvoiceGrossAmount / (1 + VatRate / 100) : rows.Sum(row => row.PaymentAmount) / (1 + VatRate / 100));
        var grossTotal = decimal.Round(netTotal * (1 + VatRate / 100), 2, MidpointRounding.AwayFromZero);
        var paymentTotal = first.PaymentAmount > 0 ? first.PaymentAmount : rows.Sum(row => row.PaymentAmount);
        var vatTotal = grossTotal - netTotal;

        document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(12, Unit.Millimetre);
            page.DefaultTextStyle(text => text.FontFamily("Courier New").FontSize(10));

            page.Content().Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text($"Data sprzedaży: {FormatDate(first.SaleDate)}").FontSize(10);
                    row.RelativeItem().AlignRight().Text($"Łęczyca {FormatDate(first.InvoiceIssueDate)}").FontSize(10);
                });

                column.Item().PaddingTop(4).AlignCenter().Text($"Faktura VAT nr {FormatInvoiceNumber(first.InvoiceNumber, first.InvoiceIssueDate)}").Bold().FontSize(16);

                column.Item().PaddingTop(22).Row(row =>
                {
                    row.RelativeItem().Column(seller =>
                    {
                        seller.Item().AlignCenter().Text("SPRZEDAWCA").Bold();
                        seller.Item().PaddingTop(7).Text(sellerName);
                        if (!string.IsNullOrWhiteSpace(first.SellerOwnerName))
                        {
                            seller.Item().Text(first.SellerOwnerName);
                        }

                        seller.Item().Text(sellerAddress);
                        seller.Item().Text("Zezwol.: FŁ-VII-4010-130/09");
                        seller.Item().PaddingTop(8).Text($"NIP: {DefaultIfEmpty(first.SellerTaxId, "-")}   Regon: {DefaultIfEmpty(first.SellerRegon, "-")}").Bold();
                        seller.Item().Text($"Bank: {DefaultIfEmpty(first.SellerBankName, "-")}").Bold();
                        seller.Item().Text($"Konto: {DefaultIfEmpty(first.SellerBankAccount, "-")}").Bold();
                    });

                    row.ConstantItem(18);

                    row.RelativeItem().Column(right =>
                    {
                        right.Item().AlignCenter().Text("NABYWCA").Bold();
                        right.Item().PaddingTop(7).Text(DefaultIfEmpty(first.PatientName, first.PayerName));
                        right.Item().Text(DefaultIfEmpty(first.PatientAddress, first.PayerAddress));
                        right.Item().Text(FormatPatientIdentifier(first));

                        right.Item().PaddingTop(16).AlignCenter().Text("PŁATNIK").Bold();
                        right.Item().PaddingTop(7).Text(DefaultIfEmpty(first.PayerName, first.PatientName)).Bold();
                        right.Item().Text(DefaultIfEmpty(first.PayerAddress, first.PatientAddress));
                        right.Item().Text(FormatIdentifier(first));
                    });
                });

                column.Item().PaddingTop(8).PaddingLeft(22).Text($"Termin płatności: {FormatDate(first.InvoicePaymentDate)}").FontSize(11);

                column.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(22);
                        columns.RelativeColumn(3.1f);
                        columns.ConstantColumn(25);
                        columns.ConstantColumn(32);
                        columns.ConstantColumn(50);
                        columns.ConstantColumn(48);
                        columns.ConstantColumn(32);
                        columns.ConstantColumn(38);
                        columns.ConstantColumn(42);
                        columns.ConstantColumn(42);
                    });

                    HeaderCell(table, "Lp.");
                    HeaderNameCell(table);
                    HeaderCell(table, "J.m");
                    HeaderCell(table, "Ilość");
                    HeaderCell(table, "Cena brutto");
                    HeaderCell(table, "Wartość brutto");
                    HeaderCell(table, "VAT %");
                    HeaderCell(table, "Kwota VAT");
                    HeaderCell(table, "Wartość netto");
                    HeaderCell(table, "Do zapłaty");

                    BodyCellCenter(table, "1");
                    BodyCell(table, first.InvoiceItemName);
                    BodyCellCenter(table, "op");
                    BodyCellRight(table, "1.00");
                    BodyCellRight(table, FormatMoney(grossTotal));
                    BodyCellRight(table, FormatMoney(grossTotal));
                    BodyCellCenter(table, $"{VatRate:N0}");
                    BodyCellRight(table, FormatMoney(vatTotal));
                    BodyCellRight(table, FormatMoney(netTotal));
                    BodyCellRight(table, FormatMoney(paymentTotal));

                    SummaryBlank(table, 4);
                    SummaryLabelCell(table, "Razem");
                    SummaryValueCell(table, FormatMoney(grossTotal));
                    SummaryNoBorder(table);
                    SummaryNoBorder(table);
                    SummaryNoBorder(table);
                    SummaryValueCell(table, FormatMoney(paymentTotal));

                    SummaryBlank(table, 4);
                    SummaryLabelCell(table, "w tym");
                    SummaryValueCell(table, "0.00");
                    SummaryValueCell(table, "23");
                    SummaryValueCell(table, "0.00");
                    SummaryValueCell(table, "0.00");
                    SummaryNoBorder(table);

                    SummaryBlank(table, 4);
                    SummaryNoBorder(table);
                    SummaryValueCell(table, FormatMoney(grossTotal));
                    SummaryValueCell(table, $"{VatRate:N0}");
                    SummaryValueCell(table, FormatMoney(vatTotal));
                    SummaryValueCell(table, FormatMoney(netTotal));
                    SummaryNoBorder(table);

                    SummaryBlank(table, 6);
                    SummaryLabelCell(table, "Razem");
                    SummaryValueCell(table, FormatMoney(vatTotal));
                    SummaryValueCell(table, FormatMoney(netTotal));
                    SummaryNoBorder(table);
                });

                column.Item().PaddingTop(14).Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Text($"Do zapłaty: {paymentTotal:N2} zł").Bold().FontSize(12);
                        left.Item().Text($"Słownie: {AmountToWords(paymentTotal)}").FontSize(10);
                    });
                });

                column.Item().PaddingTop(14).AlignCenter().Text($"Pozostało do zapłaty: {paymentTotal:N2} zł").FontSize(12);
                column.Item().AlignCenter().Text("Forma zapłaty: Przelew").FontSize(11);
                column.Item().AlignCenter().Text($"Termin płatności: {FormatDate(first.InvoicePaymentDate)}").FontSize(11);

                column.Item().PaddingTop(48).Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().AlignCenter().Text(DefaultIfEmpty(first.PatientName, first.PayerName)).FontSize(10);
                        left.Item().PaddingTop(6).AlignCenter().Text("........................................").FontSize(10);
                        left.Item().AlignCenter().Text("Podpis osoby uprawnionej do otrzymywania faktur").FontSize(8);
                    });

                    row.RelativeItem().Column(right =>
                    {
                        right.Item().AlignCenter().Text(" ").FontSize(10);
                        right.Item().PaddingTop(6).AlignCenter().Text("........................................").FontSize(10);
                        right.Item().AlignCenter().Text("Podpis osoby uprawnionej do wystawiania faktur").FontSize(8);
                    });
                });

                column.Item().PaddingTop(52).Text("wzór - KAMSOFT S.A. - System KS-AOW 2026").FontSize(8);
            });
        });
    }

    private static string FormatInvoiceNumber(string invoiceNumber, DateTime issueDate)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber) || invoiceNumber.Equals("brak", StringComparison.OrdinalIgnoreCase))
        {
            return "brak";
        }

        return invoiceNumber.Contains('/', StringComparison.Ordinal)
            ? invoiceNumber
            : $"{invoiceNumber}/{issueDate:yyyy}";
    }

    private static string FormatDate(DateTime date)
    {
        return date == default ? "" : date.ToString("yyyy.MM.dd");
    }

    private static string FormatMoney(decimal value)
    {
        return value.ToString("N2").Replace(" ", "\u00A0");
    }

    private static string FormatPatientIdentifier(SettlementReportRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.PatientTaxId))
        {
            return $"NIP: {row.PatientTaxId}";
        }

        if (!string.IsNullOrWhiteSpace(row.PatientPersonalId))
        {
            return $"PESEL: {row.PatientPersonalId}";
        }

        return "";
    }

    private static string FormatIdentifier(SettlementReportRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.PayerTaxId))
        {
            return $"NIP: {row.PayerTaxId}";
        }

        if (!string.IsNullOrWhiteSpace(row.PayerPersonalId))
        {
            return $"PESEL: {row.PayerPersonalId}";
        }

        return "NIP: ";
    }

    private static string DefaultIfEmpty(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string AmountToWords(decimal value)
    {
        var rounded = decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        var zloty = (int)Math.Floor(rounded);
        var grosze = (int)((rounded - zloty) * 100);
        return $"{NumberToWords(zloty)} złotych {NumberToWords(grosze)} groszy";
    }

    private static string NumberToWords(int value)
    {
        string[] ones = ["zero", "jeden", "dwa", "trzy", "cztery", "pięć", "sześć", "siedem", "osiem", "dziewięć"];
        string[] teens = ["dziesięć", "jedenaście", "dwanaście", "trzynaście", "czternaście", "piętnaście", "szesnaście", "siedemnaście", "osiemnaście", "dziewiętnaście"];
        string[] tens = ["", "", "dwadzieścia", "trzydzieści", "czterdzieści", "pięćdziesiąt", "sześćdziesiąt", "siedemdziesiąt", "osiemdziesiąt", "dziewięćdziesiąt"];
        string[] hundreds = ["", "sto", "dwieście", "trzysta", "czterysta", "pięćset", "sześćset", "siedemset", "osiemset", "dziewięćset"];

        if (value < 10)
        {
            return ones[value];
        }

        if (value < 20)
        {
            return teens[value - 10];
        }

        if (value < 100)
        {
            var rest = value % 10;
            return rest == 0 ? tens[value / 10] : $"{tens[value / 10]} {ones[rest]}";
        }

        if (value < 1000)
        {
            var rest = value % 100;
            return rest == 0 ? hundreds[value / 100] : $"{hundreds[value / 100]} {NumberToWords(rest)}";
        }

        return value.ToString();
    }

    private static void HeaderCell(TableDescriptor table, string value)
    {
        table.Cell().Border(0.5f).MinHeight(12, Unit.Millimetre).Padding(2).AlignCenter().AlignMiddle().Text(value).Bold().FontSize(9);
    }

    private static void HeaderNameCell(TableDescriptor table)
    {
        table.Cell().Border(0.5f).MinHeight(12, Unit.Millimetre).Padding(2).Column(column =>
        {
            column.Item().AlignCenter().Text("Nazwa towaru").Bold().FontSize(9);
            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text("Data ważności").FontSize(9);
                row.RelativeItem().AlignCenter().Text("Seria").FontSize(9);
            });
        });
    }

    private static void BodyCell(TableDescriptor table, string value)
    {
        table.Cell().Border(0.5f).MinHeight(10, Unit.Millimetre).Padding(3).Text(value).FontSize(9);
    }

    private static void BodyCellCenter(TableDescriptor table, string value)
    {
        table.Cell().Border(0.5f).MinHeight(10, Unit.Millimetre).Padding(3).AlignCenter().Text(value).FontSize(9);
    }

    private static void BodyCellRight(TableDescriptor table, string value)
    {
        table.Cell().Border(0.5f).MinHeight(10, Unit.Millimetre).Padding(3).AlignRight().Text(value).FontSize(9);
    }

    private static void SummaryLabelCell(TableDescriptor table, string value)
    {
        table.Cell()
            .BorderRight(0.5f)
            .Padding(3)
            .AlignCenter()
            .Text(value)
            .Bold()
            .FontSize(9);
    }

    private static void SummaryBlank(TableDescriptor table, uint columnSpan)
    {
        table.Cell().ColumnSpan(columnSpan).Padding(0).Text("");
    }

    private static void SummaryNoBorder(TableDescriptor table)
    {
        table.Cell().Padding(0).Text("");
    }

    private static void SummaryValueCell(TableDescriptor table, string value)
    {
        table.Cell().Border(0.5f).Padding(3).AlignRight().Text(value).Bold().FontSize(8.8f);
    }
}
