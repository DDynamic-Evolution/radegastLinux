using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Radegast.Veles.Core;
using Radegast.Veles.VPN;

namespace Radegast.Veles.ViewModels;

public partial class VpnViewModel : ObservableObject, IDisposable
{
    private readonly RadegastInstanceAvalonia _instance;
    private readonly VpnManager _manager;
    private DispatcherTimer? _pollTimer;

    public ObservableCollection<VpnProfile> Profiles { get; } = [];

    [ObservableProperty]
    private VpnProfile? _selectedProfile;

    [ObservableProperty]
    private bool _isManagerAvailable;

    [ObservableProperty]
    private string _statusText = "Not available";

    [ObservableProperty]
    private bool _isConnected;

    // Edit fields (in-place editing in the UI)
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editPrivateKey = string.Empty;
    [ObservableProperty] private string _editAddress = string.Empty;
    [ObservableProperty] private string _editPeerPublicKey = string.Empty;
    [ObservableProperty] private string _editPeerEndpoint = string.Empty;
    [ObservableProperty] private string _editPeerAllowedIPs = "0.0.0.0/0";
    [ObservableProperty] private int _editPeerKeepalive = 25;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveProfileCommand))]
    private bool _isEditing;

    public VpnViewModel(RadegastInstanceAvalonia instance, VpnManager manager)
    {
        _instance = instance;
        _manager = manager;
        IsManagerAvailable = manager.IsAvailable;

        if (manager.IsAvailable)
        {
            StatusText = "Ready";
            manager.Start();
            LoadProfiles();
            _pollTimer = new DispatcherTimer(TimeSpan.FromSeconds(10), DispatcherPriority.Normal, async (_, _) => await PollStatus());
            _pollTimer.Start();
        }
        else
        {
            StatusText = "vpn-helper not found. See Preferences → VPN for setup instructions.";
        }
    }

    partial void OnSelectedProfileChanged(VpnProfile? value)
    {
        if (value != null && IsEditing)
        {
            EditName = value.Name;
            EditPrivateKey = value.PrivateKey;
            EditAddress = value.Address;
            var peer = value.Peers.FirstOrDefault();
            if (peer != null)
            {
                EditPeerPublicKey = peer.PublicKey;
                EditPeerEndpoint = peer.Endpoint;
                EditPeerAllowedIPs = string.Join(", ", peer.AllowedIPs);
                EditPeerKeepalive = peer.PersistentKeepalive;
            }
        }
    }

    private void LoadProfiles()
    {
        Profiles.Clear();
        var saved = _instance.GlobalSettings["vpn_profiles"];
        if (saved.Type == OpenMetaverse.StructuredData.OSDType.Unknown) return;

        try
        {
            var json = saved.AsString();
            var list = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<VpnProfile>>(json);
            if (list != null)
                foreach (var p in list)
                    Profiles.Add(p);
        }
        catch { }
    }

    private void SaveProfiles()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(Profiles.ToList());
        _instance.GlobalSettings["vpn_profiles"] = OpenMetaverse.StructuredData.OSD.FromString(json);
    }

    [RelayCommand]
    private void AddProfile()
    {
        EditName = string.Empty;
        EditPrivateKey = string.Empty;
        EditAddress = string.Empty;
        EditPeerPublicKey = string.Empty;
        EditPeerEndpoint = string.Empty;
        EditPeerAllowedIPs = "0.0.0.0/0";
        EditPeerKeepalive = 25;
        SelectedProfile = null;
        IsEditing = true;
    }

    [RelayCommand]
    private void EditProfile()
    {
        if (SelectedProfile == null) return;
        EditName = SelectedProfile.Name;
        EditPrivateKey = SelectedProfile.PrivateKey;
        EditAddress = SelectedProfile.Address;
        var peer = SelectedProfile.Peers.FirstOrDefault();
        if (peer != null)
        {
            EditPeerPublicKey = peer.PublicKey;
            EditPeerEndpoint = peer.Endpoint;
            EditPeerAllowedIPs = string.Join(", ", peer.AllowedIPs);
            EditPeerKeepalive = peer.PersistentKeepalive;
        }
        IsEditing = true;
    }

    [RelayCommand(CanExecute = nameof(CanSaveProfile))]
    private void SaveProfile()
    {
        var peer = new VpnPeer
        {
            PublicKey = EditPeerPublicKey,
            Endpoint = EditPeerEndpoint,
            AllowedIPs = EditPeerAllowedIPs.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(),
            PersistentKeepalive = EditPeerKeepalive,
        };

        var profile = new VpnProfile
        {
            Name = EditName,
            PrivateKey = EditPrivateKey,
            Address = EditAddress,
            Peers = [peer],
        };

        var existing = Profiles.FirstOrDefault(p => p.Name == EditName);
        if (existing != null)
        {
            var idx = Profiles.IndexOf(existing);
            Profiles[idx] = profile;
        }
        else
        {
            Profiles.Add(profile);
        }

        SaveProfiles();
        IsEditing = false;
    }

    private bool CanSaveProfile() => IsEditing;

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
    }

    [RelayCommand]
    private void RemoveProfile()
    {
        if (SelectedProfile == null) return;
        Profiles.Remove(SelectedProfile);
        SelectedProfile = null;
        SaveProfiles();
    }

    [RelayCommand]
    private async Task ConnectProfile()
    {
        if (SelectedProfile == null) return;
        StatusText = $"Connecting {SelectedProfile.Name}...";

        var ok = await _manager.Connect(SelectedProfile.Name, SelectedProfile);
        if (ok)
        {
            IsConnected = true;
            StatusText = $"Connected: {SelectedProfile.Name}";
        }
        else
        {
            IsConnected = false;
            StatusText = $"Failed to connect {SelectedProfile.Name}";
        }
    }

    [RelayCommand]
    private async Task DisconnectProfile()
    {
        if (SelectedProfile == null) return;
        await _manager.Disconnect(SelectedProfile.Name);
        IsConnected = false;
        StatusText = $"Disconnected: {SelectedProfile.Name}";
    }

    private async Task PollStatus()
    {
        if (SelectedProfile == null) return;
        var state = await _manager.GetStatus(SelectedProfile.Name);
        IsConnected = state == VpnState.Connected;
    }

    public void Dispose()
    {
        _pollTimer?.Stop();
        _manager.Stop();
    }
}
