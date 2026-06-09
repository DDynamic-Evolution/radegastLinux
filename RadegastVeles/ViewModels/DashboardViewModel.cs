using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Radegast.Veles.Core;

namespace Radegast.Veles.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly AgentSessionManager _sessionManager;

    [ObservableProperty]
    private ObservableCollection<SessionInfo> _sessions = new();

    [ObservableProperty]
    private SessionInfo? _selectedSession;

    public DashboardViewModel(AgentSessionManager sessionManager)
    {
        _sessionManager = sessionManager;
        RefreshSessions();
    }

    public void RefreshSessions()
    {
        Sessions.Clear();
        foreach (var session in _sessionManager.Sessions)
        {
            var info = new SessionInfo
            {
                SessionId = session.Id,
                AgentName = session.AgentName,
                GridName = session.Instance.NetCom.LoginOptions.Grid?.Name ?? "Unknown",
                RegionName = session.Instance.Client.Network.CurrentSim?.Name ?? "Unknown",
                IsConnected = session.Instance.Client.Network.Connected,
                FriendsOnline = session.Instance.Client.Friends.FriendList.Values.Count(f => f.IsOnline),
                FriendsTotal = session.Instance.Client.Friends.FriendList.Count
            };
            Sessions.Add(info);
        }
    }
}

public partial class SessionInfo : ObservableObject
{
    public Guid SessionId { get; set; }

    [ObservableProperty]
    private string _agentName = string.Empty;

    [ObservableProperty]
    private string _gridName = string.Empty;

    [ObservableProperty]
    private string _regionName = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private int _friendsOnline;

    [ObservableProperty]
    private int _friendsTotal;

    public string StatusText => IsConnected ? "Connected" : "Disconnected";
    public string FriendsText => $"{FriendsOnline}/{FriendsTotal} online";
}
