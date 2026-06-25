namespace PharmaExt.App.Models;

public sealed class LabelMedicineFormOption
{
    public LabelMedicineFormOption(string latinName, string polishDescription)
    {
        LatinName = latinName;
        DisplayName = $"{latinName} - {polishDescription}";
    }

    public string LatinName { get; }
    public string DisplayName { get; }
}
