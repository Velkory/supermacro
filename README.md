# SuperMacroV1

Application Windows d'auto-clic avec panneau de configuration.

## Fonctions

- Vitesse réglable de 1 à 100 clics par seconde
- Clic gauche, droit ou milieu
- Modes **Bascule** et **Maintien**
- Raccourci personnalisable avec Ctrl, Alt et Maj
- Arrêt d'urgence global avec **F12**
- Démarrage automatique de la macro à l'ouverture
- Limitation optionnelle à Minecraft et Roblox
- Paramètres persistants
- Installateur Windows par utilisateur

## Télécharger l'installateur

Ouvrez l'onglet **Actions**, sélectionnez **Build Windows Installer**, puis téléchargez l'artefact `SuperMacroV1-Installer` du dernier build réussi.

## Compilation locale

Prérequis : .NET 8 SDK et Inno Setup 6.

```powershell
dotnet publish src/SuperMacroV1/SuperMacroV1.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist/app
& "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe" installer/SuperMacroV1.iss
```

## Compatibilité et usage

SuperMacroV1 envoie des clics via l'API Windows standard `SendInput`. Il ne fait aucune injection dans les processus et ne tente pas de contourner les protections des jeux.

Respectez les règles des serveurs Minecraft et des expériences Roblox : certains interdisent les macros ou les autoclickers.
