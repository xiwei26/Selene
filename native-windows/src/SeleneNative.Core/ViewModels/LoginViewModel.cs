using CommunityToolkit.Mvvm.ComponentModel;
using SeleneNative.Core.Models;
using SeleneNative.Core.Services;

namespace SeleneNative.Core.ViewModels;

public sealed partial class LoginViewModel : ObservableObject
{
    private readonly ISessionStore _sessionStore;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private LoginSession? _session;

    public LoginViewModel(ISessionStore? sessionStore = null)
    {
        _sessionStore = sessionStore ?? new SessionStore();
    }

    public bool IsLoggedIn => Session?.IsLoggedIn == true;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Session = await _sessionStore.LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task LoginAsync(
        string serverUrl,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var client = new ServerApiClient(serverUrl);
            var session = await client.LoginAsync(username, password, cancellationToken).ConfigureAwait(false);
            await _sessionStore.SaveAsync(session, cancellationToken).ConfigureAwait(false);
            Session = session;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        await _sessionStore.ClearAsync(cancellationToken).ConfigureAwait(false);
        Session = null;
        ErrorMessage = null;
    }

    public IContentProvider? CreateProvider()
    {
        return Session is { IsLocalMode: false } session
            ? new ServerApiClient(session.ServerUrl, session.Cookie)
            : null;
    }

    partial void OnSessionChanged(LoginSession? value)
    {
        OnPropertyChanged(nameof(IsLoggedIn));
    }
}
