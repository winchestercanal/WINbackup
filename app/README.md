# WINbackup 0.2 (beta): app para Windows

App do **Canal WINchester** feito a partir do protótipo em [`../interface`](../interface/HANDOFF.md).
WinUI 3 + .NET 10 (C#), Windows App SDK 2.5, autocontido (não precisa instalar nada no PC formatado) e roda como Administrador.

## O que já funciona de verdade

| Tela | Situação |
|---|---|
| Início | Presets, confirmação antes de começar, último backup lido do destino |
| Arquivos pessoais | Pastas do usuário + pastas personalizadas, detecção do OneDrive, exclusões, estimativa de tamanho x espaço livre, **incremental**, **Shadow Copy (VSS)** para arquivos em uso, permissões (ACL), **SHA-256** |
| Criadores | Detecta Adobe (Premiere, After Effects, Photoshop, Illustrator, Lightroom, Media Encoder, .mogrt, painéis CEP), DaVinci Resolve, OBS, Stream Deck, Elgato, Streamer.bot, Voicemeeter, NVIDIA Broadcast, Reaper, Blender, Cinema 4D e outros; copia presets, workspaces, scripts, macros e, se ligado, as pastas de plugins (AE, MediaCore, OFX, Photoshop, OBS, VST). Catálogo em `Backup/CreatorApps.cs` |
| Apoie o projeto | Pix com QR Code e "copia e cola" (BR Code gerado no app) e LivePix; lembrete a cada 5 backups bem-sucedidos, com "não mostrar de novo" |
| Programas | Lista real do `winget list` (winget, Microsoft Store, sem pacote); salva `Programas\Programas.json` (formato do `winget import`, só os marcados) e `Instalar_manualmente.html` com os sem pacote; reinstala um por um com `winget install` (os já instalados são pulados); "Exportar lista" e "Importar lista e instalar" avulsos |
| Navegadores | Perfis reais de Edge, Chrome, Brave, Vivaldi, Opera (via `Local State`) e Firefox (via `profiles.ini`); copia favoritos, configurações e notas (Chromium) ou favoritos, preferências e extensões (Firefox), mais a lista de perfis; gera `Navegadores\Extensoes.html` com link da loja de cada extensão; senhas só por exportação guiada (abre a página do navegador como usuário comum, via Explorer) e o .csv que o usuário adicionar |
| Configurações | Windows: Wi-Fi (`netsh wlan export`, com senha como administrador), tema e papel de parede, impressoras (PrintBrm), fontes do usuário, variáveis de ambiente e hosts, exportados para `Configuracoes\Windows` e reaplicados na restauração (reg import ajustando a pasta do usuário). Apps: Windows Terminal, PowerShell, VS Code (com lista de extensões), Git, Notepad++, PowerToys, Komorebi, YASB, AutoHotkey, Rainmeter |
| Saves de jogos | My Games, Saved Games, pastas de estúdios em Documentos, Unreal (`Saved\SaveGames`), Unity (`LocalLow`), Steam Cloud (nome pelo appmanifest) e Ubisoft; guarda as bibliotecas da Steam |
| Agendamentos | Tarefas no Agendador do Windows (`\WINbackup`): por horário, ao conectar o disco (evento Partition/Diagnostic 1006, disco reconhecido pelo ID do volume) ou ao entrar; acordar o PC, só na tomada, incremental |
| Linha de comando | `--preset`, `--only`, `--restore [--postformat] [--no-reboot]`, `--silent`, `--schedule`, `--remind`; um backup por vez (trava em `executando.lock`) |
| Opções | SHA-256, notificações, lembrete de 30 dias (ao abrir o app e, se ligado como administrador, ao entrar no Windows), modo portátil (`portable.txt` ao lado do .exe guarda tudo em `Dados\`) e "Criar versão portátil no pendrive" |
| Retenção | Mantém as últimas N versões de cada tipo de backup; nunca apaga o último completo nem versões das quais as mantidas dependem |
| Drivers | Lista real (WMI), exporta com `pnputil /export-driver` para a pasta `Drivers` (sem compactar); restaura da pasta, de outra pasta com .inf ou de `.zip` com `pnputil /add-driver /install` |
| Em andamento | Progresso, velocidade, tempo restante, etapas, log ao vivo, pausar/continuar, cancelar |
| Relatório | Resumo, por módulo, avisos, exportar HTML, abrir pasta, "Reiniciar agora" quando os drivers pedem |
| Histórico | Versões e logs reais do destino |
| Atividade | Último backup e destino reais |
| Opções | SHA-256 e notificações valem; o resto marcado "Em breve" |

| Restaurar backup | Versões reais do destino, conteúdo por grupo e pasta, busca de arquivos soltos; restaura para os locais originais (Documentos, Imagens etc. vão para onde essas pastas estão hoje, inclusive OneDrive), para outro usuário ou para outra pasta; Pular / Substituir / Manter os dois; confere SHA-256; drivers primeiro |
| Pós-formatação | Pega a versão mais recente de drivers, Criadores e arquivos e restaura nessa ordem. Configs de programas substituem as padrão; arquivos pessoais nunca são sobrescritos. Pede para reiniciar no fim se os drivers precisarem (a menos que "Não reiniciar" esteja ligado) |

**Ainda não implementado:** criptografia 7z AES-256 (aparece como "Em breve" em Opções).

Nos presets, as etapas que ainda não existem aparecem como "Em breve nesta versão" no progresso e no relatório. O preset Gamer ainda não tem nada executável, por isso o app avisa em vez de começar.

Navegação: botão **Voltar** na barra de título (também Alt+← e o botão lateral do mouse).

Dados de doação (Pix e LivePix) ficam em `Services/Donation.cs`.

## Formato do backup

```
<destino>\WINbackup_AAAA-MM-DD_HH-MM-SS\
  Arquivos\<Pasta>\...          cópias normais, abrem no Explorer (inclui pastas vazias)
  Criadores\<Programa>\...      presets, configs e plugins de cada programa
  Programas\Programas.json      lista para o winget import
  Programas\Instalar_manualmente.html   os sem pacote, com link de busca
  Configuracoes\Windows\         Wi-Fi, .reg do tema/fontes/variáveis, hosts, impressoras, papel de parede
  Configurações\<App>\  Saves de jogos\<Jogo>\  Navegadores\<Navegador>\   pastas copiadas
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
  Services/                     Settings, Destinations, FileDialog (COM, funciona elevado), Notifier (toast via bandeja), Sys,
                                Loc (idiomas), Theme (temas)
  Strings/en.json, es.json      traduções (texto em português → tradução), embutidas no .exe
```

## Idiomas e temas

- **Idiomas**: o texto em português é a chave. No C#, `L.T("Iniciar backup")`, `L.F("{0} pastas", n)` e `L.N(n, "1 pasta", "{0} pastas")`; no XAML, `Text="{s:T K='Iniciar backup'}"` (no XAML, `\` precisa ser escrito `\\`). Sem tradução, aparece o português. O idioma padrão segue o do Windows (pt, es; o resto vira inglês) e trocar em Opções reabre o app.
- **O que fica em português de propósito**: tudo que é gravado no backup ou nas configurações (nomes das pastas do backup, `Title`, `Kind`, `Modules` e grupos do manifesto, nome das tarefas no Agendador). Assim um backup feito em um idioma restaura em outro e a retenção continua agrupando as versões. Esses textos são traduzidos só na hora de mostrar (`L.T(m.Title)`, `Preset.Label`).
- **Temas**: as cores continuam escritas na paleta WINchester (Styles.xaml e `UI.Brush("#…")`); `Theme.cs` tem, para cada tema, a troca de cada cor. Os pincéis são compartilhados, então trocar entre os temas escuros é ao vivo. Entre claro e escuro o app reabre, porque o tema dos controles nativos (caixas de texto, rolagem) só pode ser escolhido antes de carregar o XAML.
- **Cor nova**: use uma cor que já existe na paleta ou acrescente a cor nova nas três tabelas de `Theme.cs`.


## Diferenças em relação ao HANDOFF

- **.NET 10 no lugar do .NET 8**: o .NET 8 sai do suporte em novembro de 2026; o 10 é a LTS atual.
- **Cópia de arquivos em C# em vez de robocopy**: dá progresso por byte, hash SHA-256 durante a cópia e o incremental por manifesto.
- **"Windows Update encontra sozinho?"** é uma estimativa: drivers que já vêm com o Windows ou publicados pela Microsoft = "Sim"; o resto = "Não garantido: guarde este". Não existe uma forma confiável de saber de antemão.
- **Notificação**: o `AppNotificationManager` não funciona no modo autocontido, então o toast sai pelo ícone da bandeja (aparece igual no Windows 10/11).
- **Tutorial no YouTube** abre o canal enquanto o link do vídeo não é definido.
