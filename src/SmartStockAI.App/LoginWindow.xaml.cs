using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.Extensions.Configuration;
using SmartStockAI.App.Services;
using SmartStockAI.Core.Contracts.Auth;
using SmartStockAI.Core.Contracts.Users;
using SmartStockAI.Core.Enums;

namespace SmartStockAI.App;

public partial class LoginWindow : Window, INotifyPropertyChanged
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;
    private readonly AppSessionService _appSession;
    private readonly IConfiguration _configuration;
    private string _errorMessage = string.Empty;

    public LoginWindow(IAuthService authService, IUserService userService, AppSessionService appSession, IConfiguration configuration)
    {
        _authService = authService;
        _userService = userService;
        _appSession = appSession;
        _configuration = configuration;

        InitializeComponent();
        DataContext = this;
        Loaded += OnLoaded;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage == value)
            {
                return;
            }

            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        LoginTextBox.Text = "operator";
        PasswordTextBox.Password = "Operator123!";
        LoginTextBox.Focus();
        LoginTextBox.SelectAll();
    }

    private async void SignInButton_OnClick(object sender, RoutedEventArgs e)
    {
        ErrorMessage = string.Empty;

        var login = LoginTextBox.Text.Trim();
        var password = PasswordTextBox.Password;

        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = "Введите логин и пароль.";
            return;
        }

        try
        {
            var user = await _userService.GetByLoginAsync(login);
            if (user?.Role == UserRole.Admin && !IsAdminLoginEnabled())
            {
                ErrorMessage = "Вход администратора отключён. Включите DeveloperAccess:EnableAdminLogin в appsettings.local.json.";
                return;
            }

            var authResult = await _authService.LoginAsync(new LoginRequest
            {
                Login = login,
                Password = password
            });

            if (!authResult.IsAuthenticated || authResult.User is null)
            {
                ErrorMessage = TranslateError(authResult.Error) ?? "Не удалось выполнить вход.";
                return;
            }

            if (authResult.User.Role == UserRole.Admin && !IsAdminLoginEnabled())
            {
                await _authService.LogoutAsync();
                ErrorMessage = "Вход администратора отключён. Включите DeveloperAccess:EnableAdminLogin в appsettings.local.json.";
                return;
            }

            _appSession.SetCurrentUser(authResult.User);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = TranslateError(ex.Message) ?? "Произошла ошибка при входе.";
        }
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private bool IsAdminLoginEnabled()
    {
        return _configuration.GetValue<bool>("DeveloperAccess:EnableAdminLogin");
    }

    private static string? TranslateError(string? error) => error switch
    {
        null => null,
        "Login and password are required." => "Введите логин и пароль.",
        "Invalid login or password." => "Неверный логин или пароль.",
        "Sign-in failed." => "Не удалось выполнить вход.",
        _ => error
    };

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
