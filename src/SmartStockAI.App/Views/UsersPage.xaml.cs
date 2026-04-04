using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using SmartStockAI.App.Models;
using SmartStockAI.App.Services;
using SmartStockAI.Core.Contracts.Users;
using SmartStockAI.Core.Enums;

namespace SmartStockAI.App.Views;

public partial class UsersPage : Page, INotifyPropertyChanged
{
    private readonly IUserService _userService;
    private readonly AppSessionService _appSession;
    private UserDto? _selectedUser;
    private UserDto? _sessionSelectedUser;
    private LookupItem? _selectedRoleOption;
    private string _editorLogin = string.Empty;
    private string _editorDisplayName = string.Empty;
    private string _editorPassword = string.Empty;
    private bool _editorIsActive = true;
    private int? _editingUserId;

    public UsersPage(IUserService userService, AppSessionService appSession)
    {
        _userService = userService;
        _appSession = appSession;

        InitializeComponent();
        DataContext = this;

        Users = [];
        RoleOptions =
        [
            new LookupItem { Id = (int)UserRole.Admin, Name = "Администратор" },
            new LookupItem { Id = (int)UserRole.WarehouseOperator, Name = "Оператор склада" },
            new LookupItem { Id = (int)UserRole.Manager, Name = "Менеджер" }
        ];

        _appSession.CurrentUserChanged += AppSession_OnCurrentUserChanged;
        Loaded += OnLoaded;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<UserDto> Users { get; }

    public List<LookupItem> RoleOptions { get; }

    public UserDto? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (SetField(ref _selectedUser, value))
            {
                ApplySelectedUser(value);
            }
        }
    }

    public UserDto? SessionSelectedUser
    {
        get => _sessionSelectedUser;
        set
        {
            if (SetField(ref _sessionSelectedUser, value))
            {
                OnPropertyChanged(nameof(SessionRoleLabel));
            }
        }
    }

    public LookupItem? SelectedRoleOption
    {
        get => _selectedRoleOption;
        set
        {
            if (SetField(ref _selectedRoleOption, value))
            {
                OnPropertyChanged(nameof(SelectedRoleDescription));
            }
        }
    }

    public string EditorLogin
    {
        get => _editorLogin;
        set => SetField(ref _editorLogin, value);
    }

    public string EditorDisplayName
    {
        get => _editorDisplayName;
        set => SetField(ref _editorDisplayName, value);
    }

    public string EditorPassword
    {
        get => _editorPassword;
        set => SetField(ref _editorPassword, value);
    }

    public bool EditorIsActive
    {
        get => _editorIsActive;
        set => SetField(ref _editorIsActive, value);
    }

    public string CurrentUserDisplay => _appSession.CurrentUserDisplayName;

    public string SessionRoleLabel =>
        SessionSelectedUser is null ? "Роль не выбрана" : MapRole(SessionSelectedUser.Role);

    public string EditorTitle => _editingUserId.HasValue ? "Редактирование пользователя" : "Новый пользователь";

    public string PasswordCaption => _editingUserId.HasValue ? "Пароль (оставьте пустым, чтобы не менять)" : "Пароль";

    public string SelectedRoleDescription => SelectedRoleOption?.Id switch
    {
        (int)UserRole.Admin => "Полный административный доступ. Создание второго администратора запрещено правилами backend.",
        (int)UserRole.WarehouseOperator => "Операции склада, документы движения и инвентаризации.",
        (int)UserRole.Manager => "Роль для просмотра аналитики и дашбордов.",
        _ => "Выберите роль, чтобы увидеть её уровень доступа."
    };

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await ReloadAsync();
    }

    private async void RefreshButton_OnClick(object sender, RoutedEventArgs e)
    {
        await ReloadAsync();
    }

    private void ApplySessionButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SessionSelectedUser is null)
        {
            MessageBox.Show("Сначала выберите пользователя.", "Пользователи", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedUser = SessionSelectedUser;
        MessageBox.Show(
            "Аутентификация на backend в шаге 4 привязана к текущему вошедшему пользователю. Этот список сейчас только открывает пользователя в редакторе.",
            "Пользователи",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void NewUserButton_OnClick(object sender, RoutedEventArgs e)
    {
        ResetEditor();
        SelectedUser = null;
    }

    private async void SaveUserButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EditorLogin) || string.IsNullOrWhiteSpace(EditorDisplayName) || SelectedRoleOption?.Id is not int roleValue)
        {
            MessageBox.Show("Логин, отображаемое имя и роль обязательны.", "Пользователи", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var isEditing = _editingUserId.HasValue;
        if (!isEditing && string.IsNullOrWhiteSpace(EditorPassword))
        {
            MessageBox.Show("Для нового пользователя нужно указать пароль.", "Пользователи", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            UserDto? user;
            if (isEditing)
            {
                user = await _userService.UpdateAsync(_editingUserId!.Value, new UpdateUserRequest
                {
                    Login = EditorLogin.Trim(),
                    DisplayName = EditorDisplayName.Trim(),
                    Password = string.IsNullOrWhiteSpace(EditorPassword) ? null : EditorPassword,
                    Role = (UserRole)roleValue,
                    IsActive = EditorIsActive
                });
            }
            else
            {
                user = await _userService.CreateAsync(new CreateUserRequest
                {
                    Login = EditorLogin.Trim(),
                    DisplayName = EditorDisplayName.Trim(),
                    Password = EditorPassword,
                    Role = (UserRole)roleValue,
                    IsActive = EditorIsActive
                });
            }

            await ReloadAsync(user?.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Пользователи", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void DeleteUserButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedUser is null)
        {
            return;
        }

        if (MessageBox.Show(
                $"Удалить пользователя {SelectedUser.DisplayName}?",
                "Пользователи",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var deleted = await _userService.DeleteAsync(SelectedUser.Id);
            if (!deleted)
            {
                return;
            }

            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Пользователи", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ReloadAsync(int? selectedUserId = null)
    {
        var users = await _userService.GetAllAsync();

        Users.Clear();
        foreach (var user in users.OrderBy(x => x.DisplayName))
        {
            Users.Add(user);
        }

        SessionSelectedUser = _appSession.CurrentUser is null
            ? Users.FirstOrDefault()
            : Users.FirstOrDefault(x => x.Id == _appSession.CurrentUser.Id) ?? Users.FirstOrDefault();

        SelectedUser = selectedUserId.HasValue
            ? Users.FirstOrDefault(x => x.Id == selectedUserId.Value)
            : SessionSelectedUser ?? Users.FirstOrDefault();

        if (SelectedUser is null)
        {
            ResetEditor();
        }

        OnPropertyChanged(nameof(CurrentUserDisplay));
        OnPropertyChanged(nameof(SessionRoleLabel));
    }

    private void ApplySelectedUser(UserDto? user)
    {
        if (user is null)
        {
            ResetEditor();
            return;
        }

        _editingUserId = user.Id;
        EditorLogin = user.Login;
        EditorDisplayName = user.DisplayName;
        EditorPassword = string.Empty;
        EditorIsActive = user.IsActive;
        SelectedRoleOption = RoleOptions.FirstOrDefault(x => x.Id == (int)user.Role);
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(PasswordCaption));
    }

    private void ResetEditor()
    {
        _editingUserId = null;
        EditorLogin = string.Empty;
        EditorDisplayName = string.Empty;
        EditorPassword = string.Empty;
        EditorIsActive = true;
        SelectedRoleOption = RoleOptions.FirstOrDefault(x => x.Id == (int)UserRole.WarehouseOperator) ?? RoleOptions.FirstOrDefault();
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(PasswordCaption));
    }

    private void AppSession_OnCurrentUserChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CurrentUserDisplay));
        SessionSelectedUser = _appSession.CurrentUser;
        OnPropertyChanged(nameof(SessionRoleLabel));
    }

    private static string MapRole(UserRole role) => role switch
    {
        UserRole.Admin => "Администратор",
        UserRole.WarehouseOperator => "Оператор склада",
        UserRole.Manager => "Менеджер",
        _ => role.ToString()
    };

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
