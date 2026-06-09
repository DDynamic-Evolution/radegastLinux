/*
 * Radegast Metaverse Client
 * Copyright (c) 2026, Sjofn LLC
 * All rights reserved.
 *
 * Radegast is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using OpenMetaverse.StructuredData;
using Radegast.Veles.Core;
using Radegast.Veles.ViewModels;
using Radegast.Veles.Views;

namespace Radegast.Veles;

public class App : Application
{
    private readonly CredentialManager _credentialManager = new();
    private readonly AgentSessionManager _sessionManager = new();
    private readonly Dictionary<Guid, MainWindow> _agentWindows = new();
    private LoginWindow? _loginWindow;
    private DashboardWindow? _dashboardWindow;
    private TrayIcon? _trayIcon;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Prevent Avalonia compiled bindings from removing the CommunityToolkit
            // generated INotifyPropertyChanged properties.
            BindingPlugins.DataValidators.RemoveAt(0);

            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var icons = TrayIcon.GetIcons(this);
            if (icons?.Count > 0)
            {
                _trayIcon = icons[0];
                _trayIcon.Clicked += (_, _) => TrayClick();
                RebuildTrayMenu();
            }

            ShowLogin();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ShowLogin()
    {
        if (_loginWindow is { IsVisible: true })
        {
            _loginWindow.Activate();
            return;
        }

        _loginWindow = new LoginWindow(_credentialManager);
        _loginWindow.LoginSucceeded += OnLoginSucceeded;
        _loginWindow.Show();
    }

    private void ShowDashboard()
    {
        if (_dashboardWindow is { IsVisible: true })
        {
            _dashboardWindow.Activate();
            return;
        }

        var vm = new DashboardViewModel(_sessionManager);
        _dashboardWindow = new DashboardWindow(vm);
        _dashboardWindow.Closed += (_, _) => _dashboardWindow = null;
        _dashboardWindow.Show();
    }

    private void OnLoginSucceeded(object? sender, AgentLoginSucceededEventArgs e)
    {
        _loginWindow = null;

        var session = _sessionManager.AddSession(e.Instance);
        var mainWindow = new MainWindow(session);

        // Initialize map tile cache
        var settings = e.Instance.GlobalSettings;
        var mapCacheEnabled = settings["map_cache_enabled"].Type != OSDType.Unknown
            ? settings["map_cache_enabled"].AsBoolean() : true;
        var mapCacheMaxSizeMB = settings["map_cache_max_size_mb"].Type != OSDType.Unknown
            ? settings["map_cache_max_size_mb"].AsInteger() : 500;
        var mapCacheTtlDays = settings["map_cache_ttl_days"].Type != OSDType.Unknown
            ? settings["map_cache_ttl_days"].AsInteger() : 30;
        var mapCacheDir = Path.Combine(e.Instance.UserDir, "mapcache");
        MapTileCache.Initialize(mapCacheDir, mapCacheEnabled, mapCacheMaxSizeMB * 1024 * 1024, mapCacheTtlDays);

        var capturedSession = session;
        mainWindow.LogoutRequested += (_, _) => OnLogoutRequested(capturedSession);

        _agentWindows[session.Id] = mainWindow;
        mainWindow.Show();
        mainWindow.Activate();

        if (e.Instance.GlobalSettings["auto_check_updates"].AsBoolean())
            _ = AboutWindow.CheckForUpdatesAsync(mainWindow);

        // Subscribe to friend events for tray tooltip updates
        SubscribeToSessionFriends(session);
        UpdateTrayTooltip();

        RebuildTrayMenu();
    }

    private void OnLogoutRequested(AgentSession session)
    {
        if (_agentWindows.TryGetValue(session.Id, out var window))
        {
            window.ForceClose();
            _agentWindows.Remove(session.Id);
        }

        _sessionManager.RemoveSession(session);
        UpdateTrayTooltip();
        RebuildTrayMenu();

        if (_sessionManager.Sessions.Count == 0)
        {
            ShowLogin();
        }
    }

    private void TrayClick()
    {
        if (_agentWindows.Count > 0)
        {
            var lastWindow = _agentWindows.Values.Last();
            lastWindow.Show();
            lastWindow.Activate();
        }
        else
        {
            ShowLogin();
        }
    }

    private void RebuildTrayMenu()
    {
        if (_trayIcon == null) return;

        var menu = new NativeMenu();

        foreach (var session in _sessionManager.Sessions)
        {
            var agentName = session.AgentName;
            var capturedSession = session;

            var showItem = new NativeMenuItem($"Show {agentName}");
            showItem.Click += (_, _) =>
            {
                if (_agentWindows.TryGetValue(capturedSession.Id, out var w))
                {
                    w.Show();
                    w.Activate();
                }
            };
            menu.Items.Add(showItem);

            var logoutItem = new NativeMenuItem($"Logout {agentName}");
            logoutItem.Click += (_, _) => OnLogoutRequested(capturedSession);
            menu.Items.Add(logoutItem);

            menu.Items.Add(new NativeMenuItemSeparator());
        }

        var loginItem = new NativeMenuItem("New Login");
        loginItem.Click += (_, _) => ShowLogin();
        menu.Items.Add(loginItem);

        var dashboardItem = new NativeMenuItem("Dashboard");
        dashboardItem.Click += (_, _) => ShowDashboard();
        menu.Items.Add(dashboardItem);

        menu.Items.Add(new NativeMenuItemSeparator());

        var updateItem = new NativeMenuItem("Check for Updates...");
        updateItem.Click += async (_, _) =>
        {
            var lifetime = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var parent = lifetime?.Windows.FirstOrDefault(w => w.IsVisible);
            if (parent != null)
                await AboutWindow.CheckForUpdatesAsync(parent);
        };
        menu.Items.Add(updateItem);

        menu.Items.Add(new NativeMenuItemSeparator());

        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) => ExitApplication();
        menu.Items.Add(exitItem);

        _trayIcon.Menu = menu;
    }

    private void SubscribeToSessionFriends(AgentSession session)
    {
        session.Instance.Client.Friends.FriendOnline += (_, _) => UpdateTrayTooltip();
        session.Instance.Client.Friends.FriendOffline += (_, _) => UpdateTrayTooltip();
        session.Instance.Client.Friends.FriendshipTerminated += (_, _) => UpdateTrayTooltip();
        session.Instance.Client.Friends.FriendNames += (_, _) => UpdateTrayTooltip();
    }

    private void UpdateTrayTooltip()
    {
        if (_trayIcon == null) return;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            int totalOnline = 0;
            int totalFriends = 0;
            int connectedSessions = 0;

            foreach (var session in _sessionManager.Sessions)
            {
                if (!session.Instance.Client.Network.Connected) continue;
                connectedSessions++;

                var friendList = session.Instance.Client.Friends.FriendList;
                totalFriends += friendList.Count;
                totalOnline += friendList.Values.Count(f => f.IsOnline);
            }

            if (connectedSessions == 0)
            {
                _trayIcon.ToolTipText = "Radegast Veles - Not connected";
            }
            else if (connectedSessions == 1)
            {
                _trayIcon.ToolTipText = $"Radegast Veles - {totalOnline}/{totalFriends} friends online";
            }
            else
            {
                _trayIcon.ToolTipText = $"Radegast Veles ({connectedSessions} sessions) - {totalOnline}/{totalFriends} friends online";
            }
        });
    }

    public void ExitApplication()
    {
        foreach (var window in _agentWindows.Values.ToArray())
        {
            window.ForceClose();
        }
        _agentWindows.Clear();

        _loginWindow?.Close();
        _sessionManager.Dispose();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
