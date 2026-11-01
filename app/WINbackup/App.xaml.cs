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
        if (Cli.TryRun(Environment.GetCommandLineArgs().Skip(1).ToArray())) return;
        Shell.Activate();
        // Lembrete ao abrir o app (o agendado, ao entrar no Windows, é ligado em Opções)
        if (Settings.Data.NotifyReminder && Cli.DaysSinceLastBackup() is > 30 and var days)
            Notifier.Show($"Faz {days} dias desde o último backup", "Que tal fazer um agora?");
    }
}
