# WINbackup

App gratuito para Windows do **[Canal WINchester](https://www.youtube.com/WINchesterCanal)** para fazer backup antes de formatar e deixar o PC como estava depois.

> **Status: 0.1 beta (testes).** Já funcionam de verdade: backup de Arquivos pessoais, Criadores e Drivers. As outras telas mostram como os módulos vão funcionar e entram nas próximas versões.

## O que ele faz

| Módulo | Situação |
|---|---|
| **Arquivos pessoais** | Documentos, Imagens, Área de Trabalho, Downloads e pastas que você escolher. Detecta pastas no OneDrive, copia só o que mudou (incremental), usa Shadow Copy para arquivos em uso e confere tudo com SHA-256 |
| **Criadores** | Presets, workspaces, scripts e plugins de Premiere, After Effects, Photoshop, Illustrator, Lightroom, DaVinci Resolve, OBS, Stream Deck, Voicemeeter, Blender e outros |
| **Drivers** | Exporta os drivers de terceiros com PnPUtil e reinstala depois |
| Restaurar, Programas (winget), Configurações, Navegadores, Saves de jogos, Agendamentos | Em desenvolvimento |

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
