# CLAUDE.md — Claude Pet

Animal de compagnie de bureau pour Windows 11 (cahier des charges : `PROMPT_PET.md`, bilan : `RAPPORT.md`).
Langue de travail : français (code en anglais, commentaires en français). Git local seulement, auteur `Ixorak`.

## Construire et tester

- `.\build.ps1` (ou `build.bat`) : exe natif dans `out\app\ClaudePet.exe`, puis `dist\PetSetup.exe` (Inno Setup 6).
  `-NoSetup` : l'exe seulement. Compiler pour vérifier sans toucher à l'exe qui tourne :
  `dotnet build -c Release` dans `src\ClaudePet` (sorties dans `out\bin`).
- La compilation native a besoin de `vswhere.exe` dans le PATH (`build.ps1` l'ajoute).
- Lancer pour tester : `out\app\ClaudePet.exe --log --house PetDev` (journal : `%AppData%\Pet\journal.txt` ;
  la maison de test est le raccourci « PetDev » du bureau).
- Piloter l'instance qui tourne : `out\app\ClaudePet.exe --cmd "<commande>"` : `state`, `quit`, `goto x y`,
  `throw vx vy`, `sit 30`, `sleep`, `feed`, `feedat x y`, `hunger 3.5`, `eat`, `windows`, `fs <écran> on|off|auto`,
  `stats`, `debug tap|angry|grab|climb|climbwin|jumpwin|hopdown|tunnel`, `findicon`, `dumpui`.
  Tests avec un pointeur imaginaire (la vraie souris ne bouge pas) : `debug swingtest|swinglow|reachtest|pettest`.
- Une seule instance à la fois (mutex + fenêtre « ClaudePet.Controller ») : quitter avant de recompiler.

## Pièges connus

- **Antivirus** : ne jamais écrire dans la mémoire d'un autre processus (`VirtualAllocEx`/`WriteProcessMemory`),
  ne pas poser de crochet global (`SetWindowsHookEx`) : Bitdefender a mis Pet en quarantaine pour ça le
  31 octobre 2026. Les chemins nettoyés restent bloqués (`publish\ClaudePet.exe`, `src\ClaudePet\bin\…`,
  `Bureau\Pet.lnk`, `…\Roaming\Pet\log.txt`) : d'où `out\` et `journal.txt`.
- Lancé depuis l'appli Claude (empaquetée), Pet voit `%AppData%` redirigé vers
  `%LocalAppData%\Packages\Claude_…\LocalCache\Roaming`.
- Les modifications de fichiers peuvent passer en CRLF : `.gitattributes` normalise en LF à l'ajout.
- Les scripts `.ps1` avec accents doivent être en UTF-8 avec BOM (PowerShell 5.1), `build.bat` en CRLF,
  `installer\PetSetup.iss` en UTF-8 avec BOM.
- Ne rien modifier dans le registre pendant le développement (le démarrage auto est fait par l'installateur).
