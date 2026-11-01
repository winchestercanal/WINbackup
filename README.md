# WINbackup

App gratuito para Windows do **[Canal WINchester](https://www.youtube.com/WINchesterCanal)** para fazer backup antes de formatar e deixar o PC como estava depois.

> **Status: 0.1 beta (testes).** Todos os módulos funcionam: backup, restauração, pós-formatação, agendamentos e linha de comando. Falta só a criptografia dos backups.

## O que ele faz

| Módulo | Situação |
|---|---|
| **Arquivos pessoais** | Documentos, Imagens, Área de Trabalho, Downloads e pastas que você escolher. Detecta pastas no OneDrive, copia só o que mudou (incremental), usa Shadow Copy para arquivos em uso e confere tudo com SHA-256 |
| **Criadores** | Presets, workspaces, scripts e plugins de Premiere, After Effects, Photoshop, Illustrator, Lightroom, DaVinci Resolve, OBS, Stream Deck, Voicemeeter, Blender e outros |
| **Programas** | Lista o que está instalado via winget, salva a lista para reinstalar tudo sozinho depois de formatar e gera uma lista dos que precisam ser instalados à mão |
| **Navegadores** | Favoritos, configurações e perfis de Edge, Chrome, Brave, Vivaldi, Opera e Firefox, mais a lista de extensões com link da loja. Senhas: exportação guiada, o app nunca as lê |
| **Configurações** | Redes Wi-Fi, tema e papel de parede, impressoras, fontes, variáveis de ambiente e hosts, mais configurações de Terminal, PowerShell, VS Code, Git e outros |
| **Saves de jogos** | Detecta saves em My Games, Saved Games, jogos Unreal e Unity, Steam Cloud e Ubisoft |
| **Drivers** | Exporta os drivers de terceiros com PnPUtil e reinstala depois |
| **Restaurar** | Escolhe a versão e o que trazer de volta (pastas ou arquivos soltos), para o lugar original, outro usuário ou outra pasta. O modo Pós-formatação restaura drivers, configurações e arquivos na ordem certa |
| **Agendamentos** | Backups automáticos por horário, ao conectar o HD externo ou ao entrar no Windows, pelo Agendador de Tarefas |
| **Linha de comando** | `WINbackup.exe --preset completo --dest E:\Backup`, `--only apps,configs`, `--restore E:\Backup --postformat --silent` |
| Criptografia dos backups | Em desenvolvimento |

O backup fica em pastas comuns no destino que você escolher (HD externo, outro disco ou rede), com um `manifest.json` e um log por execução. Nada é enviado para a internet.

## Requisitos

- Windows 10 (2004) ou 11, 64 bits
- Executar como administrador (necessário para drivers e Shadow Copy)

## Compilar

Requer o SDK do .NET 10.

```bash
dotnet publish -c Release -r win-x64 --self-contained true -o publish/WINbackup app/WINbackup/WINbackup.csproj
```

Detalhes da arquitetura, do formato do backup e de como testar sem UAC em [`app/README.md`](app/README.md). O protótipo da interface e as decisões de design estão em [`interface/HANDOFF.md`](interface/HANDOFF.md).

## Apoie

O WINbackup é gratuito e sem anúncios. Se ele te ajudou, apoie pelo [LivePix](https://livepix.gg/canalwinchester) ou com Pix para `winchestercanal@gmail.com`.
