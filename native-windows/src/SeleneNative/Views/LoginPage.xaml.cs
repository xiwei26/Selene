using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SeleneNative.Core.ViewModels;

namespace SeleneNative.Views;

public sealed partial class LoginPage : UserControl
{
    public event Func<Task>? SessionChanged;

    public LoginPage()
    {
        InitializeComponent();
    }

    public void Build(LoginViewModel viewModel)
    {
        ContentStack.Children.Clear();
        ContentStack.Children.Add(UiHelpers.PageHeader("登录", "连接你的 Selene 服务端账号。"));

        if (viewModel.Session is not null)
        {
            ContentStack.Children.Add(UiHelpers.InfoBar("已登录", $"{viewModel.Session.Username} @ {viewModel.Session.ServerUrl}", InfoBarSeverity.Informational));
        }

        if (!string.IsNullOrWhiteSpace(viewModel.ErrorMessage))
        {
            ContentStack.Children.Add(UiHelpers.InfoBar("错误", viewModel.ErrorMessage, InfoBarSeverity.Error));
        }

        var serverBox = new TextBox
        {
            Header = "服务端地址",
            PlaceholderText = "https://example.com",
            Text = viewModel.Session?.ServerUrl ?? string.Empty,
            Width = 460,
        };
        var usernameBox = new TextBox
        {
            Header = "用户名",
            PlaceholderText = "请输入用户名",
            Text = viewModel.Session?.Username ?? string.Empty,
            Width = 460,
        };
        var passwordBox = new PasswordBox
        {
            Header = "密码",
            PlaceholderText = "请输入密码",
            Width = 460,
        };
        var loginButton = new Button { Content = "登录", HorizontalAlignment = HorizontalAlignment.Left };
        loginButton.Click += async (_, _) =>
        {
            loginButton.IsEnabled = false;
            await viewModel.LoginAsync(serverBox.Text, usernameBox.Text, passwordBox.Password);
            loginButton.IsEnabled = true;
            if (viewModel.Session is not null && SessionChanged is not null)
            {
                await SessionChanged.Invoke();
            }

            Build(viewModel);
        };
        var logoutButton = new Button { Content = "退出登录", HorizontalAlignment = HorizontalAlignment.Left };
        logoutButton.Click += async (_, _) =>
        {
            await viewModel.LogoutAsync();
            if (SessionChanged is not null)
            {
                await SessionChanged.Invoke();
            }

            Build(viewModel);
        };

        ContentStack.Children.Add(new StackPanel
        {
            Spacing = 14,
            Children =
            {
                serverBox,
                usernameBox,
                passwordBox,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    Children = { loginButton, logoutButton },
                },
            },
        });
    }
}
