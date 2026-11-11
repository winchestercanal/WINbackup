<p align="center">
  <img src="docs/img/banner.svg" alt="WINbackup: seus dados, a salvo" width="100%">
</p>

<p align="center">
  <a href="https://github.com/winchestercanal/WINbackup/releases"><img src="https://img.shields.io/github/v/release/winchestercanal/WINbackup?include_prereleases&label=vers%C3%A3o&color=19e3ff&labelColor=0a0f1c&style=for-the-badge" alt="Versão"></a>
  <a href="https://github.com/winchestercanal/WINbackup/releases"><img src="https://img.shields.io/github/downloads/winchestercanal/WINbackup/total?label=downloads&color=2f7bff&labelColor=0a0f1c&style=for-the-badge" alt="Downloads"></a>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-2f7bff?labelColor=0a0f1c&style=for-the-badge" alt="Windows 10 e 11">
  <img src="https://img.shields.io/badge/feito%20com-IA%20(Claude)-d97757?logo=claude&logoColor=white&labelColor=0a0f1c&style=for-the-badge" alt="Feito com IA (Claude)">
</p>

<p align="center">
  <a href="#-baixar-e-testar"><b>Baixar</b></a> ·
  <a href="#-o-que-ele-salva"><b>O que ele salva</b></a> ·
  <a href="#-telas"><b>Telas</b></a> ·
  <a href="#-feedback"><b>Feedback</b></a> ·
  <a href="#-apoie"><b>Apoie</b></a>
</p>

