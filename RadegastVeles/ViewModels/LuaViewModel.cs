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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Radegast.Veles.Core;
using Radegast.Veles.Scripting;

namespace Radegast.Veles.ViewModels;

public partial class LuaViewModel : ObservableObject
{
    private readonly RadegastInstanceAvalonia _instance;
    private readonly LuaPluginManager _manager;

    public ObservableCollection<LuaScriptInfo> Scripts { get; } = new();

    [ObservableProperty]
    private LuaScriptInfo? _selectedScript;

    [ObservableProperty]
    private string _logText = string.Empty;

    [ObservableProperty]
    private string _scriptsDirectory = string.Empty;

    public LuaViewModel(RadegastInstanceAvalonia instance)
    {
        _instance = instance;
        _manager = instance.LuaPlugins;
        _scriptsDirectory = GetScriptsDirectory();
        LuaApi.OnLog += OnScriptLog;
        RefreshList();
    }

    private static string GetScriptsDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RadegastVeles", "scripts");
    }

    private void OnScriptLog(string message)
    {
        LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
    }

    public void RefreshList()
    {
        Scripts.Clear();
        foreach (var plugin in _manager.Plugins)
            Scripts.Add(new LuaScriptInfo(plugin.Name, plugin.FilePath, plugin.IsRunning));
    }

    [RelayCommand]
    private void ReloadAll()
    {
        _manager.DiscoverAndLoad();
        RefreshList();
        LogText = $"[{DateTime.Now:HH:mm:ss}] Reloaded all scripts.\n";
    }

    [RelayCommand]
    private void ReloadScript()
    {
        if (SelectedScript == null) return;
        _manager.Reload(SelectedScript.Name);
        RefreshList();
        LogText = $"[{DateTime.Now:HH:mm:ss}] Reloaded script: {SelectedScript.Name}\n";
    }

    [RelayCommand]
    private void OpenScriptsFolder()
    {
        var dir = GetScriptsDirectory();
        Directory.CreateDirectory(dir);
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = dir,
            UseShellExecute = true,
            Verb = "open"
        };
        System.Diagnostics.Process.Start(psi);
    }
}

public class LuaScriptInfo
{
    public string Name { get; }
    public string FilePath { get; }
    public bool IsRunning { get; }

    public LuaScriptInfo(string name, string filePath, bool isRunning)
    {
        Name = name;
        FilePath = filePath;
        IsRunning = isRunning;
    }
}
