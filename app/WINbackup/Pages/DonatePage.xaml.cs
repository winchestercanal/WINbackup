using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using QRCoder;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using WINbackup.Controls;
using WINbackup.Services;

namespace WINbackup.Pages;

/// <summary>Área de doação: Pix com QR Code e valor sugerido, e LivePix.</summary>
public sealed partial class DonatePage : Page
{
    static readonly decimal?[] Suggested = [5, 10, 25, 50, null];
    decimal? _amount;

    public DonatePage()
    {
        InitializeComponent();
        Responsive.StackWhenNarrow(MainGrid, 820);

        ReminderToggle.IsOn = !Settings.Data.DonationReminderOff;
        ReminderToggle.Toggled += (_, on) => { Settings.Data.DonationReminderOff = !on; Settings.Save(); };

        LivePixButton.IsEnabled = Donation.LivePixReady;
        LivePixSoon.Visibility = Donation.LivePixReady ? Visibility.Collapsed : Visibility.Visible;

        if (!Donation.PixReady)
        {
            PixPanel.Visibility = Visibility.Collapsed;
            PixSoon.Visibility = Visibility.Visible;
            return;
        }

        foreach (var value in Suggested)
        {
            var rb = new RadioButton
            {
                Style = (Style)Application.Current.Resources["FilterRadio"], Height = 34, Padding = new Thickness(16, 0, 16, 0), GroupName = "amount",
                Content = value is { } v ? $"R$ {v:0}" : "Valor livre", IsChecked = value == null,
            };
            rb.Checked += (_, _) => { _amount = value; Render(); };
            Amounts.Children.Add(rb);
        }
        CopyKeyButton.Content = $"Copiar só a chave: {Donation.PixKey}";
        Render();
    }

    void Render()
    {
        AmountText.Text = _amount is { } v ? v.ToString("C0", CultureInfo.GetCultureInfo("pt-BR")) : "Valor livre";
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(Donation.PixPayload(_amount), QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(10);
        _ = ShowAsync(png);
    }

    async Task ShowAsync(byte[] png)
    {
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(png.AsBuffer());
        stream.Seek(0);
        var bmp = new BitmapImage();
        await bmp.SetSourceAsync(stream);
        QrImage.Source = bmp;
    }

    static void Copy(string text)
    {
        var dp = new DataPackage();
        dp.SetText(text);
        Clipboard.SetContent(dp);
    }

    void OnCopyCode(object sender, RoutedEventArgs e)
    {
        Copy(Donation.PixPayload(_amount));
        CopyCodeButton.Content = "Copiado! Cole no app do banco";
    }

    void OnCopyKey(object sender, RoutedEventArgs e)
    {
        Copy(Donation.PixKey);
        CopyKeyButton.Content = "Chave copiada";
    }

    void OnLivePix(object sender, RoutedEventArgs e) => Sys.Open(Donation.LivePixUrl);
    void OnYouTube(object sender, RoutedEventArgs e) => Sys.Open("https://www.youtube.com/WINchesterCanal");
}
