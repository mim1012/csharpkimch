using CommunityToolkit.Mvvm.ComponentModel;

namespace KimchiHedge.Client.ViewModels;

/// <summary>
/// ViewModel 기본 클래스
/// </summary>
public abstract class ViewModelBase : ObservableObject, IDisposable
{
    private bool _isBusy;
    private bool _disposed;

    /// <summary>
    /// 작업 중 여부
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    /// <summary>
    /// 리소스 정리 (이벤트 해제 등)
    /// </summary>
    protected virtual void OnDispose() { }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        OnDispose();
        GC.SuppressFinalize(this);
    }
}
