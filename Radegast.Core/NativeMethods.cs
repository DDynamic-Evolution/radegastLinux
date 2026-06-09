/**
 * Radegast Metaverse Client
 * Copyright(c) 2021-2025, Sjofn, LLC
 * All rights reserved.
 *  
 * Radegast is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program.If not, see<https://www.gnu.org/licenses/>.
 */

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Radegast.Core
{
    public sealed class NativeMethods
    {
        #region Fields

        public const CallingConvention CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl;

        private static readonly WindowsLibraryLoader WindowsLibraryLoader = new WindowsLibraryLoader();

        /// <summary>
        /// Cached handle to the loaded FMOD library, so we only load it once.
        /// </summary>
        private static IntPtr _fmodHandle = IntPtr.Zero;

        #endregion

        #region Constructors

        static NativeMethods()
        {
#if NET
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                WindowsLibraryLoader.LoadLibraries(new[] { "fmod" });
            }
            else
            {
                // Attempt to pre-load libfmod.so directly so it's in memory
                TryPreloadFmod();

                // Also register a fallback resolver in case pre-loading didn't cover everything
                NativeLibrary.SetDllImportResolver(
                    typeof(NativeMethods).Assembly,
                    ResolveDll);
            }
#else
            WindowsLibraryLoader.LoadLibraries(new[] { "fmod" });
#endif
        }

        public static void Init()
        {
            // Triggers the static constructor
        }

        #endregion

#if NET
        /// <summary>
        /// Try to load libfmod.so directly from known paths so it's already
        /// resident before any P/Invoke call touches it.  On Linux with .NET 8
        /// the DllImportResolver should also cover this, but pre-loading is an
        /// extra safety net.
        /// </summary>
        private static void TryPreloadFmod()
        {
            string lib = FindFmodLibrary();
            if (lib == null) return;

            try
            {
                _fmodHandle = NativeLibrary.Load(lib);
                Console.Error.WriteLine($"[NativeMethods] Pre-loaded {lib}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[NativeMethods] Pre-load failed for {lib}: {ex.Message}");
            }
        }

        /// <summary>
        /// Locate the FMOD native library on disk.
        /// </summary>
        private static string? FindFmodLibrary()
        {
            string[] searchNames =
            {
                "libfmod.so.12.10",
                "libfmod.so.12",
                "libfmod.so",
                "libfmod.dylib",
                "fmod.so.12.10",
                "fmod.so.12",
                "fmod.so",
                "fmod.dylib",
            };

            string? assemblyDir = Path.GetDirectoryName(typeof(NativeMethods).Assembly.Location);
            string appBaseDir = AppContext.BaseDirectory;

            foreach (string baseDir in new[] { assemblyDir, appBaseDir })
            {
                if (baseDir == null) continue;

                // Check assemblies/ subdirectory (development layout)
                string assembliesDir = Path.Combine(baseDir, "assemblies");
                if (Directory.Exists(assembliesDir))
                {
                    foreach (string name in searchNames)
                    {
                        string path = Path.Combine(assembliesDir, name);
                        if (File.Exists(path))
                            return path;
                    }
                }

                // Check the base directory directly (deployment layout)
                foreach (string name in searchNames)
                {
                    string path = Path.Combine(baseDir, name);
                    if (File.Exists(path))
                        return path;
                }
            }

            return null;
        }

        /// <summary>
        /// Custom DllImport resolver for cross-platform native library loading.
        /// Handles FMOD library resolution on Linux/macOS where dllmap is not supported.
        /// </summary>
        private static IntPtr ResolveDll(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName != "fmod")
                return IntPtr.Zero;

            // If we already loaded it, return the cached handle
            if (_fmodHandle != IntPtr.Zero)
                return _fmodHandle;

            string? lib = FindFmodLibrary();
            if (lib == null)
            {
                Console.Error.WriteLine("[NativeMethods] FMOD library not found on disk");
                return IntPtr.Zero;
            }

            try
            {
                _fmodHandle = NativeLibrary.Load(lib);
                Console.Error.WriteLine($"[NativeMethods] Resolved FMOD from {lib}");
                return _fmodHandle;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[NativeMethods] Failed to load {lib}: {ex.Message}");
                return IntPtr.Zero;
            }
        }
#endif
    }
}
