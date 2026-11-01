using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using WINbackup.Models;

namespace WINbackup.Controls;

public static class Dialogs
{
    static Style S(string key) => (Style)Application.Current.Resources[key];

    static ContentDialog Base(XamlRoot root, string title, UIElement body, string primary, string? secondary, bool danger = false, bool warn = false)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        header.Children.Add(new Border
        {
            Width = 40, Height = 40, CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(warn || danger ? 1 : 1.5),
            Background = warn || danger ? UI.Brush("#2a1f0b") : UI.Brush("#1a2b52"),
            BorderBrush = warn || danger ? UI.Brush("#5c4413") : UI.Blue,
            Child = new Icon { Width = 20, Height = 20, StrokeThickness = 2.25, Data = warn || danger ? Icons.Warning : Icons.Download,
                Foreground = warn || danger ? UI.Warn : UI.Brush("#ffffff") },
        });
        header.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });

        var content = new StackPanel { Spacing = 18, Width = 392 };
        content.Children.Add(header);
        content.Children.Add(body);

        var d = new ContentDialog
        {
            XamlRoot = root,
            Content = content,
            PrimaryButtonText = primary,
            CloseButtonText = secondary ?? "",
            DefaultButton = ContentDialogButton.Primary,
            PrimaryButtonStyle = S(danger ? "DangerButton" : warn ? "WarnButton" : "PrimaryButton"),
            CloseButtonStyle = S("SecondaryButton"),
            RequestedTheme = ElementTheme.Dark,
        };
        if (warn || danger) d.BorderBrush = UI.Brush("#5c4413");
        return d;
    }

    public static TextBlock Para(string text, bool muted = false) => new()
    {
        Text = text, TextWrapping = TextWrapping.Wrap, FontSize = muted ? 13 : 14, LineHeight = muted ? 20 : 21,
        Foreground = muted ? UI.Muted2 : UI.Brush("#c9d6ee"),
    };

    public static async Task<bool> ConfirmAsync(XamlRoot root, string title, string text, string primary, string secondary, bool danger = false)
    {
        var d = Base(root, title, Para(text), primary, secondary, danger: danger);
        return await d.ShowAsync() == ContentDialogResult.Primary;
    }

    public static async Task<bool> ConfirmAsync(XamlRoot root, string title, UIElement body, string primary, string secondary)
    {
        var d = Base(root, title, body, primary, secondary);
        return await d.ShowAsync() == ContentDialogResult.Primary;
    }

    public static async Task InfoAsync(XamlRoot root, string title, string text)
    {
        var d = Base(root, title, Para(text, muted: true), "Entendi", null);
        await d.ShowAsync();
    }

    /// <summary>Lembrete ocasional de doação, com "não mostrar de novo".</summary>
    public static async Task<bool> DonationReminderAsync(XamlRoot root)
    {
        var never = new ToggleButton { Style = S("CheckCard"), Background = UI.Brush("#0a1120"), BorderBrush = UI.Brush("#2a3b63"),
            Content = new TextBlock { Text = "Não mostrar este lembrete de novo", FontSize = 13 } };
        var body = new StackPanel { Spacing = 14 };
        body.Children.Add(Para($"Você já fez {Services.Settings.Data.SuccessfulBackups} backups com o WINbackup. Ele é gratuito e feito pelo Canal WINchester; se está sendo útil, apoie com o valor que achar justo."));
        body.Children.Add(never);
        var d = Base(root, "Curtindo o WINbackup?", body, "Quero apoiar", "Agora não");
        var result = await d.ShowAsync();
        if (never.IsChecked == true)
        {
            Services.Settings.Data.DonationReminderOff = true;
            Services.Settings.Save();
        }
        return result == ContentDialogResult.Primary;
    }

    /// <summary>
    /// Pop-up de risco do "Não reiniciar o computador": só confirma depois de marcar
    /// "Entendo os riscos… por minha conta" (HANDOFF, decisões já tomadas).
    /// </summary>
    public static async Task<bool> NoRebootAsync(XamlRoot root)
    {
        var ack = new ToggleButton { Style = S("CheckCard"), Background = UI.Brush("#0a1120"), BorderBrush = UI.Brush("#2a3b63"),
            Content = new TextBlock { Text = "Entendo os riscos e quero continuar sem reiniciar, por minha conta.", FontSize = 13, TextWrapping = TextWrapping.Wrap } };
        var body = new StackPanel { Spacing = 16 };
        body.Children.Add(Para("Depois de instalar drivers, o Windows precisa reiniciar para carregá-los corretamente. Sem reiniciar, alguns dispositivos podem não funcionar, ter conflitos ou causar erros até o próximo reinício."));
        body.Children.Add(Para("Os drivers serão instalados normalmente. Só o reinício automático será pulado, e você reinicia quando quiser.", muted: true));
        body.Children.Add(ack);

        var d = Base(root, "Reiniciar é recomendado", body, "Continuar sem reiniciar", "Manter reinício", warn: true);
        d.DefaultButton = ContentDialogButton.Close;
        d.IsPrimaryButtonEnabled = false;
        ack.Checked += (_, _) => d.IsPrimaryButtonEnabled = true;
        ack.Unchecked += (_, _) => d.IsPrimaryButtonEnabled = false;
        return await d.ShowAsync() == ContentDialogResult.Primary;
    }
}
