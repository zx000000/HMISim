using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HMISim.Controls.ViewModels;

/// <summary>Controls 内部 ViewModel 基类（属性变更通知）。</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>设置字段并在变化时通知。</summary>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    /// <summary>手动发出属性变更通知。</summary>
    protected void RaisePropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