**WINbackup** é um app gratuito para Windows, do **[Canal WINchester](https://www.youtube.com/WINchesterCanal)**, que faz backup de tudo o que importa antes de formatar e deixa o PC como estava depois: arquivos, programas, configurações, saves de jogos, drivers, navegadores e os presets dos seus apps de criação.

<p align="center"><b>Abra, faça o backup e depois restaure. Simples, rápido e eficiente.</b></p>

### 💡 Por que o WINbackup existe

> Durante anos testei inúmeras ferramentas de backup. Todas precisavam de muitos ajustes e nenhuma entregava tudo o que eu precisava, o que me obrigava a usar várias ferramentas para um único propósito.
>
> O WINbackup nasceu para transformar a tarefa cansativa de fazer e restaurar backups de arquivos, programas e drivers em algo **simples, rápido e eficiente**. Você abre, executa o backup e depois restaura.
>
> **Sem configurações complicadas, sem escolhas entre termos difíceis e sem a chance de esquecer alguma coisa e só lembrar quando já é tarde demais.**
>
> — *Canal WINchester*

> [!IMPORTANT]
> 🤖 **Este aplicativo foi criado com inteligência artificial.** O código foi escrito pelo **Claude**, a IA da Anthropic, a partir do protótipo e das decisões do Canal WINchester, que orientou, revisou e testou cada etapa.

> [!WARNING]
> **Versão 0.1 beta, de testes.** Todos os módulos funcionam, mas ainda estamos validando em PCs diferentes. Enquanto testa, **mantenha uma cópia extra dos seus arquivos importantes** e conte pra gente o que encontrar.

<p align="center">
  <img src="docs/img/presets.gif" alt="Tela inicial do WINbackup alternando entre os presets" width="90%">
</p>

## ✨ Por que o WINbackup

<table>
  <tr>
    <td width="33%" valign="top">
      <img src="docs/img/icons/shield.svg" width="44" alt=""><br>
      <b>Seguro de verdade</b><br>
      <sub>Confere cada arquivo com SHA-256, copia arquivos em uso pela Shadow Copy do Windows e nunca envia nada para a internet.</sub>
    </td>
    <td width="33%" valign="top">
      <img src="docs/img/icons/wizard.svg" width="44" alt=""><br>
      <b>Pós-formatação guiada</b><br>
      <sub>Depois de formatar, restaura drivers, programas, configurações e arquivos na ordem certa, com um clique.</sub>
    </td>
    <td width="33%" valign="top">
      <img src="docs/img/icons/drive.svg" width="44" alt=""><br>
      <b>Pastas comuns, sem prisão</b><br>
      <sub>O backup fica em pastas normais no HD externo, outro disco ou na rede. Dá para abrir e copiar sem o app.</sub>
    </td>
  </tr>
  <tr>
    <td valign="top">
      <img src="docs/img/icons/history.svg" width="44" alt=""><br>
      <b>Incremental com versões</b><br>
      <sub>Copia só o que mudou e mantém as últimas versões. Você escolhe de qual dia restaurar.</sub>
    </td>
    <td valign="top">
      <img src="docs/img/icons/schedule.svg" width="44" alt=""><br>
      <b>No automático</b><br>
      <sub>Agende por horário, ao conectar o HD externo ou ao entrar no Windows.</sub>
    </td>
    <td valign="top">
      <img src="docs/img/icons/download.svg" width="44" alt=""><br>
      <b>Portátil</b><br>
      <sub>Não precisa instalar. Roda direto da pasta ou de um pendrive.</sub>
    </td>
  </tr>
</table>

## 🔄 Como funciona

<table>
  <tr>
    <td align="center" width="33%">
      <img src="docs/img/icons/download.svg" width="56" alt=""><br>
      <b>1. Faça o backup</b><br>
      <sub>Escolha um preset (completo, documentos, dev, gamer ou criadores) e o destino.</sub>
    </td>
    <td align="center" width="33%">
      <img src="docs/img/icons/drive.svg" width="56" alt=""><br>
      <b>2. Formate o PC</b><br>
      <sub>Seus dados ficam guardados no HD externo, em pastas comuns.</sub>
    </td>
    <td align="center" width="33%">
      <img src="docs/img/icons/restore.svg" width="56" alt=""><br>
      <b>3. Restaure tudo</b><br>
      <sub>Abra o WINbackup no Windows novo e use o modo Pós-formatação.</sub>
    </td>
  </tr>
</table>

## 📦 O que ele salva

| Módulo | O que faz |
|---|---|
| <img src="docs/img/icons/files.svg" width="32" alt=""><br>**Arquivos pessoais** | Documentos, Imagens, Área de Trabalho, Downloads e as pastas que você escolher. Detecta pastas no OneDrive e mantém as pastas vazias |
| <img src="docs/img/icons/creators.svg" width="32" alt=""><br>**Criadores** | Presets, workspaces, scripts e plugins de Premiere, After Effects, Photoshop, Illustrator, Lightroom, DaVinci Resolve, OBS, Stream Deck, Voicemeeter, Blender e outros |
| <img src="docs/img/icons/apps.svg" width="32" alt=""><br>**Programas** | Lista o que está instalado via winget, reinstala tudo sozinho depois de formatar e gera a lista dos que precisam ser instalados à mão |
| <img src="docs/img/icons/browsers.svg" width="32" alt=""><br>**Navegadores** | Favoritos, configurações e perfis de Edge, Chrome, Brave, Vivaldi, Opera e Firefox, mais a lista de extensões com link da loja. Senhas: exportação guiada, o app nunca as lê |
| <img src="docs/img/icons/configs.svg" width="32" alt=""><br>**Configurações** | Redes Wi-Fi, tema e papel de parede, impressoras, fontes, variáveis de ambiente e hosts, mais Terminal, PowerShell, VS Code, Git e outros |
| <img src="docs/img/icons/games.svg" width="32" alt=""><br>**Saves de jogos** | My Games, Saved Games, jogos Unreal e Unity, Steam Cloud e Ubisoft |
| <img src="docs/img/icons/drivers.svg" width="32" alt=""><br>**Drivers** | Exporta os drivers de terceiros com PnPUtil e reinstala depois |
| <img src="docs/img/icons/restore.svg" width="32" alt=""><br>**Restaurar** | Escolha a versão e o que trazer de volta (pastas ou arquivos soltos), para o lugar original, outro usuário ou outra pasta |
| <img src="docs/img/icons/schedule.svg" width="32" alt=""><br>**Agendamentos** | Backups automáticos pelo Agendador de Tarefas do Windows |
| <img src="docs/img/icons/terminal.svg" width="32" alt=""><br>**Linha de comando** | Tudo também roda pelo terminal, para scripts e automações |
| <img src="docs/img/icons/configs.svg" width="32" alt=""><br>**Temas e idiomas** | Português, inglês e espanhol. Temas WINchester, escuro neutro, preto OLED e claro, em Opções |
| <img src="docs/img/icons/lock.svg" width="32" alt=""><br>Criptografia | *Em desenvolvimento* |

## 📸 Telas

<table>
  <tr>
    <td width="50%"><img src="docs/img/files.png" alt="Arquivos pessoais"><p align="center"><sub><b>Arquivos pessoais</b>: pastas do usuário, OneDrive, exclusões e Shadow Copy</sub></p></td>
    <td width="50%"><img src="docs/img/creators.png" alt="Criadores"><p align="center"><sub><b>Criadores</b>: presets e plugins de Adobe, DaVinci, OBS e outros</sub></p></td>
  </tr>
  <tr>
    <td><img src="docs/img/restore.png" alt="Restaurar backup"><p align="center"><sub><b>Restaurar</b>: escolha a versão e o que trazer de volta</sub></p></td>
    <td><img src="docs/img/wizard.png" alt="Modo pós-formatação"><p align="center"><sub><b>Pós-formatação</b>: deixa o PC como estava, na ordem certa</sub></p></td>
  </tr>
  <tr>
    <td colspan="2" align="center"><img src="docs/img/games.png" alt="Saves de jogos" width="50%"><p align="center"><sub><b>Saves de jogos</b>: encontra o progresso dos seus jogos</sub></p></td>
  </tr>
</table>

## 📥 Baixar e testar

<p align="center">
  <a href="https://github.com/winchestercanal/WINbackup/releases"><img src="https://img.shields.io/badge/Baixar%20o%20WINbackup-0.1%20beta-19e3ff?labelColor=0a0f1c&style=for-the-badge" alt="Baixar o WINbackup" height="40"></a>
</p>

1. Em **[Releases](https://github.com/winchestercanal/WINbackup/releases)**, baixe o `.zip` da versão mais recente.
2. Extraia numa pasta (por exemplo `C:\WINbackup` ou um pendrive). Não precisa instalar nada: o .NET e o Windows App SDK já vêm dentro.
3. Abra o `WINbackup.exe` e aceite o aviso de administrador.

> [!NOTE]
> O Windows pode mostrar **"O Windows protegeu o computador"** porque o app ainda não tem assinatura digital. Clique em **Mais informações → Executar assim mesmo**.

**Primeiro teste seguro:** escolha um destino vazio (pendrive ou pasta de teste), faça backup de uma pasta pequena e restaure em **Restaurar → Outra pasta**.

**Requisitos:** Windows 10 (2004) ou 11, 64 bits. Precisa rodar como administrador (para drivers e Shadow Copy).

## 💬 Feedback

Encontrou um erro ou tem uma sugestão? **[Abra uma issue](https://github.com/winchestercanal/WINbackup/issues/new)** contando:

- o que você fez e o que esperava acontecer;
- a versão do Windows;
- se possível, o log da execução (`backup_audit_….log` dentro da pasta `WINbackup_<data>` do backup, ou `%APPDATA%\WINbackup\logs`) e o arquivo `%APPDATA%\WINbackup\erros.log`.

## ⌨️ Linha de comando

```powershell
WINbackup.exe --preset completo --dest E:\Backup  # backup completo
WINbackup.exe --only apps,configs --silent        # só programas e configurações, sem janela
WINbackup.exe --restore E:\Backup --postformat    # restauração pós-formatação
```

O backup fica em pastas comuns no destino que você escolher, com um `manifest.json` e um log por execução. **Nada é enviado para a internet.**

<details>
<summary><b>🛠️ Compilar a partir do código</b></summary>

<br>

Requer o SDK do .NET 10.

```bash
dotnet publish -c Release -r win-x64 --self-contained true -o publish/WINbackup app/WINbackup/WINbackup.csproj
```

Feito em **WinUI 3** (Windows App SDK) e **.NET 10**. Detalhes da arquitetura, do formato do backup e de como testar sem UAC em [`app/README.md`](app/README.md). O protótipo da interface e as decisões de design estão em [`interface/HANDOFF.md`](interface/HANDOFF.md).

</details>

## 💙 Apoie

O WINbackup é gratuito e sem anúncios. Se ele te ajudou, considere apoiar o projeto:

<p>
  <a href="https://livepix.gg/canalwinchester"><img src="https://img.shields.io/badge/LivePix-apoiar-ff6b8a?labelColor=0a0f1c&style=for-the-badge" alt="Apoiar pelo LivePix"></a>
  <img src="https://img.shields.io/badge/Pix-winchestercanal%40gmail.com-3ddc97?labelColor=0a0f1c&style=for-the-badge" alt="Pix: winchestercanal@gmail.com">
  <a href="https://www.youtube.com/WINchesterCanal"><img src="https://img.shields.io/badge/YouTube-Canal%20WINchester-ff0000?logo=youtube&logoColor=white&labelColor=0a0f1c&style=for-the-badge" alt="Canal WINchester no YouTube"></a>
</p>

<p align="center"><sub>Feito com 🤖 IA e 💙 pelo Canal WINchester</sub></p>
