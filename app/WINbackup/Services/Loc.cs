using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.UI.Xaml.Markup;

namespace WINbackup.Services;

/// <summary>
/// Idiomas do app. O texto em português é a chave: L.T("Iniciar backup") devolve a tradução do idioma
/// atual (Strings\en.json, Strings\es.json) ou o próprio texto quando não há tradução.
/// Nomes que viram pastas ou ficam gravados no manifesto continuam em português e só são traduzidos na tela.
/// </summary>
public static class L
{
    public static readonly (string Code, string Name)[] Languages = [("pt", "Português"), ("en", "English"), ("es", "Español")];

    static Dictionary<string, string> _map = new();

    /// <summary>Código do idioma em uso: "pt", "en" ou "es".</summary>
    public static string Current { get; private set; } = "pt";

    public static CultureInfo Culture { get; private set; } = new("pt-BR");

    /// <summary>Idioma do Windows, quando é um dos suportados; senão inglês.</summary>
    public static string SystemLanguage => CultureInfo.InstalledUICulture.TwoLetterISOLanguageName switch
    {
        "pt" => "pt",
        "es" => "es",
        _ => "en",
    };

    public static void Apply(string setting)
    {
        var code = setting is "pt" or "en" or "es" ? setting : SystemLanguage;
        Current = code;
        Culture = new CultureInfo(code switch { "en" => "en-US", "es" => "es-ES", _ => "pt-BR" });
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = Culture;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = Culture;
        _map = code == "pt" ? new() : Load(code);
    }

    static Dictionary<string, string> Load(string code)
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"WINbackup.Strings.{code}.json");
            if (stream == null) return new();
            return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new();
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            return new();
        }
    }

    /// <summary>Traduz um texto fixo.</summary>
    public static string T(string pt) => _map.TryGetValue(pt, out var t) && t.Length > 0 ? t : pt;

    /// <summary>Traduz um texto com lacunas {0}, {1}… e preenche no formato do idioma.</summary>
    public static string F(string pt, params object?[] args) => string.Format(Culture, T(pt), args);

    /// <summary>Escolhe entre singular e plural: L.N(n, "{0} arquivo", "{0} arquivos").</summary>
    public static string N(long n, string one, string many) => F(n == 1 ? one : many, n);
}

/// <summary>Texto traduzido no XAML: Text="{s:T K='Iniciar backup'}".</summary>
[MarkupExtensionReturnType(ReturnType = typeof(string))]
public sealed class T : MarkupExtension
{
    public string K { get; set; } = "";
    protected override object ProvideValue() => L.T(K);
}
