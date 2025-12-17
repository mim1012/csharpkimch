using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using KimchiHedge.Client.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace KimchiHedge.Client.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();

        // DI에서 ViewModel 가져오기
        var viewModel = App.Services.GetRequiredService<LoginViewModel>();
        viewModel.OnLoginSuccess = OnLoginSuccess;
        DataContext = viewModel;

        EmailTextBox.Focus();

        // 엔터키로 로그인
        PasswordBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter && viewModel.LoginCommand.CanExecute(PasswordBox))
            {
                viewModel.LoginCommand.Execute(PasswordBox);
            }
        };

        EmailTextBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter)
            {
                PasswordBox.Focus();
            }
        };
    }

    private void OnLoginSuccess()
    {
        var mainWindow = new MainWindow();
        mainWindow.Show();
        this.Close();
    }

    private void OnRegisterLinkClick(object sender, RoutedEventArgs e)
    {
        var registerWindow = new RegisterWindow();
        registerWindow.Show();
        this.Close();
    }
}
