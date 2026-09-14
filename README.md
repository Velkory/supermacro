# SuperMacroV1 v1.0.3

Application Windows d'auto-clic avec panneau de configuration.

## Fonctions

- Vitesse réglable de 1 à 100 clics par seconde
- Clic gauche, droit ou milieu
- Modes **Bascule** et **Maintien**
- Raccourci personnalisable avec Ctrl, Alt et Maj
- Arrêt d'urgence global avec **F12**
- Démarrage automatique de la macro à l'ouverture
- Mode universel pour tous les clients Minecraft
- Détection de Minecraft Java/Bedrock, CMClient, Lunar, Badlion, Feather, LabyMod, Prism, MultiMC, CurseForge, Modrinth et autres clients Java
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

## Nouveautés 1.0.2

- Mode universel lorsque le filtre de fenêtre est décoché
- Reconnaissance étendue des processus Java et des principaux clients Minecraft
- Libellés du panneau clarifiés pour le mode universel
- Version de l'application et de l'installateur mise à jour

## Nouveautés 1.0.3 — Blox Fruits

- Section dédiée accessible depuis le panneau principal
- Preset Air Dash : Espace puis Q
- Preset Soru vers Air Dash : R, Espace puis Q
- Preset Air Jump vers Soru et Dash : Espace, R puis Q
- Délai réglable de 20 à 500 ms
- Raccourci global configurable, F8 par défaut
- Vérification optionnelle que Roblox est au premier plan
- F12 annule immédiatement la séquence
