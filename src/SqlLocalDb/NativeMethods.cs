﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NativeMethods.cs" company="https://github.com/martincostello/sqllocaldb">
//   Martin Costello (c) 2012-2015
// </copyright>
// <license>
//   See license.txt in the project root for license information.
// </license>
// <summary>
//   NativeMethods.cs
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Microsoft.Win32;

namespace System.Data.SqlLocalDb
{
    /// <summary>
    /// A class containing native P/Invoke methods.  This class cannot be inherited.
    /// </summary>
    [SecurityCritical]
    internal static class NativeMethods
    {
        /// <summary>
        /// The maximum size of SQL Server LocalDB connection string.
        /// </summary>
        internal const int LOCALDB_MAX_SQLCONNECTION_BUFFER_SIZE = 260;

        /// <summary>
        /// The maximum size of SQL Server LocalDB instance names.
        /// </summary>
        internal const int MAX_LOCALDB_INSTANCE_NAME_LENGTH = 128;

        /// <summary>
        /// The maximum size of an SQL Server LocalDB version string.
        /// </summary>
        internal const int MAX_LOCALDB_VERSION_LENGTH = 43;

        /// <summary>
        /// The maximum length of a SID string.
        /// </summary>
        internal const int MAX_STRING_SID_LENGTH = 186;

        /// <summary>
        /// Specifies that error messages that are too long should be truncated.
        /// </summary>
        private const int LOCALDB_TRUNCATE_ERR_MESSAGE = 1;

        /// <summary>
        /// This value represents the recommended maximum number of directories an application should include in its DLL search path.
        /// </summary>
        /// <remarks>
        /// Only supported on Windows Vista, 7, Server 2008 and Server 2008 R2 with KB2533623.
        /// See <c>https://msdn.microsoft.com/en-us/library/windows/desktop/ms684179%28v=vs.85%29.aspx</c>.
        /// </remarks>
        private const int LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;

        /// <summary>
        /// The name of the Windows Kernel library.
        /// </summary>
        private const string KernelLibName = "kernel32.dll";

        /// <summary>
        /// An array containing the '\0' character. This field is read-only.
        /// </summary>
        private static readonly char[] _nullArray = new char[] { '\0' };

        /// <summary>
        /// Synchronization object to protect loading the native library and its functions.
        /// </summary>
        private static readonly object _syncRoot = new object();

        /// <summary>
        /// The handle to the native SQL LocalDB API.
        /// </summary>
        private static SafeLibraryHandle _localDB;

        /// <summary>
        /// The delegate to the <c>LocalDBCreateInstance</c> LocalDB API function.
        /// </summary>
        private static Functions.LocalDBCreateInstance _localDBCreateInstance;

        /// <summary>
        /// The delegate to the <c>LocalDBDeleteInstance</c> LocalDB API function.
        /// </summary>
        private static Functions.LocalDBDeleteInstance _localDBDeleteInstance;

        /// <summary>
        /// The delegate to the <c>LocalDBFormatMessage</c> LocalDB API function.
        /// </summary>
        private static Functions.LocalDBFormatMessage _localDBFormatMessage;

        /// <summary>
        /// The delegate to the <c>LocalDBGetInstanceInfo</c> LocalDB API function.
        /// </summary>
        private static Functions.LocalDBGetInstanceInfo _localDBGetInstanceInfo;

        /// <summary>
        /// The delegate to the <c>LocalDBGetInstances</c> LocalDB API function.
        /// </summary>
        private static Functions.LocalDBGetInstances _localDBGetInstances;

        /// <summary>
        /// The delegate to the <c>LocalDBGetVersionInfo</c> LocalDB API function.
        /// </summary>
        private static Functions.LocalDBGetVersionInfo _localDBGetVersionInfo;

        /// <summary>
        /// The delegate to the <c>LocalDBGetVersions</c> LocalDB API function.
        /// </summary>
        private static Functions.LocalDBGetVersions _localDBGetVersions;

        /// <summary>
        /// The delegate to the <c>LocalDBShareInstance</c> LocalDB API function.
        /// </summary>
        private static Functions.LocalDBShareInstance _localDBShareInstance;

        /// <summary>
        /// The delegate to the <c>LocalDBStartInstance</c> LocalDB API function.
        /// </summary>
        private static Functions.LocalDBStartInstance _localDBStartInstance;

        /// <summary>
        /// The delegate to the <c>LocalDBStartTracing</c> LocalDB API function.
        /// </summary>
        private static Functions.LocalDBStartTracing _localDBStartTracing;

        /// <summary>
        /// The delegate to the <c>LocalDBStopInstance</c> LocalDB API function.
        /// </summary>
        private static Functions.LocalDBStopInstance _localDBStopInstance;

        /// <summary>
        /// The delegate to the <c>LocalDBStopTracing</c> LocalDB API function.
        /// </summary>
        private static Functions.LocalDBStopTracing _localDBStopTracing;

        /// <summary>
        /// The delegate to the <c>LocalDBUnshareInstance</c> LocalDB API function.
        /// </summary>
        private static Functions.LocalDBUnshareInstance _localDBUnshareInstance;

        /// <summary>
        /// The <see cref="IRegistry"/> to use.
        /// </summary>
        private static IRegistry _registry;

