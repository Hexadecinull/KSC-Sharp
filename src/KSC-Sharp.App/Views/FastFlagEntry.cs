using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KSCSharp.App.Views;

public class FastFlagEntry : INotifyPropertyChanged
{
    private string _key;
    private string _value;

    public FastFlagEntry(string key, string value)
    {
        _key = key;
        _value = value;
    }

    public string Key
    {
        get => _key;
        set { _key = value; OnChanged(); OnChanged(nameof(Display)); }
    }

    public string Value
    {
        get => _value;
        set { _value = value; OnChanged(); OnChanged(nameof(Display)); }
    }

    public string Display => $"{Key} = {Value}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
