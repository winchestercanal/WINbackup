# WINbackup — Handoff da interface

Projeto do **Canal WINchester**. App de Windows para backup e restauração de arquivos pessoais, programas, configurações, saves de jogos, drivers e navegadores.

- Script original (lógica de referência): https://github.com/winchestercanal/WINbackup-Script
- Protótipo interativo (canvas no Claude): https://claude.ai/artifact/WEQMQtFzzSsnVSK3vcbo43
- Esta pasta: o código-fonte de cada tela do protótipo (`telas/*.dc.html`). São arquivos de um formato de protótipo (HTML + um bloco `<script type="text/x-dc">` com a lógica). Eles **não abrem sozinhos no navegador**: use como referência exata de layout, textos, cores, estados e comportamento. Para ver funcionando, abra o link do protótipo.

---

## 1. Decisões já tomadas (não mudar sem perguntar)

- Interface **simples por padrão**: o Início só tem "Iniciar backup", "Restaurar backup" e os presets. Detalhes ficam nas telas do menu.
- Menu lateral com **categorias recolhíveis, recolhidas por padrão**. A categoria da tela atual abre sozinha.
- **Criptografia desativada por padrão** em todos os modos. Continua disponível em Opções.
- **Um único destino de backup**, escolhido no cartão "Destino dos backups" do menu lateral. Todas as telas de backup usam esse destino e só o exibem ("Salvando em …").
- "Iniciar backup" no Início **pede confirmação** em um pop-up antes de começar.
- Opção **"Não reiniciar o computador"** na restauração: desligada por padrão. Ao ligar, abre um aviso de risco e só confirma depois de marcar "Entendo os riscos… por minha conta". Os drivers são instalados mesmo assim; só o reinício automático é pulado.
- Senhas de navegador **nunca são lidas pelo app**: exportação guiada (abre a página certa de cada navegador).
- Requer **execução como Administrador** (badge na barra de título).

## 2. Identidade visual (Canal WINchester)

Escuro, destaques em azul e um pouco de neon.

| Token | Valor | Uso |
|---|---|---|
| bg-window | `#0a0f1c` | fundo da janela |
| bg-sidebar / titlebar | `#070b15` | navegação e barra de título |
| surface | `#0e1526` | cards |
| surface-strong | `#0e1830` | card em destaque / hover |
| border | `#1c2a48` | bordas padrão |
| border-strong | `#22407a` / `#2a3b63` | bordas de seleção / botões secundários |
| blue | `#2f7bff` | azul principal, toggles ligados |
| neon | `#19e3ff` | ação primária, seleção, hover dos botões principais (texto em cima: `#04121a`) |
| text | `#e8f0ff` | texto |
| text-muted | `#8ea3c7` / `#a9badb` | textos secundários |
| ok | `#3ddc97` | sucesso |
| warn | `#ffb547` (fundo `#1d1608`, borda `#5c4413`) | avisos |
| error | `#ff3d5a` / `#ff6b81` | erros; tutorial usa texto `#ff5c6c` |

- Fontes: **Segoe UI Variable** (UI), **Cascadia Mono / Consolas** (caminhos, comandos, logs).
- Raios: 6–8 px (botões/itens), 10–12 px (cards), 16 px (botões grandes do Início).
- Hover: botões grandes do Início passam do azul neutro (`#0e1526` + borda `#2f7bff`) para o neon (`#0e1830` + borda `#19e3ff`, círculo do ícone neon). Demais botões clareiam levemente.
- Estrutura nativa do Windows 11: barra de título própria com minimizar/maximizar/fechar, navegação lateral com indicador vertical na seção ativa.

## 3. Navegação

```
Início
Backup ▸        Arquivos pessoais · Programas · Configurações · Saves de jogos · Drivers · Navegadores
Restaurar ▸     Restaurar backup · Pós-formatação
Painel ▸        Atividade · Agendamentos · Histórico e versões
—
[Destino dos backups]  (cartão clicável → lista de destinos + "Procurar outra pasta…")
Opções
Tutorial no YouTube    (texto vermelho; link do vídeo ainda a definir)
```

## 4. Telas (arquivo → função)