        /// <summary>
        /// Defines a method for opening a registry sub-key.
        /// </summary>
        internal interface IRegistry
        {
            /// <summary>
            /// Retrieves a sub-key as read-only.
            /// </summary>
            /// <param name="keyName">The name or path of the sub-key to open as read-only.</param>
            /// <returns>
            /// The sub-key requested, or <see langword="null"/> if the operation failed.
            /// </returns>
            IRegistryKey OpenSubKey(string keyName);
        }

        /// <summary>
        /// Defines a registry sub-key.
        /// </summary>
        internal interface IRegistryKey : IRegistry, IDisposable
        {
            /// <summary>
            /// Retrieves an array of strings that contains all the sub-key names.
            /// </summary>
            /// <returns>
            /// An array of strings that contains the names of the sub-keys for the current key.
            /// </returns>
            string[] GetSubKeyNames();

            /// <summary>
            /// Retrieves the value associated with the specified name.
            /// </summary>
            /// <param name="name">The name of the value to retrieve. This string is not case-sensitive.</param>
            /// <returns>
            /// The value associated with <paramref name="name"/>, or <see langword="null"/> if <paramref name="name"/> is not found.
            /// </returns>
            string GetValue(string name);
        }

        /// <summary>
        /// Gets the version of the SQL LocalDB native API loaded, if any.
        /// </summary>
        internal static Version NativeApiVersion
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets or sets the <see cref="IRegistry"/> to use.
        /// </summary>
        /// <remarks>
        /// Used for unit testing.
        /// </remarks>
        internal static IRegistry Registry
        {
            get { return _registry ?? WindowsRegistry.Instance; }
            set { _registry = value; }
        }

        /// <summary>
        /// Creates a new instance of SQL Server LocalDB.
        /// </summary>
        /// <param name="wszVersion">The LocalDB version, for example 11.0 or 11.0.1094.2.</param>
        /// <param name="pInstanceName">The name for the LocalDB instance to create.</param>
        /// <param name="dwFlags">Reserved for future use. Currently should be set to 0.</param>
        /// <returns>The HRESULT returned by the LocalDB API.</returns>
        internal static int CreateInstance(string wszVersion, string pInstanceName, int dwFlags)
        {
            return EnsureFunctionAndInvoke(
                "LocalDBCreateInstance",
                ref _localDBCreateInstance,
                (function) => function(wszVersion, pInstanceName, dwFlags));
        }

        /// <summary>
        /// Deletes the specified SQL Server Express LocalDB instance.
        /// </summary>
        /// <param name="pInstanceName">The name of the LocalDB instance to delete.</param>
        /// <param name="dwFlags">Reserved for future use. Currently should be set to 0.</param>
        /// <returns>The HRESULT returned by the LocalDB API.</returns>
        internal static int DeleteInstance(string pInstanceName, int dwFlags)
        {
            return EnsureFunctionAndInvoke(
                "LocalDBDeleteInstance",
                ref _localDBDeleteInstance,
                (function) => function(pInstanceName, dwFlags));
        }

