using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PharmaExt.App.Models;

public sealed class ImportedFormIngredient : INotifyPropertyChanged
{
    private decimal? _usedQuantity;
    private bool _trackEdits;
    private Action? _markParentEdited;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Id { get; set; }
    public int ImportedFormId { get; set; }
    public int Lp { get; set; }
    public string Name { get; set; } = "";
    public decimal PrescribedQuantity { get; set; }
    public string Unit { get; set; } = "";
    public string BatchNumber { get; set; } = "";
    public DateTime? ExpiryDate { get; set; }
    public string ManufacturerSupplier { get; set; } = "";

    public decimal? UsedQuantity
    {
        get => _usedQuantity;
        set
        {
            if (_usedQuantity == value)
            {
                return;
            }

            _usedQuantity = value;
            OnPropertyChanged();
            if (_trackEdits)
            {
                _markParentEdited?.Invoke();
            }
        }
    }

    public void EnableEditTracking(Action markParentEdited)
    {
        _markParentEdited = markParentEdited;
        _trackEdits = true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