| Arquivo | Tela | Pontos principais |
|---|---|---|
| `Main.dc.html` | Início | 2 botões grandes; presets (Backup completo, Só documentos, Ambiente de dev, Gamer) com descrição; confirmação antes de iniciar |
| `Files.dc.html` | Arquivos pessoais | modo Completo/Seletivo; 7 pastas padrão + pastas personalizadas; **detecção de pastas no OneDrive** (Known Folder Move) e opção de baixar arquivos só na nuvem; exclusões (node_modules, *.tmp…); estimativa de tamanho x espaço livre; Shadow Copy (VSS), incremental, preservar ACLs |
| `Apps.dc.html` | Programas | lista via `winget export` com origem (winget / Microsoft Store / sem pacote); filtros; **Exportar lista** e **Importar lista e instalar** (só instala a partir de um arquivo.json) |
| `Configs.dc.html` | Configurações | Windows: Wi-Fi (com senha), tema, impressoras, fontes, variáveis, hosts. Apps: Terminal, PowerShell, VS Code, Git, OBS, Streamer.bot, Komorebi, YASB; "Adicionar pasta de configuração" |
| `Games.dc.html` | Saves de jogos | Documents\My Games, Saved Games, AppData; nuvem x local; caminhos das bibliotecas Steam; botões pequenos **Só locais / Só na nuvem / Todos** que alternam (1º clique marca o grupo, 2º desmarca só aquele grupo) |
| `Drivers.dc.html` | Drivers | Backup (DISM) e Restaurar (PnPUtil, aceita ZIP ou pasta .inf); filtro só de terceiros; coluna "Windows Update encontra sozinho?"; botões alternáveis **Fora do Windows Update / No Windows Update / Todos** |
| `Browsers.dc.html` | Navegadores | Edge, Chrome, Firefox, Brave, Opera, Vivaldi; **perfis listados por nome, conta e pasta** (Default, Profile 1…) com seleção individual; favoritos, extensões, Speed Dial (Vivaldi); exportação guiada de senhas |
| `Restore.dc.html` | Restaurar backup | versões do backup; senha só se a versão for criptografada; árvore de conteúdo com seleção; conflitos (Pular / Substituir / Manter os dois); restaurar para usuário atual, outro usuário ou outra pasta; opção "Não reiniciar" + pop-up de risco |
| `Wizard.dc.html` | Pós-formatação | 6 etapas em ordem: Drivers (Wi-Fi primeiro) → Programas → Config. do Windows → Config. de apps → Arquivos e saves → Navegadores; cada etapa liga/desliga; "Não reiniciar" (padrão desligado) + pop-up; "Sem perguntas"; "Avisar quando terminar" |
| `Activity.dc.html` | Atividade | último backup, próximo agendado, status do destino; automações ativas; atividade recente |
| `Schedule.dc.html` | Agendamentos | gatilhos: horário (dias + hora), **ao conectar disco** (reconhecido pelo ID do volume, não pela letra), ao entrar no Windows; presets e seu nome de CLI; acordar o PC, só na tomada, incremental |
| `History.dc.html` | Histórico e versões | versões com integridade (SHA-256) e proteção; retenção "manter as últimas N" (o último completo nunca é apagado); logs com filtro |
| `Options.dc.html` | Opções | criptografia 7z AES-256 (desligada), verificação SHA-256, notificações (fim, erros, lembrete 30 dias), linha de comando, autounattend (`FirstLogonCommands`), modo portátil, bandeja do sistema |
| `Progress.dc.html` | Em andamento | % geral, arquivo atual, velocidade, tempo restante; etapas (VSS → Programas → Configs → Arquivos → Jogos → Drivers → Navegadores → Compactar → Verificar SHA-256); pausar/continuar, cancelar |
| `Report.dc.html` | Relatório final | resumo, por módulo, avisos (programas sem pacote, senhas pendentes); exportar relatório HTML; **notificação toast do Windows** |
| `Sidebar.dc.html`, `TitleBar.dc.html` | Componentes | navegação e barra de título usadas por todas as telas |

Textos entre colchetes (`[TAMANHO]`, `[DATA]`, `[N]`…) são espaços reservados para dados reais.

## 5. Linha de comando prevista

```
WINbackup.exe --preset completo --dest E:\Backup
WINbackup.exe --only apps,configs --dest E:\Backup
WINbackup.exe --restore E:\Backup --postformat --silent
```

## 6. Mapeamento com o script atual

| Módulo | Script hoje | Evolução prevista na interface |
|---|---|---|
| Arquivos | `robocopy /E /R:2 /W:3` por pasta | + VSS, incremental, exclusões, OneDrive, pastas personalizadas |
| Drivers | `dism /online /export-driver` + `Compress-Archive`; restore `pnputil /add-driver /install` | + filtro de terceiros/Windows Update, seleção, opção sem reinício |
| Navegadores | copia `Bookmarks` (Default) do Edge/Chrome em ZIP | + todos os perfis, mais navegadores, extensões, Speed Dial |
| Logs | `backup_audit_<data>.log` | + histórico de versões, filtros, relatório final |
| Novos | — | Programas (winget), Configurações, Saves de jogos, agendamento, CLI, portátil |

## 7. Sugestão de stack (decidir antes de começar)

- **WinUI 3 + .NET 8 (C#)**: visual nativo do Windows 11 (Mica, barra de título, toasts, Agendador de Tarefas, VSS via APIs). Melhor para um app "oficial" do canal.
- **Tauri + front em HTML/CSS**: reaproveita quase direto o layout do protótipo; backend em Rust chamando robocopy/dism/winget.
- **Electron**: mais simples para web, porém mais pesado.

Em qualquer caso: manifest pedindo administrador, operações longas em segundo plano com progresso e cancelamento, e um log por execução.

## 8. Pendências

- Link do vídeo tutorial no YouTube.
- Confirmar o comportamento do botão "Restaurar backup" do Início (hoje abre a Pós-formatação; decidir se terá confirmação também).
- Definir formato do pacote de backup (pasta por data + manifesto JSON é o sugerido).
