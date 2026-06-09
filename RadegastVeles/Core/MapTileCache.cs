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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Radegast.Veles.Core;

public static class MapTileCache
{
    private static readonly ConcurrentDictionary<(uint, uint), Bitmap?> Cache = new();
    private static readonly object _lock = new();
    private static readonly Dictionary<string, List<Action>> _pendingCallbacks = new();
    
    private static string _cacheDirectory = string.Empty;
    private static bool _diskCacheEnabled = true;
    private static long _maxCacheSizeBytes = 500 * 1024 * 1024; // 500 MB default
    private static int _ttlDays = 30;

    public static void Initialize(string cacheDirectory, bool enabled, long maxSizeBytes, int ttlDays)
    {
        _cacheDirectory = cacheDirectory;
        _diskCacheEnabled = enabled;
        _maxCacheSizeBytes = maxSizeBytes;
        _ttlDays = ttlDays;

        if (_diskCacheEnabled && !string.IsNullOrEmpty(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
            CleanupOldTiles();
        }
    }

    public static Bitmap? GetTile(uint gridX, uint gridY)
    {
        var key = (gridX, gridY);
        
        // Check memory cache first
        if (Cache.TryGetValue(key, out var bitmap))
            return bitmap;

        // Check disk cache
        if (_diskCacheEnabled && !string.IsNullOrEmpty(_cacheDirectory))
        {
            var filePath = GetTilePath(gridX, gridY);
            if (File.Exists(filePath))
            {
                var fileInfo = new FileInfo(filePath);
                var age = DateTime.Now - fileInfo.LastWriteTime;
                
                if (age.TotalDays <= _ttlDays)
                {
                    try
                    {
                        using var stream = File.OpenRead(filePath);
                        var diskBitmap = new Bitmap(stream);
                        Cache[key] = diskBitmap;
                        return diskBitmap;
                    }
                    catch
                    {
                        // Corrupted file, delete it
                        try { File.Delete(filePath); } catch { }
                    }
                }
                else
                {
                    // Expired, delete it
                    try { File.Delete(filePath); } catch { }
                }
            }
        }

        return null;
    }

    public static void RequestTile(uint gridX, uint gridY, Action? onComplete = null)
    {
        var key      = (gridX, gridY);
        var queueKey = $"maptile:{gridX}:{gridY}";

        lock (_lock)
        {
            // If already cached in memory, fire the callback immediately and return.
            if (Cache.ContainsKey(key))
            {
                if (onComplete != null)
                    Dispatcher.UIThread.Post(onComplete);
                return;
            }

            // Check disk cache
            if (_diskCacheEnabled && !string.IsNullOrEmpty(_cacheDirectory))
            {
                var filePath = GetTilePath(gridX, gridY);
                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    var age = DateTime.Now - fileInfo.LastWriteTime;
                    
                    if (age.TotalDays <= _ttlDays)
                    {
                        try
                        {
                            using var stream = File.OpenRead(filePath);
                            var diskBitmap = new Bitmap(stream);
                            Cache[key] = diskBitmap;
                            
                            if (onComplete != null)
                                Dispatcher.UIThread.Post(onComplete);
                            return;
                        }
                        catch
                        {
                            try { File.Delete(filePath); } catch { }
                        }
                    }
                    else
                    {
                        try { File.Delete(filePath); } catch { }
                    }
                }
            }

            // Register this callback so it will fire when the download completes.
            if (onComplete != null)
            {
                if (!_pendingCallbacks.TryGetValue(queueKey, out var list))
                {
                    list = [];
                    _pendingCallbacks[queueKey] = list;
                }
                list.Add(onComplete);
            }

            // If a download is already in-flight, the callback is now registered; nothing else to do.
            if (TextureDownloadQueue.Instance.IsPending(queueKey)) return;

            var url = $"https://map.secondlife.com/map-1-{gridX}-{gridY}-objects.jpg";
            TextureDownloadQueue.Instance.EnqueueWithBytes(queueKey, url, (bitmap, rawBytes) =>
            {
                if (bitmap != null)
                {
                    Cache[key] = bitmap;
                    
                    // Save to disk cache
                    if (_diskCacheEnabled && !string.IsNullOrEmpty(_cacheDirectory) && rawBytes != null)
                    {
                        SaveTileToDisk(gridX, gridY, rawBytes);
                    }
                }

                List<Action>? toFire;
                lock (_lock)
                {
                    _pendingCallbacks.TryGetValue(queueKey, out toFire);
                    _pendingCallbacks.Remove(queueKey);
                }

                if (toFire != null)
                    foreach (var cb in toFire)
                        Dispatcher.UIThread.Post(cb);
            }, TexturePriority.Low);
        }
    }

    private static string GetTilePath(uint gridX, uint gridY)
    {
        return Path.Combine(_cacheDirectory, $"{gridX}_{gridY}.jpg");
    }

    private static void SaveTileToDisk(uint gridX, uint gridY, byte[] rawBytes)
    {
        try
        {
            var filePath = GetTilePath(gridX, gridY);
            File.WriteAllBytes(filePath, rawBytes);
            
            // Enforce cache size limit
            EnforceCacheSizeLimit();
        }
        catch
        {
            // Ignore disk cache errors
        }
    }

    private static void EnforceCacheSizeLimit()
    {
        try
        {
            var directory = new DirectoryInfo(_cacheDirectory);
            var files = directory.GetFiles("*.jpg")
                .OrderByDescending(f => f.LastWriteTime)
                .ToList();

            long totalSize = files.Sum(f => f.Length);

            // Remove oldest files until we're under the limit
            foreach (var file in files)
            {
                if (totalSize <= _maxCacheSizeBytes)
                    break;

                totalSize -= file.Length;
                file.Delete();
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private static void CleanupOldTiles()
    {
        try
        {
            var directory = new DirectoryInfo(_cacheDirectory);
            var cutoff = DateTime.Now.AddDays(-_ttlDays);

            foreach (var file in directory.GetFiles("*.jpg"))
            {
                if (file.LastWriteTime < cutoff)
                {
                    try { file.Delete(); } catch { }
                }
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    public static void ClearCache()
    {
        // Clear memory cache
        Cache.Clear();

        // Clear disk cache
        if (_diskCacheEnabled && !string.IsNullOrEmpty(_cacheDirectory))
        {
            try
            {
                var directory = new DirectoryInfo(_cacheDirectory);
                foreach (var file in directory.GetFiles("*.jpg"))
                {
                    try { file.Delete(); } catch { }
                }
            }
            catch
            {
                // Ignore errors
            }
        }
    }

    public static long GetCacheSizeBytes()
    {
        if (!_diskCacheEnabled || string.IsNullOrEmpty(_cacheDirectory))
            return 0;

        try
        {
            var directory = new DirectoryInfo(_cacheDirectory);
            return directory.GetFiles("*.jpg").Sum(f => f.Length);
        }
        catch
        {
            return 0;
        }
    }

    public static int GetCacheTileCount()
    {
        if (!_diskCacheEnabled || string.IsNullOrEmpty(_cacheDirectory))
            return 0;

        try
        {
            var directory = new DirectoryInfo(_cacheDirectory);
            return directory.GetFiles("*.jpg").Length;
        }
        catch
        {
            return 0;
        }
    }
}
