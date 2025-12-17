using System.Windows;
using System.Windows.Controls;
using KimchiHedge.Client.ViewModels;

namespace KimchiHedge.Client.Views;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is HistoryViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }
}