        /// <summary>
        /// Frees a specified library.
        /// </summary>
        /// <param name="handle">The handle to the module to free.</param>
        /// <returns>Whether the library was successfully unloaded.</returns>
        [DllImport(KernelLibName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        [SecurityCritical]
        internal static extern bool FreeLibrary(IntPtr handle);

        /// <summary>
        /// Returns information for the specified SQL Server Express LocalDB instance,
        /// such as whether it exists, the LocalDB version it uses, whether it is running,
        /// and so on.
        /// </summary>
        /// <param name="wszInstanceName">The instance name.</param>
        /// <param name="pInstanceInfo">The buffer to store the information about the LocalDB instance.</param>
        /// <param name="dwInstanceInfoSize">Holds the size of the InstanceInfo buffer.</param>
        /// <returns>The HRESULT returned by the LocalDB API.</returns>
        internal static int GetInstanceInfo(string wszInstanceName, IntPtr pInstanceInfo, int dwInstanceInfoSize)
        {
            return EnsureFunctionAndInvoke(
                "LocalDBGetInstanceInfo",
                ref _localDBGetInstanceInfo,
                (function) => function(wszInstanceName, pInstanceInfo, dwInstanceInfoSize));
        }

        /// <summary>
        /// Returns all SQL Server Express LocalDB instances with the given version.
        /// </summary>
        /// <param name="pInstanceNames">
        /// When this function returns, contains the names of both named and default
        /// LocalDB instances on the user’s workstation.
        /// </param>
        /// <param name="lpdwNumberOfInstances">
        /// On input, contains the number of slots for instance names in the
        /// <paramref name="pInstanceNames"/> buffer. On output, contains the number
        /// of LocalDB instances found on the user’s workstation.
        /// </param>
        /// <returns>The HRESULT returned by the LocalDB API.</returns>
        internal static int GetInstanceNames(IntPtr pInstanceNames, ref int lpdwNumberOfInstances)
        {
            var function = EnsureFunction("LocalDBGetInstances", ref _localDBGetInstances);

            if (function == null)
            {
                return SqlLocalDbErrors.NotInstalled;
            }

            return function(pInstanceNames, ref lpdwNumberOfInstances);
        }

        /// <summary>
        /// Returns the localized textual description for the specified SQL Server Express LocalDB error.
        /// </summary>
        /// <param name="hrLocalDB">The LocalDB error code.</param>
        /// <param name="dwLanguageId">The language desired (LANGID) or 0, in which case the Win32 FormatMessage language order is used.</param>
        /// <param name="wszMessage">The buffer to store the LocalDB error message.</param>
        /// <param name="lpcchMessage">
        /// On input contains the size of the <paramref name="wszMessage"/> buffer in characters. On output,
        /// if the given buffer size is too small, contains the buffer size required in characters, including
        /// any trailing nulls.  If the function succeeds, contains the number of characters in the message,
        /// excluding any trailing nulls.
        /// </param>
        /// <returns>The HRESULT returned by the LocalDB API.</returns>
        internal static int GetLocalDbError(int hrLocalDB, int dwLanguageId, StringBuilder wszMessage, ref int lpcchMessage)
        {
            var function = EnsureFunction("LocalDBFormatMessage", ref _localDBFormatMessage);

            if (function == null)
            {
                return SqlLocalDbErrors.NotInstalled;
            }

            return function(hrLocalDB, LOCALDB_TRUNCATE_ERR_MESSAGE, dwLanguageId, wszMessage, ref lpcchMessage);
        }

        /// <summary>
        /// Returns information for the specified SQL Server Express LocalDB version,
        /// such as whether it exists and the full LocalDB version number (including
        /// build and release numbers).
        /// </summary>
        /// <param name="wszVersionName">The LocalDB version name.</param>
        /// <param name="pVersionInfo">The buffer to store the information about the LocalDB version.</param>
        /// <param name="dwVersionInfoSize">Holds the size of the VersionInfo buffer.</param>
        /// <returns>The HRESULT returned by the LocalDB API.</returns>
        internal static int GetVersionInfo(string wszVersionName, IntPtr pVersionInfo, int dwVersionInfoSize)
        {
            return EnsureFunctionAndInvoke(
                "LocalDBGetVersionInfo",
                ref _localDBGetVersionInfo,
                (function) => function(wszVersionName, pVersionInfo, dwVersionInfoSize));
        }

        /// <summary>
        /// Returns all SQL Server Express LocalDB versions available on the computer.
        /// </summary>
        /// <param name="pVersion">Contains names of the LocalDB versions that are available on the user’s workstation.</param>
        /// <param name="lpdwNumberOfVersions">
        /// On input holds the number of slots for versions in the <paramref name="pVersion"/>
        /// buffer. On output, holds the number of existing LocalDB versions.
        /// </param>
        /// <returns>The HRESULT returned by the LocalDB API.</returns>
        internal static int GetVersions(IntPtr pVersion, ref int lpdwNumberOfVersions)
        {
            var function = EnsureFunction("LocalDBGetVersions", ref _localDBGetVersions);

            if (function == null)
            {
                return SqlLocalDbErrors.NotInstalled;
            }

            return function(pVersion, ref lpdwNumberOfVersions);
        }

        /// <summary>
        /// Shares the specified SQL Server Express LocalDB instance with other
        /// users of the computer, using the specified shared name.
        /// </summary>
        /// <param name="pOwnerSID">The SID of the instance owner.</param>
        /// <param name="pInstancePrivateName">The private name for the LocalDB instance to share.</param>
        /// <param name="pInstanceSharedName">The shared name for the LocalDB instance to share.</param>
        /// <param name="dwFlags">Reserved for future use. Currently should be set to 0.</param>
        /// <returns>The HRESULT returned by the LocalDB API.</returns>
        internal static int ShareInstance(IntPtr pOwnerSID, string pInstancePrivateName, string pInstanceSharedName, int dwFlags)
        {
            return EnsureFunctionAndInvoke(
                "LocalDBShareInstance",
                ref _localDBShareInstance,
                (function) => function(pOwnerSID, pInstancePrivateName, pInstanceSharedName, dwFlags));
        }

        /// <summary>
        /// Starts the specified SQL Server Express LocalDB instance.
        /// </summary>
        /// <param name="pInstanceName">The name of the LocalDB instance to start.</param>
        /// <param name="dwFlags">Reserved for future use. Currently should be set to 0.</param>
        /// <param name="wszSqlConnection">The buffer to store the connection string to the LocalDB instance.</param>
        /// <param name="lpcchSqlConnection">
        /// On input contains the size of the <paramref name="wszSqlConnection"/> buffer in
        /// characters, including any trailing nulls. On output, if the given buffer size is
        /// too small, contains the required buffer size in characters, including any trailing nulls.
        /// </param>
        /// <returns>The HRESULT returned by the LocalDB API.</returns>
        internal static int StartInstance(string pInstanceName, int dwFlags, StringBuilder wszSqlConnection, ref int lpcchSqlConnection)
        {
            var function = EnsureFunction("LocalDBStartInstance", ref _localDBStartInstance);

            if (function == null)
            {
                return SqlLocalDbErrors.NotInstalled;
            }

            return function(pInstanceName, dwFlags, wszSqlConnection, ref lpcchSqlConnection);
        }

        /// <summary>
        /// Enables tracing of API calls for all the SQL Server Express
        /// LocalDB instances owned by the current Windows user.
        /// </summary>
        /// <returns>The HRESULT returned by the LocalDB API.</returns>
        internal static int StartTracing()
        {
            return EnsureFunctionAndInvoke(
                "LocalDBStartTracing",
                ref _localDBStartTracing,
                (function) => function());
        }

        /// <summary>
        /// Stops the specified SQL Server Express LocalDB instance from running.
        /// </summary>
        /// <param name="pInstanceName">The name of the LocalDB instance to stop.</param>
        /// <param name="options">One or a combination of the flag values specifying the way to stop the instance.</param>
        /// <param name="ulTimeout">
        /// The time in seconds to wait for this operation to complete. If this
        /// value is 0, this function will return immediately without waiting for the LocalDB instance to stop.
        /// </param>
        /// <returns>The HRESULT returned by the LocalDB API.</returns>
        internal static int StopInstance(string pInstanceName, StopInstanceOptions options, int ulTimeout)
        {
            return EnsureFunctionAndInvoke(
                "LocalDBStopInstance",
                ref _localDBStopInstance,
                (function) => function(pInstanceName, (int)options, ulTimeout));
        }

        /// <summary>
        /// Disables tracing of API calls for all the SQL Server Express LocalDB
        /// instances owned by the current Windows user.
        /// </summary>
        /// <returns>The HRESULT returned by the LocalDB API.</returns>
        internal static int StopTracing()
        {
            return EnsureFunctionAndInvoke(
                "LocalDBStopTracing",
                ref _localDBStopTracing,
                (function) => function());
        }

        /// <summary>
        /// Stops the sharing of the specified SQL Server Express LocalDB instance.
        /// </summary>
        /// <param name="pInstanceName">
        /// The private name for the LocalDB instance to share.
        /// </param>
        /// <param name="dwFlags">
        /// Reserved for future use. Currently should be set to 0.
        /// </param>
        /// <returns>The HRESULT returned by the LocalDB API.</returns>
        internal static int UnshareInstance(string pInstanceName, int dwFlags)
        {
            return EnsureFunctionAndInvoke(
                "LocalDBUnshareInstance",
                ref _localDBUnshareInstance,
                (function) => function(pInstanceName, dwFlags));
        }

        /// <summary>
        /// Marshals the specified <see cref="Array"/> of <see cref="byte"/> to a <see cref="string"/>.
        /// </summary>
        /// <param name="bytes">The array to marshal as a <see cref="string"/>.</param>
        /// <returns>
        /// A <see cref="string"/> representation of <paramref name="bytes"/>.
        /// </returns>
        internal static string MarshalString(byte[] bytes)
        {
            Debug.Assert(bytes != null, "bytes cannot be null.");
            return Encoding.Unicode.GetString(bytes).TrimEnd(_nullArray);
        }

        /// <summary>
        /// Tries to obtaining the path to the latest version of the SQL LocalDB
        /// native API DLL for the currently executing process.
        /// </summary>
        /// <param name="fileName">
        /// When the method returns, contains the path to the SQL Local DB API
        /// to use, if found; otherwise <see langword="null"/>.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the native API path was successfully found;
        /// otherwise <see langword="false"/>.
        /// </returns>
        internal static bool TryGetLocalDbApiPath(out string fileName)
        {
            fileName = null;

            bool isWow64Process = Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess;

            // Open the appropriate Registry key if running as a 32-bit process on a 64-bit machine
            string keyName = string.Format(
                CultureInfo.InvariantCulture,
                @"SOFTWARE\{0}Microsoft\Microsoft SQL Server Local DB\Installed Versions",
                isWow64Process ? @"Wow6432Node\" : string.Empty);

            IRegistryKey key = Registry.OpenSubKey(keyName);

            if (key == null)
            {
                Logger.Warning(Logger.TraceEvent.RegistryKeyNotFound, SR.NativeMethods_RegistryKeyNotFoundFormat, keyName);
                return false;
            }

            Version latestVersion = null;
            Version overrideVersion = null;
            string path = null;

            try
            {
                // Is there a setting overriding the version to load?
                string overrideVersionString = SqlLocalDbConfig.NativeApiOverrideVersionString;

                foreach (string versionString in key.GetSubKeyNames())
                {
                    Version version;

                    try
                    {
                        version = new Version(versionString);
                    }
                    catch (ArgumentException)
                    {
                        Logger.Warning(Logger.TraceEvent.InvalidRegistryKey, SR.NativeMethods_InvalidRegistryKeyNameFormat, versionString);
                        continue;
                    }
                    catch (FormatException)
                    {
                        Logger.Warning(Logger.TraceEvent.InvalidRegistryKey, SR.NativeMethods_InvalidRegistryKeyNameFormat, versionString);
                        continue;
                    }
                    catch (OverflowException)
                    {
                        Logger.Warning(Logger.TraceEvent.InvalidRegistryKey, SR.NativeMethods_InvalidRegistryKeyNameFormat, versionString);
                        continue;
                    }

                    if (!string.IsNullOrEmpty(overrideVersionString) &&
                        overrideVersion == null &&
                        string.Equals(versionString, overrideVersionString, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Verbose(Logger.TraceEvent.NativeApiVersionOverriddenByUser, SR.NativeMethods_ApiVersionOverriddenByUserFormat, version);
                        overrideVersion = version;
                    }

                    if (latestVersion == null ||
                        latestVersion < version)
                    {
                        latestVersion = version;
                    }
                }

                if (!string.IsNullOrEmpty(overrideVersionString) && overrideVersion == null)
                {
                    Logger.Warning(
                        Logger.TraceEvent.NativeApiVersionOverrideNotFound,
                        SR.NativeMethods_OverrideVersionNotFoundFormat,
                        overrideVersionString,
                        Environment.MachineName,
                        latestVersion);
                }

                Version versionToUse = overrideVersion ?? latestVersion;

                if (versionToUse != null)
                {
                    using (var subkey = key.OpenSubKey(versionToUse.ToString()))
                    {
                        path = subkey.GetValue("InstanceAPIPath");
                    }

                    NativeApiVersion = versionToUse;
                }
            }
            finally
            {
                key.Dispose();
            }

            if (string.IsNullOrEmpty(path))
            {
                Logger.Warning(Logger.TraceEvent.NoNativeApiFound, SR.NativeMethods_NoNativeApiFound);
                return false;
            }

            if (!File.Exists(path))
            {
                Logger.Error(Logger.TraceEvent.NativeApiPathNotFound, SR.NativeMethods_NativeApiNotFoundFormat, path);
                return false;
            }

            fileName = Path.GetFullPath(path);
            return true;
        }

        /// <summary>
        /// Retrieves the address of an exported function or variable from the specified dynamic-link library (DLL).
        /// </summary>
        /// <param name="hModule">A handle to the DLL module that contains the function or variable. </param>
        /// <param name="lpProcName">The function or variable name, or the function's ordinal value.</param>
        /// <returns>
        /// If the function succeeds, the return value is the address of the exported function or variable.
        /// If the function fails, the return value is <see cref="IntPtr.Zero"/>.
        /// </returns>
        /// <remarks>
        /// See <c>http://msdn.microsoft.com/en-us/library/windows/desktop/ms683212%28v=vs.85%29.aspx</c>.
        /// </remarks>
        [DllImport(KernelLibName, BestFitMapping = false, CharSet = CharSet.Ansi, ThrowOnUnmappableChar = true)]
        private static extern IntPtr GetProcAddress(
            SafeLibraryHandle hModule,
            [MarshalAs(UnmanagedType.LPStr)]
            string lpProcName);

        /// <summary>
        /// Loads the specified module into the address space of the calling process.
        /// The specified module may cause other modules to be loaded.
        /// </summary>
        /// <param name="lpFileName">The name of the module.</param>
        /// <param name="hFile">This parameter is reserved for future use. It must be <see cref="IntPtr.Zero"/>.</param>
        /// <param name="dwFlags">The action to be taken when loading the module.</param>
        /// <returns>
        /// If the function succeeds, the return value is a handle to the module.
        /// If the function fails, the return value is <see langword="null"/>.
        /// </returns>
        /// <remarks>
        /// See <c>https://msdn.microsoft.com/en-us/library/windows/desktop/ms684179%28v=vs.85%29.aspx</c>.
        /// </remarks>
        [DllImport(KernelLibName, BestFitMapping = false, CharSet = CharSet.Ansi, SetLastError = true, ThrowOnUnmappableChar = true)]
        private static extern SafeLibraryHandle LoadLibraryEx(
            [MarshalAs(UnmanagedType.LPStr)]
            string lpFileName,
            IntPtr hFile,
            int dwFlags);

        /// <summary>
        /// Ensures that the specified delegate to an unmanaged function is initialized.
        /// </summary>
        /// <typeparam name="T">The type of the delegate representing the unmanaged function.</typeparam>
        /// <param name="functionName">The name of the unmanaged function to ensure is loaded.</param>
        /// <param name="function">A reference to a location to ensure contains a delegate for the specified function name.</param>
        /// <returns>
        /// An instance of <typeparamref name="T"/> that points to the specified unmanaged
        /// function, if found; otherwise <see langword="null"/>.
        /// </returns>
        private static T EnsureFunction<T>(string functionName, ref T function)
            where T : class
        {
            Debug.Assert(functionName != null, "functionName cannot be null.");

            if (function == null)
            {
                lock (_syncRoot)
                {
                    if (function == null)
                    {
                        function = GetDelegate<T>(functionName);
                    }
                }
            }

            return function;
        }

        /// <summary>
        /// Ensures that the specified delegate to an unmanaged function is initialized and invokes the specified callback delegate if it does.
        /// </summary>
        /// <typeparam name="T">The type of the delegate representing the unmanaged function.</typeparam>
        /// <param name="functionName">The name of the unmanaged function to ensure is loaded.</param>
        /// <param name="function">A reference to a location to ensure contains a delegate for the specified function name.</param>
        /// <param name="callback">A delegate to a callback method to invoke with the function if initialized.</param>
        /// <returns>
        /// The <see cref="int"/> result of invoking <paramref name="callback"/>, if the function was
        /// initialized; otherwise the value of <see cref="SqlLocalDbErrors.NotInstalled"/> is returned.
        /// </returns>
        private static int EnsureFunctionAndInvoke<T>(string functionName, ref T function, Func<T, int> callback)
            where T : class
        {
            Debug.Assert(callback != null, "callback cannot be null.");

            function = EnsureFunction(functionName, ref function);

            return function == null ? SqlLocalDbErrors.NotInstalled : callback(function);
        }

        /// <summary>
        /// Ensures that the LocalDB native API has been loaded.
        /// </summary>
        /// <returns>
        /// A <see cref="SafeLibraryHandle"/> pointing to the loaded
        /// SQL LocalDB API, if successful; otherwise <see langword="null"/>.
        /// </returns>
        private static SafeLibraryHandle EnsureLocalDBLoaded()
        {
            if (_localDB == null)
            {
                lock (_syncRoot)
                {
                    if (_localDB == null)
                    {
                        if (!TryGetLocalDbApiPath(out string fileName))
                        {
                            return null;
                        }

                        int dwFlags = 0;

                        // Check if the local machine has KB2533623 installed in order
                        // to use the more secure flags when calling LoadLibraryEx
                        bool hasKB2533623;

                        using (var hModule = LoadLibraryEx(KernelLibName, IntPtr.Zero, 0))
                        {
                            // If the AddDllDirectory function is found then the flags are supported
                            hasKB2533623 = GetProcAddress(hModule, "AddDllDirectory") != IntPtr.Zero;
                        }

                        if (hasKB2533623)
                        {
                            // If KB2533623 is installed then specify the more secure LOAD_LIBRARY_SEARCH_DEFAULT_DIRS in dwFlags
                            dwFlags = LOAD_LIBRARY_SEARCH_DEFAULT_DIRS;
                        }

                        _localDB = LoadLibraryEx(fileName, IntPtr.Zero, dwFlags);

                        if (_localDB == null ||
                            _localDB.IsInvalid)
                        {
                            int error = Marshal.GetLastWin32Error();
                            Logger.Error(Logger.TraceEvent.NativeApiLoadFailed, SR.NativeMethods_NativeApiLoadFailedFormat, fileName, error);
                            _localDB = null;
                        }
                        else
                        {
                            Logger.Verbose(Logger.TraceEvent.NativeApiLoaded, SR.NativeMethods_NativeApiLoadedFormat, fileName);
                        }
                    }
                }
            }

            return _localDB;
        }

        /// <summary>
        /// Returns a delegate of the specified type to the specified unmanaged function.
        /// </summary>
        /// <typeparam name="T">The type of the delegate to return.</typeparam>
        /// <param name="functionName">The name of the unmanaged function.</param>
        /// <returns>
        /// An instance of <typeparamref name="T"/> that points to the specified unmanaged
        /// function, if found; otherwise <see langword="null"/>.
        /// </returns>
        private static T GetDelegate<T>(string functionName)
            where T : class
        {
            Debug.Assert(functionName != null, "functionName cannot be null.");

            SafeLibraryHandle handle = EnsureLocalDBLoaded();

            if (handle == null)
            {
                Logger.Warning(Logger.TraceEvent.NativeApiNotLoaded, SR.NativeMethods_NativeApiNotLoaded);
                return null;
            }

            IntPtr ptr = GetProcAddress(handle, functionName);

            if (ptr == IntPtr.Zero)
            {
                Logger.Error(Logger.TraceEvent.FunctionNotFound, SR.NativeMethods_FunctionNotFoundFormat, functionName);
                return null;
            }

            return Marshal.GetDelegateForFunctionPointer<T>(ptr);
        }

        /// <summary>
        /// A class containing delegates to functions in the SQL LocalDB native API.
        /// </summary>
        private static class Functions
        {
            /// <summary>
            /// Creates a new instance of SQL Server LocalDB.
            /// </summary>
            /// <param name="wszVersion">The LocalDB version, for example 11.0 or 11.0.1094.2.</param>
            /// <param name="pInstanceName">The name for the LocalDB instance to create.</param>
            /// <param name="dwFlags">Reserved for future use. Currently should be set to 0.</param>
            /// <returns>The HRESULT returned by the LocalDB API.</returns>
            /// <remarks>
            /// See <c>http://technet.microsoft.com/en-us/library/hh214784.aspx</c>.
            /// </remarks>
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal delegate int LocalDBCreateInstance(
                [MarshalAs(UnmanagedType.LPWStr)] string wszVersion,
                [MarshalAs(UnmanagedType.LPWStr)] string pInstanceName,
                int dwFlags);

            /// <summary>
            /// Deletes the specified SQL Server Express LocalDB instance.
            /// </summary>
            /// <param name="pInstanceName">The name of the LocalDB instance to delete.</param>
            /// <param name="dwFlags">Reserved for future use. Currently should be set to 0.</param>
            /// <returns>The HRESULT returned by the LocalDB API.</returns>
            /// <remarks>
            /// See <c>http://technet.microsoft.com/en-us/library/hh214724.aspx</c>.
            /// </remarks>
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal delegate int LocalDBDeleteInstance(
                [MarshalAs(UnmanagedType.LPWStr)] string pInstanceName,
                int dwFlags);

            /// <summary>
            /// Returns the localized textual description for the specified SQL Server Express LocalDB error.
            /// </summary>
            /// <param name="hrLocalDB">The LocalDB error code.</param>
            /// <param name="dwFlags">The flags specifying the behavior of this function.</param>
            /// <param name="dwLanguageId">The language desired (LANGID) or 0, in which case the Win32 FormatMessage language order is used.</param>
            /// <param name="wszMessage">The buffer to store the LocalDB error message.</param>
            /// <param name="lpcchMessage">
            /// On input contains the size of the <paramref name="wszMessage"/> buffer in characters. On output,
            /// if the given buffer size is too small, contains the buffer size required in characters, including
            /// any trailing nulls.  If the function succeeds, contains the number of characters in the message,
            /// excluding any trailing nulls.
            /// </param>
            /// <returns>The HRESULT returned by the LocalDB API.</returns>
            /// <remarks>
            /// See <c>http://technet.microsoft.com/en-us/library/hh214483.aspx</c>.
            /// </remarks>
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal delegate int LocalDBFormatMessage(
                int hrLocalDB,
                int dwFlags,
                int dwLanguageId,
                [MarshalAs(UnmanagedType.LPWStr)][Out] StringBuilder wszMessage,
                ref int lpcchMessage);

            /// <summary>
            /// Returns information for the specified SQL Server Express LocalDB instance,
            /// such as whether it exists, the LocalDB version it uses, whether it is running,
            /// and so on.
            /// </summary>
            /// <param name="wszInstanceName">The instance name.</param>
            /// <param name="pInstanceInfo">The buffer to store the information about the LocalDB instance.</param>
            /// <param name="dwInstanceInfoSize">Holds the size of the InstanceInfo buffer.</param>
            /// <returns>The HRESULT returned by the LocalDB API.</returns>
            /// <remarks>
            /// See <c>http://technet.microsoft.com/en-us/library/hh245734.aspx</c>.
            /// </remarks>
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal delegate int LocalDBGetInstanceInfo(
                [MarshalAs(UnmanagedType.LPWStr)]
                string wszInstanceName,
                IntPtr pInstanceInfo,
                int dwInstanceInfoSize);

            /// <summary>
            /// Returns all SQL Server Express LocalDB instances with the given version.
            /// </summary>
            /// <param name="pInstanceNames">
            /// When this function returns, contains the names of both named and default
            /// LocalDB instances on the user’s workstation.
            /// </param>
            /// <param name="lpdwNumberOfInstances">
            /// On input, contains the number of slots for instance names in the
            /// <paramref name="pInstanceNames"/> buffer. On output, contains the number
            /// of LocalDB instances found on the user’s workstation.
            /// </param>
            /// <returns>The HRESULT returned by the LocalDB API.</returns>
            /// <remarks>
            /// See <c>http://technet.microsoft.com/en-us/library/hh234622.aspx</c>.
            /// </remarks>
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal delegate int LocalDBGetInstances(IntPtr pInstanceNames, ref int lpdwNumberOfInstances);

            /// <summary>
            /// Returns information for the specified SQL Server Express LocalDB version,
            /// such as whether it exists and the full LocalDB version number (including
            /// build and release numbers).
            /// </summary>
            /// <param name="wszVersionName">The LocalDB version name.</param>
            /// <param name="pVersionInfo">The buffer to store the information about the LocalDB version.</param>
            /// <param name="dwVersionInfoSize">Holds the size of the VersionInfo buffer.</param>
            /// <returns>The HRESULT returned by the LocalDB API.</returns>
            /// <remarks>
            /// See <c>http://technet.microsoft.com/en-us/library/hh234365.aspx</c>.
            /// </remarks>
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal delegate int LocalDBGetVersionInfo(
                [MarshalAs(UnmanagedType.LPWStr)]
                string wszVersionName,
                IntPtr pVersionInfo,
                int dwVersionInfoSize);

            /// <summary>
            /// Returns all SQL Server Express LocalDB versions available on the computer.
            /// </summary>
            /// <param name="pVersion">Contains names of the LocalDB versions that are available on the user’s workstation.</param>
            /// <param name="lpdwNumberOfVersions">
            /// On input holds the number of slots for versions in the <paramref name="pVersion"/>
            /// buffer. On output, holds the number of existing LocalDB versions.
            /// </param>
            /// <returns>The HRESULT returned by the LocalDB API.</returns>
            /// <remarks>
            /// See <c>http://technet.microsoft.com/en-us/library/hh231031.aspx</c>.
            /// </remarks>
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal delegate int LocalDBGetVersions(IntPtr pVersion, ref int lpdwNumberOfVersions);

            /// <summary>
            /// Shares the specified SQL Server Express LocalDB instance with other
            /// users of the computer, using the specified shared name.
            /// </summary>
            /// <param name="pOwnerSID">The SID of the instance owner.</param>
            /// <param name="pInstancePrivateName">The private name for the LocalDB instance to share.</param>
            /// <param name="pInstanceSharedName">The shared name for the LocalDB instance to share.</param>
            /// <param name="dwFlags">Reserved for future use. Currently should be set to 0.</param>
            /// <returns>The HRESULT returned by the LocalDB API.</returns>
            /// <remarks>
            /// See <c>http://technet.microsoft.com/en-us/library/hh245693.aspx</c>.
            /// </remarks>
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal delegate int LocalDBShareInstance(
                IntPtr pOwnerSID,
                [MarshalAs(UnmanagedType.LPWStr)] string pInstancePrivateName,
                [MarshalAs(UnmanagedType.LPWStr)] string pInstanceSharedName,
                int dwFlags);

            /// <summary>
            /// Starts the specified SQL Server Express LocalDB instance.
            /// </summary>
            /// <param name="pInstanceName">The name of the LocalDB instance to start.</param>
            /// <param name="dwFlags">Reserved for future use. Currently should be set to 0.</param>
            /// <param name="wszSqlConnection">The buffer to store the connection string to the LocalDB instance.</param>
            /// <param name="lpcchSqlConnection">
            /// On input contains the size of the <paramref name="wszSqlConnection"/> buffer in
            /// characters, including any trailing nulls. On output, if the given buffer size is
            /// too small, contains the required buffer size in characters, including any trailing nulls.
            /// </param>
            /// <returns>The HRESULT returned by the LocalDB API.</returns>
            /// <remarks>
            /// See <c>http://technet.microsoft.com/en-us/library/hh217143.aspx</c>.
            /// </remarks>
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal delegate int LocalDBStartInstance(
                [MarshalAs(UnmanagedType.LPWStr)] string pInstanceName,
                int dwFlags,
                [MarshalAs(UnmanagedType.LPWStr)][Out] StringBuilder wszSqlConnection,
                ref int lpcchSqlConnection);

            /// <summary>
            /// Enables tracing of API calls for all the SQL Server Express
            /// LocalDB instances owned by the current Windows user.
            /// </summary>
            /// <returns>The HRESULT returned by the LocalDB API.</returns>
            /// <remarks>
            /// See <c>http://technet.microsoft.com/en-us/library/hh247594.aspx</c>.
            /// </remarks>
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal delegate int LocalDBStartTracing();

            /// <summary>
            /// Stops the specified SQL Server Express LocalDB instance from running.
            /// </summary>
            /// <param name="pInstanceName">The name of the LocalDB instance to stop.</param>
            /// <param name="dwFlags">One or a combination of the flag values specifying the way to stop the instance.</param>
            /// <param name="ulTimeout">
            /// The time in seconds to wait for this operation to complete. If this
            /// value is 0, this function will return immediately without waiting for the LocalDB instance to stop.
            /// </param>
            /// <returns>The HRESULT returned by the LocalDB API.</returns>
            /// <remarks>
            /// See <c>http://technet.microsoft.com/en-us/library/hh215035.aspx</c>.
            /// </remarks>
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal delegate int LocalDBStopInstance(
                [MarshalAs(UnmanagedType.LPWStr)]
                string pInstanceName,
                int dwFlags,
                int ulTimeout);

            /// <summary>
            /// Disables tracing of API calls for all the SQL Server Express LocalDB
            /// instances owned by the current Windows user.
            /// </summary>
            /// <returns>The HRESULT returned by the LocalDB API.</returns>
            /// <remarks>
            /// See <c>http://technet.microsoft.com/en-us/library/hh214120.aspx</c>.
            /// </remarks>
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal delegate int LocalDBStopTracing();

            /// <summary>
            /// Stops the sharing of the specified SQL Server Express LocalDB instance.
            /// </summary>
            /// <param name="pInstanceName">The private name for the LocalDB instance to share.</param>
            /// <param name="dwFlags">Reserved for future use. Currently should be set to 0.</param>
            /// <returns>The HRESULT returned by the LocalDB API.</returns>
            /// <remarks>
            /// See <c>http://technet.microsoft.com/en-us/library/hh215383.aspx</c>.
            /// </remarks>
            internal delegate int LocalDBUnshareInstance(
                [MarshalAs(UnmanagedType.LPWStr)]
                string pInstanceName,
                int dwFlags);
        }

        /// <summary>
        /// A class representing an implementation of <see cref="IRegistry"/> for the Windows registry. This class cannot be inherited.
        /// </summary>
        private sealed class WindowsRegistry : IRegistry
        {
            /// <summary>
            /// The singleton instance of <see cref="WindowsRegistry"/>. This field is read-only.
            /// </summary>
            internal static readonly WindowsRegistry Instance = new WindowsRegistry();

            /// <summary>
            /// Prevents a default instance of the <see cref="WindowsRegistry"/> class from being created.
            /// </summary>
            private WindowsRegistry()
            {
            }

            /// <inheritdoc />
            public IRegistryKey OpenSubKey(string keyName)
            {
                var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyName, writable: false);
                return key == null ? null : new WindowsRegistryKey(key);
            }
        }

        /// <summary>
        /// A class representing an implementation of <see cref="IRegistryKey"/> for a Windows registry key. This class cannot be inherited.
        /// </summary>
        private sealed class WindowsRegistryKey : IRegistryKey
        {
            /// <summary>
            /// The <see cref="RegistryKey"/> wrapped by the instance. This field is read-only.
            /// </summary>
            private readonly RegistryKey _key;

            /// <summary>
            /// Initializes a new instance of the <see cref="WindowsRegistryKey"/> class.
            /// </summary>
            /// <param name="key">The <see cref="RegistryKey"/> to wrap.</param>
            internal WindowsRegistryKey(RegistryKey key)
            {
                Debug.Assert(key != null, "key cannot be null.");
                _key = key;
            }

            /// <inheritdoc />
            void IDisposable.Dispose()
            {
                _key.Dispose();
            }

            /// <inheritdoc />
            public string[] GetSubKeyNames() => _key.GetSubKeyNames();

            /// <inheritdoc />
            public string GetValue(string name) => _key.GetValue(name, null, RegistryValueOptions.None) as string;

            /// <inheritdoc />
            public IRegistryKey OpenSubKey(string keyName)
            {
                var key = _key.OpenSubKey(keyName);
                return key == null ? null : new WindowsRegistryKey(key);
            }
        }
    }
}
