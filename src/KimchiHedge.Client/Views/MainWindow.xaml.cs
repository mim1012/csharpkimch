using System.Windows;
using KimchiHedge.Client.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace KimchiHedge.Client.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // DI에서 ViewModel 가져오기
        var viewModel = App.Services.GetRequiredService<MainViewModel>();
        viewModel.OnLogout = OnLogout;
        DataContext = viewModel;
    }

    private void OnLogout()
    {
        var loginWindow = new LoginWindow();
        loginWindow.Show();
        this.Close();
    }
}
