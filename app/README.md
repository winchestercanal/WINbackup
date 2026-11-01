# WINbackup 0.1 (beta): app para Windows

App do **Canal WINchester** feito a partir do protótipo em [`../interface`](../interface/HANDOFF.md).
WinUI 3 + .NET 10 (C#), Windows App SDK 2.5, autocontido (não precisa instalar nada no PC formatado) e roda como Administrador.

## O que já funciona de verdade

| Tela | Situação |
|---|---|
| Início | Presets, confirmação antes de começar, último backup lido do destino |
| Arquivos pessoais | Pastas do usuário + pastas personalizadas, detecção do OneDrive, exclusões, estimativa de tamanho x espaço livre, **incremental**, **Shadow Copy (VSS)** para arquivos em uso, permissões (ACL), **SHA-256** |
| Criadores | Detecta Adobe (Premiere, After Effects, Photoshop, Illustrator, Lightroom, Media Encoder, .mogrt, painéis CEP), DaVinci Resolve, OBS, Stream Deck, Elgato, Streamer.bot, Voicemeeter, NVIDIA Broadcast, Reaper, Blender, Cinema 4D e outros; copia presets, workspaces, scripts, macros e, se ligado, as pastas de plugins (AE, MediaCore, OFX, Photoshop, OBS, VST). Catálogo em `Backup/CreatorApps.cs` |
| Apoie o projeto | Pix com QR Code e "copia e cola" (BR Code gerado no app) e LivePix; lembrete a cada 5 backups bem-sucedidos, com "não mostrar de novo" |
| Drivers | Lista real (WMI), exporta com `pnputil /export-driver` para a pasta `Drivers` (sem compactar); restaura da pasta, de outra pasta com .inf ou de `.zip` com `pnputil /add-driver /install` |
| Em andamento | Progresso, velocidade, tempo restante, etapas, log ao vivo, pausar/continuar, cancelar |
| Relatório | Resumo, por módulo, avisos, exportar HTML, abrir pasta, "Reiniciar agora" quando os drivers pedem |
| Histórico | Versões e logs reais do destino |
| Atividade | Último backup e destino reais |
| Opções | SHA-256 e notificações valem; o resto marcado "Em breve" |

**Prévia (layout fiel ao protótipo, dados de exemplo, botões avisam "Em breve")**: Programas, Configurações, Saves de jogos, Navegadores, Restaurar backup (arquivos), Pós-formatação, Agendamentos, criptografia, modo portátil, linha de comando.

Nos presets, as etapas que ainda não existem aparecem como "Em breve nesta versão" no progresso e no relatório. O preset Gamer ainda não tem nada executável, por isso o app avisa em vez de começar.

Navegação: botão **Voltar** na barra de título (também Alt+← e o botão lateral do mouse).

Dados de doação (Pix e LivePix) ficam em `Services/Donation.cs`.

## Formato do backup

```
<destino>\WINbackup_AAAA-MM-DD_HH-MM-SS\
  Arquivos\<Pasta>\...          cópias normais, abrem no Explorer (inclui pastas vazias)
  Criadores\<Programa>\...      presets, configs e plugins de cada programa
  Drivers\oemNN_<dispositivo>\  pacotes .inf exportados + drivers.json
  manifest.json                 tudo o que a versão contém
  backup_audit_<data>.log       log da execução
  relatorio.html                (se exportado)
```

No incremental, os arquivos sem mudança não são copiados de novo: no `manifest.json` eles apontam (`Stored`) para a versão onde estão guardados. Por isso uma versão incremental depende das anteriores; a retenção automática (ainda não implementada) vai ter de respeitar isso.

## Compilar e rodar

Requer o SDK do .NET 10 (`winget install Microsoft.DotNet.SDK.10`).

```bash
dotnet build -c Teste WINbackup/WINbackup.csproj
```

`-c Teste` gera uma versão **sem pedir administrador** em `WINbackup/bin/Teste/…`, só para mexer na interface (drivers e Shadow Copy não funcionam nela, e o app avisa com o selo "Sem administrador"). Para isolar dos seus dados, defina `WINBACKUP_DATA=<pasta>` antes de abrir: as configurações vão para essa pasta em vez de `%APPDATA%\WINbackup`.

Versão final (pede UAC, inclui .NET e Windows App SDK):

```bash
dotnet publish -c Release -r win-x64 --self-contained true -o publish/WINbackup WINbackup/WINbackup.csproj
```

Teste sempre o pacote publicado também (`dotnet publish -c Teste … -o <pasta>`): a compilação normal pode funcionar e o pacote não. `EnableMsixTooling` no projeto é o que coloca o `WINbackup.pri` com as telas no pacote.

## Organização do código

```
WINbackup/
  App.xaml, MainWindow.xaml     barra de título própria, menu lateral, navegação
  Themes/Styles.xaml            tokens de cor e estilos (botões, interruptor, linhas com seleção…)
  Controls/                     Icon (SVG do protótipo), Sidebar, ToggleRow, SaveTargetBar, Dialogs…
  Pages/                        uma página por tela do protótipo
  Backup/                       JobSpec/presets, BackupRunner (etapas, pausa, log), FilesModule, DriversModule, ShadowCopies, Manifest
  Services/                     Settings, Destinations, FileDialog (COM, funciona elevado), Notifier (toast via bandeja), Sys
```

## Diferenças em relação ao HANDOFF

- **.NET 10 no lugar do .NET 8**: o .NET 8 sai do suporte em novembro de 2026; o 10 é a LTS atual.
- **Cópia de arquivos em C# em vez de robocopy**: dá progresso por byte, hash SHA-256 durante a cópia e o incremental por manifesto.
- **"Windows Update encontra sozinho?"** é uma estimativa: drivers que já vêm com o Windows ou publicados pela Microsoft = "Sim"; o resto = "Não garantido: guarde este". Não existe uma forma confiável de saber de antemão.
- **Notificação**: o `AppNotificationManager` não funciona no modo autocontido, então o toast sai pelo ícone da bandeja (aparece igual no Windows 10/11).
- **Tutorial no YouTube** abre o canal enquanto o link do vídeo não é definido.
