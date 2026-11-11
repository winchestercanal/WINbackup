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
        // O tema dos controles nativos só pode ser escolhido antes de carregar o XAML do app
        Settings.Load();
        RequestedTheme = Settings.Data.Theme == "light" ? ApplicationTheme.Light : ApplicationTheme.Dark;
        InitializeComponent();
        UnhandledException += (_, e) => CrashLog.Write(e.Exception);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        L.Apply(Settings.Data.Language);
        Theme.Apply(Settings.Data.Theme);
        Shell = new MainWindow();
        Notifier.Init(WinRT.Interop.WindowNative.GetWindowHandle(Shell));
        if (Cli.TryRun(Environment.GetCommandLineArgs().Skip(1).ToArray())) return;
        Shell.Activate();
        // Lembrete ao abrir o app (o agendado, ao entrar no Windows, é ligado em Opções)
        if (Settings.Data.NotifyReminder && Cli.DaysSinceLastBackup() is > 30 and var days)
            Notifier.Show(L.F("Faz {0} dias desde o último backup", days), L.T("Que tal fazer um agora?"));
    }
}
