using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WINbackup.Services;

/// <summary>
/// Abre um programa como o usuário comum, mesmo com o WINbackup elevado: pede ao Explorer (que roda
/// sem elevação) para executar. Evita, por exemplo, abrir o Chrome como administrador num segundo
/// processo que brigaria pelo perfil com o Chrome já aberto.
/// </summary>
public static class ShellLauncher
{
    public static void Run(string file, string args)
    {
        if (Sys.IsAdmin)
        {
            try
            {
                RunViaExplorer(file, args);
                return;
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex);
            }
        }
        Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = true });
    }

    static void RunViaExplorer(string file, string args)
    {
        dynamic windows = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39"))!)!;
        object location = 0;      // CSIDL_DESKTOP
        object root = null!;      // VT_EMPTY
        object desktop = windows.FindWindowSW(ref location, ref root, 8 /* SWC_DESKTOP */, out int _, 1 /* SWFO_NEEDDISPATCH */);

        var service = (IServiceProvider)desktop;
        var sidTopLevel = new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837");
        var iidBrowser = typeof(IShellBrowser).GUID;
        service.QueryService(ref sidTopLevel, ref iidBrowser, out var browserPtr);
        var browser = (IShellBrowser)Marshal.GetObjectForIUnknown(browserPtr);
        Marshal.Release(browserPtr);

        browser.QueryActiveShellView(out var view);
        var iidDispatch = new Guid("00020400-0000-0000-C000-000000000046");
        view.GetItemObject(0 /* SVGIO_BACKGROUND */, ref iidDispatch, out var folderView);
        dynamic shell = ((dynamic)folderView).Application;
        shell.ShellExecute(file, args, "", "open", 1);
    }

    [ComImport, Guid("6d5140c1-7436-11ce-8034-00aa006009fa"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IServiceProvider
    {
        void QueryService(ref Guid service, ref Guid riid, out IntPtr obj);
    }

    // Só QueryActiveShellView é usado; os anteriores ocupam as posições da vtable
    [ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellBrowser
    {
        [PreserveSig] int GetWindow(out IntPtr hwnd);
        [PreserveSig] int ContextSensitiveHelp(int enter);
        [PreserveSig] int InsertMenusSB(IntPtr a, IntPtr b);
        [PreserveSig] int SetMenuSB(IntPtr a, IntPtr b, IntPtr c);
        [PreserveSig] int RemoveMenusSB(IntPtr a);
        [PreserveSig] int SetStatusTextSB(IntPtr a);
        [PreserveSig] int EnableModelessSB(int a);
        [PreserveSig] int TranslateAcceleratorSB(IntPtr a, short b);
        [PreserveSig] int BrowseObject(IntPtr a, uint b);
        [PreserveSig] int GetViewStateStream(uint a, out IntPtr b);
        [PreserveSig] int GetControlWindow(uint a, out IntPtr b);
        [PreserveSig] int SendControlMsg(uint a, uint b, IntPtr c, IntPtr d, out IntPtr e);
        void QueryActiveShellView(out IShellView view);
    }

    [ComImport, Guid("000214E3-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellView
    {
        [PreserveSig] int GetWindow(out IntPtr hwnd);
        [PreserveSig] int ContextSensitiveHelp(int enter);
        [PreserveSig] int TranslateAccelerator(IntPtr a);
        [PreserveSig] int EnableModeless(int a);
        [PreserveSig] int UIActivate(uint a);
        [PreserveSig] int Refresh();
        [PreserveSig] int CreateViewWindow(IntPtr a, IntPtr b, IntPtr c, IntPtr d, out IntPtr e);
        [PreserveSig] int DestroyViewWindow();
        [PreserveSig] int GetCurrentInfo(IntPtr a);
        [PreserveSig] int AddPropertySheetPages(uint a, IntPtr b, IntPtr c);
        [PreserveSig] int SaveViewState();
        [PreserveSig] int SelectItem(IntPtr a, uint b);
        void GetItemObject(uint item, ref Guid riid, [MarshalAs(UnmanagedType.IDispatch)] out object obj);
    }
}
