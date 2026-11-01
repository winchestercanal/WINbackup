using System.Globalization;
using System.Text;

namespace WINbackup.Services;

/// <summary>
/// Dados de doação do Canal WINchester e o código Pix "copia e cola" (BR Code estático do Banco Central).
/// Preencha PixKey, PixName, PixCity e LivePixUrl; enquanto vazios, a tela mostra "em breve".
/// </summary>
public static class Donation
{
    public const string PixKey = "winchestercanal@gmail.com";
    public const string PixName = "Canal WINchester"; // nome do recebedor, como aparece no app do banco (até 25 letras)
    public const string PixCity = "São Paulo";        // cidade do recebedor (até 15 letras)
    public const string LivePixUrl = "https://livepix.gg/canalwinchester"; // ex.: https://livepix.gg/seucanal

    public static bool PixReady => PixKey.Length > 0 && PixName.Length > 0 && PixCity.Length > 0;
    public static bool LivePixReady => LivePixUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    /// <summary>Lembrete a cada N backups bem-sucedidos.</summary>
    public const int ReminderEvery = 5;

    /// <summary>Conta um backup que deu certo e diz se é hora do lembrete.</summary>
    public static bool CountSuccessAndCheckReminder()
    {
        var s = Settings.Data;
        s.SuccessfulBackups++;
        Settings.Save();
        return !s.DonationReminderOff && (PixReady || LivePixReady) && s.SuccessfulBackups % ReminderEvery == 0;
    }

    /// <summary>Payload EMV do Pix. Sem valor, quem paga digita o valor no banco.</summary>
    public static string PixPayload(decimal? amount)
    {
        static string F(string id, string value) => id + value.Length.ToString("00") + value;

        var account = F("00", "br.gov.bcb.pix") + F("01", PixKey);
        var sb = new StringBuilder()
            .Append(F("00", "01"))
            .Append(F("26", account))
            .Append(F("52", "0000"))
            .Append(F("53", "986"));
        if (amount is > 0) sb.Append(F("54", amount.Value.ToString("0.00", CultureInfo.InvariantCulture)));
        sb.Append(F("58", "BR"))
          .Append(F("59", Clean(PixName, 25)))
          .Append(F("60", Clean(PixCity, 15)))
          .Append(F("62", F("05", "WINBACKUP")))
          .Append("6304");
        var payload = sb.ToString();
        return payload + Crc16(payload).ToString("X4");
    }

    /// <summary>Sem acentos e em maiúsculas, como os bancos esperam no BR Code.</summary>
    static string Clean(string s, int max)
    {
        var plain = new string(s.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).ToUpperInvariant();
        return plain.Length > max ? plain[..max] : plain;
    }

    /// <summary>CRC16-CCITT (polinômio 0x1021, início 0xFFFF), exigido no campo 63.</summary>
    static ushort Crc16(string data)
    {
        ushort crc = 0xFFFF;
        foreach (var b in Encoding.UTF8.GetBytes(data))
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc;
    }
}
