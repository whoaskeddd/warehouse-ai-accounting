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
    private readonly AuditTrailService _auditTrail;
    private UserDto? _selectedUser;
    private UserDto? _sessionSelectedUser;
    private LookupItem? _selectedRoleOption;
    private string _editorLogin = string.Empty;
    private string _editorDisplayName = string.Empty;
    private int? _editingUserId;

    public UsersPage(IUserService userService, AppSessionService appSession, AuditTrailService auditTrail)
    {
        _userService = userService;
        _appSession = appSession;
        _auditTrail = auditTrail;

        InitializeComponent();
        DataContext = this;

        Users = [];
        RoleOptions =
        [
            new LookupItem { Id = (int)UserRole.Admin, Name = "Администратор" },
            new LookupItem { Id = (int)UserRole.WarehouseOperator, Name = "Кладовщик" },
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

    public string CurrentUserDisplay => _appSession.CurrentUserDisplayName;

    public string SessionRoleLabel =>
        SessionSelectedUser is null ? "Роль не выбрана" : MapRole(SessionSelectedUser.Role);

    public string EditorTitle => _editingUserId.HasValue ? "Редактирование" : "Новый пользователь";

    public string SelectedRoleDescription => SelectedRoleOption?.Id switch
    {
        (int)UserRole.Admin => "Полный доступ к настройкам, справочникам, документам и административным операциям.",
        (int)UserRole.WarehouseOperator => "Работа со складом: приход, расход, инвентаризация и проверка расхождений.",
        (int)UserRole.Manager => "Просмотр ключевых данных, отчетов и пользовательских сценариев без критичных изменений.",
        _ => "Выбери роль, чтобы увидеть краткое описание прав."
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
            MessageBox.Show("Выбери пользователя для входа.", "Пользователи", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _appSession.SetCurrentUser(SessionSelectedUser);
        _auditTrail.Add(
            SessionSelectedUser.DisplayName,
            "Смена активного пользователя",
            SessionSelectedUser.Login,
            $"Роль: {MapRole(SessionSelectedUser.Role)}");
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
            MessageBox.Show("Заполни логин, имя и роль пользователя.", "Пользователи", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var isEditing = _editingUserId.HasValue;

        try
        {
            UserDto? user;
            if (isEditing)
            {
                user = await _userService.UpdateAsync(_editingUserId!.Value, new UpdateUserRequest
                {
                    Login = EditorLogin.Trim(),
                    DisplayName = EditorDisplayName.Trim(),
                    Role = (UserRole)roleValue
                });
            }
            else
            {
                user = await _userService.CreateAsync(new CreateUserRequest
                {
                    Login = EditorLogin.Trim(),
                    DisplayName = EditorDisplayName.Trim(),
                    Role = (UserRole)roleValue
                });
            }

            await ReloadAsync(user?.Id);
            var actor = _appSession.CurrentUser?.DisplayName ?? "Локальный оператор";
            _auditTrail.Add(
                actor,
                isEditing ? "Изменение пользователя" : "Создание пользователя",
                EditorLogin.Trim(),
                $"Имя: {EditorDisplayName.Trim()}, роль: {MapRole((UserRole)roleValue)}");
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

            var actor = _appSession.CurrentUser?.DisplayName ?? "Локальный оператор";
            _auditTrail.Add(actor, "Удаление пользователя", SelectedUser.Login, $"Удален пользователь {SelectedUser.DisplayName}.", "Warning");

            if (_appSession.CurrentUser?.Id == SelectedUser.Id)
            {
                _appSession.SetCurrentUser(null);
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

        SessionSelectedUser = selectedUserId.HasValue
            ? Users.FirstOrDefault(x => x.Id == selectedUserId.Value)
            : _appSession.CurrentUser is null
                ? Users.FirstOrDefault()
                : Users.FirstOrDefault(x => x.Id == _appSession.CurrentUser.Id);

        if (_appSession.CurrentUser is null && SessionSelectedUser is not null)
        {
            _appSession.SetCurrentUser(SessionSelectedUser);
        }

        SelectedUser = selectedUserId.HasValue
            ? Users.FirstOrDefault(x => x.Id == selectedUserId.Value)
            : Users.FirstOrDefault();

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
        SelectedRoleOption = RoleOptions.FirstOrDefault(x => x.Id == (int)user.Role);
        OnPropertyChanged(nameof(EditorTitle));
    }

    private void ResetEditor()
    {
        _editingUserId = null;
        EditorLogin = string.Empty;
        EditorDisplayName = string.Empty;
        SelectedRoleOption = RoleOptions.FirstOrDefault();
        OnPropertyChanged(nameof(EditorTitle));
    }

    private void AppSession_OnCurrentUserChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CurrentUserDisplay));
    }

    private static string MapRole(UserRole role) => role switch
    {
        UserRole.Admin => "Администратор",
        UserRole.WarehouseOperator => "Кладовщик",
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
