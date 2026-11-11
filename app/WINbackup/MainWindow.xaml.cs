using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Graphics;
using WINbackup.Backup;
using WINbackup.Controls;
using WINbackup.Models;
using WINbackup.Pages;
using WINbackup.Services;

namespace WINbackup;

public sealed partial class MainWindow : Window
{
    static readonly Dictionary<string, Type> Pages = new()
    {
        ["home"] = typeof(HomePage),
        ["files"] = typeof(FilesPage),
        ["creators"] = typeof(CreatorsPage),
        ["donate"] = typeof(DonatePage),
        ["apps"] = typeof(AppsPage),
        ["configs"] = typeof(ConfigsPage),
        ["games"] = typeof(GamesPage),
        ["drivers"] = typeof(DriversPage),
        ["browsers"] = typeof(BrowsersPage),
        ["restore"] = typeof(RestorePage),
        ["wizard"] = typeof(WizardPage),
        ["activity"] = typeof(ActivityPage),
        ["schedule"] = typeof(SchedulePage),
        ["history"] = typeof(HistoryPage),
        ["options"] = typeof(OptionsPage),
        ["progress"] = typeof(ProgressPage),
        ["report"] = typeof(ReportPage),
    };

    bool _closeConfirmed;
    readonly Stack<string> _history = new();
    string? _current;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);

        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        ApplyChrome();

        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "WINbackup.ico"));
        SizeAndCenter();
        Root.Loaded += (_, _) => AdminBadge.Margin = new Thickness(0, 0, AppWindow.TitleBar.RightInset / Root.XamlRoot.RasterizationScale + 12, 0);
        AppWindow.Closing += OnClosing;
        Closed += (_, _) => Notifier.Shutdown();

        Nav = NewSidebar();
        BackButton.SizeChanged += (_, _) => UpdateTitleBarRegions();
        Root.Loaded += (_, _) => UpdateTitleBarRegions();
        Root.KeyboardAccelerators.Add(BackAccelerator());
        // Botão "voltar" lateral do mouse
        Root.PointerPressed += (_, e) => { if (e.GetCurrentPoint(Root).Properties.IsXButton1Pressed) GoBack(); };
        Navigate("home");
    }

    Sidebar Nav;

    Sidebar NewSidebar()
    {
        var bar = new Sidebar();
        bar.Navigate += key => Navigate(key);
        NavHost.Child = bar;
        return bar;
    }

    /// <summary>Cores da barra de título e o selo de administrador, no tema e idioma atuais.</summary>
    void ApplyChrome()
    {
        Root.RequestedTheme = Theme.Element;
        ToolTipService.SetToolTip(BackButton, L.T("Voltar (Alt+Seta para a esquerda)"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(BackButton, L.T("Voltar"));
        var tb = AppWindow.TitleBar;
        tb.ButtonBackgroundColor = Colors.Transparent;
        tb.ButtonInactiveBackgroundColor = Colors.Transparent;
        tb.ButtonForegroundColor = UI.Color("#c9d6ee");
        tb.ButtonInactiveForegroundColor = UI.Color("#5f7398");
        tb.ButtonHoverBackgroundColor = UI.Color("#1a2236");
        tb.ButtonHoverForegroundColor = UI.Color("#ffffff");
        tb.ButtonPressedBackgroundColor = UI.Color("#22304a");
        tb.ButtonPressedForegroundColor = UI.Color("#ffffff");

        if (Sys.IsAdmin)
        {
            AdminText.Text = L.T("Administrador");
            return;
        }
        AdminText.Text = L.T("Sem administrador");
        AdminText.Foreground = UI.Warn;
        AdminIcon.Foreground = UI.Warn;
        AdminBadge.Background = UI.Brush("#1d1608");
        AdminBadge.BorderBrush = UI.Brush("#5c4413");
        ToolTipService.SetToolTip(AdminBadge, L.T("Abra o WINbackup como administrador para salvar drivers e arquivos protegidos."));
    }

    /// <summary>Refaz o menu e a tela atual depois de trocar o tema ou o idioma.</summary>
    public void Reload()
    {
        ApplyChrome();
        Nav = NewSidebar();
        Nav.Active = _current is "progress" or "report" or null ? "home" : _current;
        if (_current != null) Navigate(_current, null, back: true);
    }

    public void Navigate(string key, object? parameter = null) => Navigate(key, parameter, back: false);

    void Navigate(string key, object? parameter, bool back)
    {
        if (!Pages.TryGetValue(key, out var type)) return;
        // Em andamento e Relatório são passageiras: voltar nunca leva de novo a elas
        if (!back && _current != null && _current != key && _current is not ("progress" or "report"))
        {
            _history.Push(_current);
        }
        _current = key;
        Nav.Active = key is "progress" or "report" ? "home" : key;
        ContentFrame.Navigate(type, parameter, new SuppressNavigationTransitionInfo());
        ContentFrame.BackStack.Clear();
        BackButton.Visibility = _history.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public void GoBack()
    {
        // Pula telas repetidas em sequência
        while (_history.Count > 0)
        {
            var key = _history.Pop();
            if (key == _current) continue;
            Navigate(key, null, back: true);
            return;
        }
    }

    void OnBack(object sender, RoutedEventArgs e) => GoBack();

    KeyboardAccelerator BackAccelerator()
    {
        var k = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Left, Modifiers = Windows.System.VirtualKeyModifiers.Menu };
        k.Invoked += (_, e) => { GoBack(); e.Handled = true; };
        return k;
    }

    /// <summary>A barra de título arrasta a janela; o botão Voltar precisa ser liberado para receber cliques.</summary>
    void UpdateTitleBarRegions()
    {
        if (Root.XamlRoot == null) return;
        var source = InputNonClientPointerSource.GetForWindowId(AppWindow.Id);
        if (BackButton.Visibility != Visibility.Visible || BackButton.ActualWidth == 0)
        {
            source.ClearRegionRects(NonClientRegionKind.Passthrough);
            return;
        }
        var scale = Root.XamlRoot.RasterizationScale;
        var p = BackButton.TransformToVisual(Root).TransformPoint(new Windows.Foundation.Point(0, 0));
        source.SetRegionRects(NonClientRegionKind.Passthrough,
            [new RectInt32((int)(p.X * scale), (int)(p.Y * scale), (int)(BackButton.ActualWidth * scale), (int)(BackButton.ActualHeight * scale))]);
    }

    /// <summary>Começa uma tarefa e abre a tela Em andamento.</summary>
    public void Run(JobSpec spec)
    {
        if (BackupRunner.Current is { IsFinished: false })
        {
            Navigate("progress");
            return;
        }
        BackupRunner.Start(spec);
        Navigate("progress");
    }

    public void Minimize()
    {
        if (AppWindow.Presenter is OverlappedPresenter p) p.Minimize();
    }

    public void BringToFront()
    {
        AppWindow.Show();
        Activate();
    }

    void SizeAndCenter()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var scale = GetDpiForWindow(hwnd) / 96.0;
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var w = Math.Min((int)(1280 * scale), area.Width);
        var h = Math.Min((int)(800 * scale), area.Height);
        AppWindow.MoveAndResize(new RectInt32(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h));
        if (AppWindow.Presenter is OverlappedPresenter p)
        {
            p.PreferredMinimumWidth = (int)(1040 * scale);
            p.PreferredMinimumHeight = (int)(640 * scale);
        }
    }

    async void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_closeConfirmed || BackupRunner.Current is not { IsFinished: false } runner) return;
        args.Cancel = true;
        var ok = await Dialogs.ConfirmAsync(Root.XamlRoot, L.T("Cancelar e sair?"),
            L.T("Ainda há uma tarefa em andamento. Se sair agora, ela será cancelada e a versão ficará incompleta."),
            L.T("Cancelar e sair"), L.T("Continuar"), danger: true);
        if (!ok) return;
        runner.Cancel();
        _closeConfirmed = true;
        Close();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern uint GetDpiForWindow(IntPtr hwnd);
}
