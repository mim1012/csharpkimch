using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KimchiHedge.Client.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace KimchiHedge.Client.Views;

public partial class RegisterWindow : Window
{
    private readonly RegisterViewModel _viewModel;

    public RegisterWindow()
    {
        InitializeComponent();

        // DI에서 ViewModel 가져오기
        _viewModel = App.Services.GetRequiredService<RegisterViewModel>();
        _viewModel.LoginRequested += OnLoginRequested;
        _viewModel.CloseRequested += OnCloseRequested;
        DataContext = _viewModel;

        EmailTextBox.Focus();

        // PasswordBox 바인딩 (PasswordBox는 바인딩이 안됨)
        PasswordBox.PasswordChanged += (s, e) => _viewModel.Password = PasswordBox.Password;
        PasswordConfirmBox.PasswordChanged += (s, e) => _viewModel.PasswordConfirm = PasswordConfirmBox.Password;

        // 엔터키로 다음 필드로 이동
        EmailTextBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter)
                PasswordBox.Focus();
        };

        PasswordBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter)
                PasswordConfirmBox.Focus();
        };

        PasswordConfirmBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter && _viewModel.RegisterCommand.CanExecute(null))
                _viewModel.RegisterCommand.Execute(null);
        };
    }

    private void OnLoginRequested(object? sender, EventArgs e)
    {
        var loginWindow = new LoginWindow();
        loginWindow.Show();
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.LoginRequested -= OnLoginRequested;
        _viewModel.CloseRequested -= OnCloseRequested;
        base.OnClosed(e);
    }
}
