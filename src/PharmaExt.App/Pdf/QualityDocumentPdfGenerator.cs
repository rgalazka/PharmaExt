using PharmaExt.App.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO;
using System.Text.RegularExpressions;

namespace PharmaExt.App.Pdf;

public sealed class QualityDocumentPdfGenerator
{
    public void Generate(QualityDocument document, string filePath)
    {
        try
        {
            GenerateWithWord(document, filePath);
        }
        catch
        {
            GenerateFallback(document, filePath);
        }
    }

    private static void GenerateWithWord(QualityDocument qualityDocument, string filePath)
    {
        var wordType = Type.GetTypeFromProgID("Word.Application")
            ?? throw new InvalidOperationException("Microsoft Word nie jest zainstalowany.");

        dynamic? wordApplication = null;
        dynamic? document = null;
        try
        {
            wordApplication = Activator.CreateInstance(wordType)
                ?? throw new InvalidOperationException("Nie udalo sie uruchomic Microsoft Word.");
            wordApplication.Visible = false;
            document = wordApplication.Documents.Add();

            dynamic pageSetup = document.PageSetup;
            pageSetup.TopMargin = wordApplication.CentimetersToPoints(1.8);
            pageSetup.BottomMargin = wordApplication.CentimetersToPoints(1.8);
            pageSetup.LeftMargin = wordApplication.CentimetersToPoints(1.8);
            pageSetup.RightMargin = wordApplication.CentimetersToPoints(1.8);

            AddCoverPage(document, wordApplication, qualityDocument);

            dynamic range = document.Range(document.Content.End - 1, document.Content.End - 1);
            range.InsertBreak(7);
            range = document.Range(document.Content.End - 1, document.Content.End - 1);
            range.InsertFile(Path.GetFullPath(qualityDocument.FilePath));

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
            document.SaveAs2(Path.GetFullPath(filePath), 17);
        }
        finally
        {
            if (document is not null)
            {
                document.Close(false);
            }

            if (wordApplication is not null)
            {
                wordApplication.Quit(false);
            }
        }
    }

    private static void AddCoverPage(dynamic document, dynamic wordApplication, QualityDocument qualityDocument)
    {
        dynamic range = document.Range(0, 0);
        dynamic title = range.Paragraphs.Add();
        title.Range.Text = "DOKUMENTACJA SYSTEMU JAKOSCI";
        title.Range.Font.Name = "Arial";
        title.Range.Font.Size = 16;
        title.Range.Font.Bold = true;
        title.Range.ParagraphFormat.Alignment = 1;
        title.Range.InsertParagraphAfter();

        dynamic subtitle = document.Paragraphs.Add();
        subtitle.Range.Text = qualityDocument.Title;
        subtitle.Range.Font.Name = "Arial";
        subtitle.Range.Font.Size = 14;
        subtitle.Range.Font.Bold = true;
        subtitle.Range.ParagraphFormat.Alignment = 1;
        subtitle.Range.InsertParagraphAfter();
        subtitle.Range.InsertParagraphAfter();

        dynamic tableRange = document.Range(document.Content.End - 1, document.Content.End - 1);
        dynamic table = document.Tables.Add(tableRange, 7, 4);
        table.Borders.Enable = true;
        table.Range.Font.Name = "Arial";
        table.Range.Font.Size = 10;
        table.Range.ParagraphFormat.Alignment = 0;

        SetTableCell(table, 1, 1, "Kod dokumentu", true);
        SetTableCell(table, 1, 2, qualityDocument.Code, false);
        SetTableCell(table, 1, 3, "Wersja", true);
        SetTableCell(table, 1, 4, qualityDocument.Version, false);
        SetTableCell(table, 2, 1, "Tytul", true);
        SetTableCell(table, 2, 2, qualityDocument.Title, false);
        table.Cell(2, 2).Merge(table.Cell(2, 4));
        SetTableCell(table, 3, 1, "Kategoria", true);
        SetTableCell(table, 3, 2, qualityDocument.CategoryName, false);
        table.Cell(3, 2).Merge(table.Cell(3, 4));
        SetTableCell(table, 4, 1, "Status", true);
        SetTableCell(table, 4, 2, qualityDocument.Status, false);
        SetTableCell(table, 4, 3, "Data wydania", true);
        SetTableCell(table, 4, 4, qualityDocument.IssueDate.ToString("dd.MM.yyyy"), false);
        SetTableCell(table, 5, 1, "Data obowiazywania", true);
        SetTableCell(table, 5, 2, qualityDocument.EffectiveDate.ToString("dd.MM.yyyy"), false);
        SetTableCell(table, 5, 3, "Aktualizacja", true);
        SetTableCell(table, 5, 4, qualityDocument.UpdatedAt.ToString("dd.MM.yyyy"), false);
        SetTableCell(table, 6, 1, "Dokument", true);
        SetTableCell(table, 6, 2, $"{qualityDocument.Code} {qualityDocument.Title}", false);
        table.Cell(6, 2).Merge(table.Cell(6, 4));
        SetTableCell(table, 7, 1, "Uwagi", true);
        SetTableCell(table, 7, 2, "", false);
        table.Cell(7, 2).Merge(table.Cell(7, 4));

        table.Rows.Alignment = 1;
        table.Range.InsertParagraphAfter();

        dynamic footer = document.Paragraphs.Add();
        footer.Range.Text = "Tresci instrukcji rozpoczynaja sie od nastepnej strony.";
        footer.Range.Font.Name = "Arial";
        footer.Range.Font.Size = 9;
        footer.Range.Font.Italic = true;
        footer.Range.ParagraphFormat.Alignment = 1;
    }

