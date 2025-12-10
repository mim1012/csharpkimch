using CommunityToolkit.Mvvm.ComponentModel;

namespace KimchiHedge.Client.ViewModels;

/// <summary>
/// ViewModel 기본 클래스
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
    private bool _isBusy;

    /// <summary>
    /// 작업 중 여부
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }
}
