using System.Windows.Media;

namespace PharmaExt.App.Models;

public sealed class AnalysisPrescriptionRow
{
    private static readonly Brush PurchaseVatHigherBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38));
    private static readonly Brush SaleVatHigherBrush = new SolidColorBrush(Color.FromRgb(22, 163, 74));
    private static readonly Brush NegativeProfitBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38));
    private static readonly Brush PositiveProfitBrush = new SolidColorBrush(Color.FromRgb(22, 163, 74));
    private static readonly Brush NeutralBrush = Brushes.Transparent;

    public int DisplayNumber { get; set; }
    public DateTime SaleDate { get; set; }
    public string SourcePrescriptionId { get; set; } = "";
    public string PrescriptionNumber { get; set; } = "";
    public string PatientName { get; set; } = "";
    public string PatientAddress { get; set; } = "";
    public decimal IngredientsNet { get; set; }
    public decimal IngredientsGross { get; set; }
    public decimal PurchaseNet { get; set; }
    public decimal PurchaseVat { get; set; }
    public decimal TaxaLaborumGross { get; set; }
    public decimal MarginGross { get; set; }
    public decimal SaleVatRate { get; set; }
    public decimal PatientPayment { get; set; }

    public decimal TaxaLaborumNet => DivideByVatFactor(TaxaLaborumGross, SaleVatRate);
    public decimal TaxaLaborumVat => TaxaLaborumGross - TaxaLaborumNet;
    public decimal MarginNet => DivideByVatFactor(MarginGross, SaleVatRate);
    public decimal MarginVat => MarginGross - MarginNet;
    public decimal TaxAndMarginNet => TaxaLaborumNet + MarginNet;
    public decimal TaxAndMarginVat => TaxaLaborumVat + MarginVat;
    public decimal TaxAndMarginGross => TaxaLaborumGross + MarginGross;
    public decimal IngredientsSalesVat => IngredientsGross - IngredientsNet;
    public decimal TotalNet => IngredientsNet + TaxAndMarginNet;
    public decimal TotalGross => IngredientsGross + TaxAndMarginGross;
    public decimal SalesVat => TotalGross - TotalNet;
    public decimal PurchaseGross => PurchaseNet + PurchaseVat;
    public decimal VatDifference => PurchaseVat - SalesVat;
    public decimal NetProfit => TaxAndMarginNet;
    public decimal ProfitMinusVat => NetProfit - VatDifference;
    public Brush ProfitMinusVatBrush => GetProfitBrush(ProfitMinusVat);
    public Brush VatDifferenceBrush => GetDifferenceBrush(VatDifference);

    public static Brush GetDifferenceBrush(decimal difference)
    {
        if (difference > 0.005m)
        {
            return PurchaseVatHigherBrush;
        }

        if (difference < -0.005m)
        {
            return SaleVatHigherBrush;
        }

        return NeutralBrush;
    }

    public static Brush GetProfitBrush(decimal value)
    {
        if (value < -0.005m)
        {
            return NegativeProfitBrush;
        }

        if (value > 0.005m)
        {
            return PositiveProfitBrush;
        }

        return Brushes.Black;
    }

    private static decimal DivideByVatFactor(decimal grossValue, decimal vatRate)
    {
        var factor = 1 + vatRate / 100;
        return factor == 0 ? grossValue : grossValue / factor;
    }
}
