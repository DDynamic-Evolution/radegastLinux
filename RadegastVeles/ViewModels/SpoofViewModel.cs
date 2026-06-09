using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Radegast;

namespace Radegast.Veles.ViewModels;

public partial class SpoofViewModel : ObservableObject
{
    [ObservableProperty]
    private string _seed = string.Empty;

    [ObservableProperty]
    private string _id0 = string.Empty;

    [ObservableProperty]
    private string _mac = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    public SpoofViewModel()
    {
        RefreshDisplay();
    }

    [RelayCommand]
    private void RerollSeed()
    {
        HWSpoof.RerollSeed();
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        Seed = HWSpoof.GetSeed();
        Id0 = HWSpoof.GetId0();
        Mac = HWSpoof.GetMac();
        Username = HWSpoof.GetUsername();
    }
}
