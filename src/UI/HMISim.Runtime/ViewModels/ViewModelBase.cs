using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace HMISim.Runtime.ViewModels;

/// <summary>ViewModel 基类：属性变更通知的样板收敛（阶段 0 零外部依赖，不引 MVVM 框架包）。</summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>设置字段并在值变化时发通知。返回是否发生了变化。</summary>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    /// <summary>手动发一个属性变更通知。</summary>
    protected void RaiseChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>最简命令实现。</summary>
public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    /// <inheritdoc />
    public void Execute(object? parameter) => execute();

    /// <summary>通知命令可用状态变化。</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
