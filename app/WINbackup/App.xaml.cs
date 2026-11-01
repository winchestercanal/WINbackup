using System.Text;
using Microsoft.UI.Xaml;
using WINbackup.Services;

namespace WINbackup;

public partial class App : Application
{
    public static MainWindow Shell { get; private set; } = null!;

    public App()
    {
        // pnputil e robocopy escrevem na página de código OEM do console (ex.: 850 em pt-BR)
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        InitializeComponent();
        UnhandledException += (_, e) => CrashLog.Write(e.Exception);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Settings.Load();
        Shell = new MainWindow();
        Notifier.Init(WinRT.Interop.WindowNative.GetWindowHandle(Shell));
        Shell.Activate();
    }
}
