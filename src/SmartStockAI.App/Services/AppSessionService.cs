using System;
using SmartStockAI.Core.Contracts.Users;

namespace SmartStockAI.App.Services;

public sealed class AppSessionService
{
    public event EventHandler? CurrentUserChanged;

    public UserDto? CurrentUser { get; private set; }

    public string CurrentUserDisplayName =>
        CurrentUser is null
            ? "Пользователь не выбран"
            : $"{CurrentUser.DisplayName} ({CurrentUser.Login})";

    public void SetCurrentUser(UserDto? user)
    {
        CurrentUser = user;
        CurrentUserChanged?.Invoke(this, EventArgs.Empty);
    }
}
