using System.Runtime.InteropServices;

namespace WINbackup.Services;

/// <summary>
/// Diálogo de abrir pasta/arquivo do Windows via COM. Os seletores do WinUI não
/// funcionam quando o app roda como administrador, este funciona.
/// </summary>
public static class FileDialog
{
    const uint FOS_PICKFOLDERS = 0x20, FOS_FORCEFILESYSTEM = 0x40, FOS_PATHMUSTEXIST = 0x800, FOS_FILEMUSTEXIST = 0x1000;
    const uint SIGDN_FILESYSPATH = 0x80058000;

    public static string? PickFolder(string title) => Show(title, FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST, null);

    public static string? PickFile(string title, string filterName, string filterSpec) =>
        Show(title, FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST | FOS_FILEMUSTEXIST, [new FilterSpec { Name = filterName, Spec = filterSpec }]);

    static string? Show(string title, uint options, FilterSpec[]? filters)
    {
        var dialog = (IFileDialog)new FileOpenDialogCom();
        try
        {
            dialog.GetOptions(out var current);
            dialog.SetOptions(current | options);
            dialog.SetTitle(title);
            if (filters != null) dialog.SetFileTypes((uint)filters.Length, filters);
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Shell);
            if (dialog.Show(hwnd) != 0) return null;
            dialog.GetResult(out var item);
            item.GetDisplayName(SIGDN_FILESYSPATH, out var ptr);
            try { return Marshal.PtrToStringUni(ptr); }
            finally { Marshal.FreeCoTaskMem(ptr); }
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            return null;
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct FilterSpec
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string Name;
        [MarshalAs(UnmanagedType.LPWStr)] public string Spec;
    }

    [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
    class FileOpenDialogCom { }

    [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFileDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        void SetFileTypes(uint count, [MarshalAs(UnmanagedType.LPArray)] FilterSpec[] specs);
        void SetFileTypeIndex(uint index);
        void GetFileTypeIndex(out uint index);
        void Advise(IntPtr events, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(uint options);
        void GetOptions(out uint options);
        void SetDefaultFolder(IShellItem item);
        void SetFolder(IShellItem item);
        void GetFolder(out IShellItem item);
        void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName(out IntPtr name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void GetResult(out IShellItem item);
    }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint sigdn, out IntPtr name);
        void GetAttributes(uint mask, out uint attribs);
        void Compare(IShellItem other, uint hint, out int order);
    }
}
