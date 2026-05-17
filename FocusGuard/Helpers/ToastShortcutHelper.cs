using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace FocusGuard.Helpers;

public static class ToastShortcutHelper
{
    public const string AppUserModelId = "FocusGuard.Desktop";

    public static void EnsureShortcut()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var shortcutPath = GetShortcutPath();
        var executablePath = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(executablePath) || File.Exists(shortcutPath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);

        var shellLinkType = Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"));
        if (shellLinkType is null)
        {
            return;
        }

        var shellLink = (IShellLinkW)Activator.CreateInstance(shellLinkType)!;
        shellLink.SetPath(executablePath);
        shellLink.SetArguments(string.Empty);
        shellLink.SetIconLocation(executablePath, 0);

        using var appId = PropVariant.FromString(AppUserModelId);
        var propertyStore = (IPropertyStore)shellLink;
        var appIdKey = PropertyKeys.AppUserModelId;
        propertyStore.SetValue(ref appIdKey, ref appId.Value);
        propertyStore.Commit();

        var persistFile = (IPersistFile)shellLink;
        persistFile.Save(shortcutPath, true);
    }

    private static string GetShortcutPath()
    {
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        return Path.Combine(programs, "Programs", "FocusGuard.lnk");
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("00000138-0000-0000-C000-000000000046")]
    private interface IPropertyStore
    {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PropertyKey pkey);
        void GetValue(ref PropertyKey key, out PropVariantValue pv);
        void SetValue(ref PropertyKey key, ref PropVariantValue pv);
        void Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey
    {
        public Guid FormatId;
        public uint PropertyId;
    }

    private static class PropertyKeys
    {
        public static PropertyKey AppUserModelId = new()
        {
            FormatId = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
            PropertyId = 5
        };
    }

    private sealed class PropVariant : IDisposable
    {
        public PropVariantValue Value;

        public static PropVariant FromString(string value)
        {
            return new PropVariant
            {
                Value = new PropVariantValue
                {
                    VariantType = 31,
                    Pointer = Marshal.StringToCoTaskMemUni(value)
                }
            };
        }

        public void Dispose()
        {
            PropVariantClear(ref Value);
        }

        [DllImport("Ole32.dll")]
        private static extern int PropVariantClear(ref PropVariantValue pvar);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariantValue
    {
        public ushort VariantType;
        public ushort Reserved1;
        public ushort Reserved2;
        public ushort Reserved3;
        public IntPtr Pointer;
    }
}