    private static void SetTableCell(dynamic table, int row, int column, string text, bool bold)
    {
        dynamic cell = table.Cell(row, column);
        cell.Range.Text = text;
        cell.Range.Font.Bold = bold;
        cell.Range.ParagraphFormat.SpaceAfter = 0;
        cell.Range.ParagraphFormat.Alignment = 0;
    }

    private static void GenerateFallback(QualityDocument document, string filePath)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(18, Unit.Millimetre);
                page.DefaultTextStyle(text => text.FontFamily("Arial").FontSize(10));
                page.Content().Column(column =>
                {
                    AddFallbackCover(column, document);
                });
            });

            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(18, Unit.Millimetre);
                page.DefaultTextStyle(text => text.FontFamily("Arial").FontSize(10));
                page.Content().Column(column =>
                {
                    AddFallbackTextContent(column, document);
                });
            });
        }).GeneratePdf(filePath);
    }

    private static void AddFallbackCover(ColumnDescriptor column, QualityDocument document)
    {
        column.Item().Text("Dokumentacja Systemu Jakosci").Bold().FontSize(16).AlignCenter();
        column.Item().PaddingTop(3).Text($"{document.Code} {document.Title}").Bold().FontSize(14).AlignCenter();
        column.Item().PaddingTop(12).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(38, Unit.Millimetre);
                columns.RelativeColumn();
                columns.ConstantColumn(35, Unit.Millimetre);
                columns.RelativeColumn();
            });

            MetadataCell(table, "Kod dokumentu");
            MetadataCell(table, document.Code);
            MetadataCell(table, "Wersja");
            MetadataCell(table, document.Version);
            MetadataCell(table, "Tytul");
            MetadataCell(table, document.Title);
            MetadataCell(table, "Status");
            MetadataCell(table, document.Status);
            MetadataCell(table, "Kategoria");
            MetadataCell(table, document.CategoryName);
            MetadataCell(table, "Data wydania");
            MetadataCell(table, document.IssueDate.ToString("dd.MM.yyyy"));
            MetadataCell(table, "Data obowiazywania");
            MetadataCell(table, document.EffectiveDate.ToString("dd.MM.yyyy"));
            MetadataCell(table, "Aktualizacja");
            MetadataCell(table, document.UpdatedAt.ToString("dd.MM.yyyy"));
        });
    }

    private static void AddFallbackTextContent(ColumnDescriptor column, QualityDocument document)
    {
        var paragraphs = SplitParagraphs(document.ContentText);
        if (paragraphs.Count == 0)
        {
            column.Item().PaddingTop(10).Text("Brak tresci dokumentu zapisanej w bazie.").Italic();
            return;
        }

        foreach (var paragraph in paragraphs)
        {
            var (text, isBold) = ParseParagraph(paragraph);
            var textDescriptor = column.Item()
                .PaddingTop(isBold ? 7 : 5)
                .Text(text)
                .LineHeight(1.2f);

            if (isBold)
            {
                textDescriptor.Bold();
            }
        }
    }

    private static void MetadataCell(TableDescriptor table, string value)
    {
        table.Cell().Border(0.5f).Padding(3).Text(value);
    }

    private static IReadOnlyList<string> SplitParagraphs(string content)
    {
        return content
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }

    private static (string Text, bool IsBold) ParseParagraph(string value)
    {
        var text = value.Trim();
        var isMarkedBold = text.StartsWith("**", StringComparison.Ordinal)
            && text.EndsWith("**", StringComparison.Ordinal)
            && text.Length > 4;

        if (isMarkedBold)
        {
            text = text[2..^2];
        }

        return (text, isMarkedBold || IsQualityHeading(text));
    }

    private static bool IsQualityHeading(string text)
    {
        return Regex.IsMatch(text, @"^\d+\.\s+\S")
            || Regex.IsMatch(text, @"^[A-Z0-9\s\-]{12,}$");
    }
}
