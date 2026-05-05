# Documentation technique — FruitShoot

Projet Unity **XR** (OpenXR · PICO · XR Interaction Toolkit). Ce document présente l'**architecture logicielle**, les **scripts**, les **flux de données**, la **procédure de build** et les **pistes d'évolution** du projet.

---

## 1. Informations générales du projet

| Élément | Valeur |
|---|---|
| Moteur | Unity |
| Nom produit | **FruitShoot** (`ProjectSettings → Player → Product Name`) |
| Société | EDN FGES |
| Assets et scripts | `Assets/Projet/` |
| Scènes | `Assets/Scenes/` (ex. `VR Ninja.unity`, `VR Scene.unity`) |

---

## 2. Stack XR et packages Unity

Fichier de référence : [`Packages/manifest.json`](Packages/manifest.json).

| Package | Rôle |
|---|---|
| `com.unity.xr.openxr` | Backend OpenXR |
| `com.unity.xr.openxr.picoxr` | Cible PICO (package local dans `Packages/Local/`) |
| `com.unity.xr.interaction.toolkit` | Interactions 3D : grab, ray, etc. |
| `com.unity.xr.management` | Gestion du loader XR |
| `com.unity.xr.hands` | Support suivi de mains (présent, optionnel) |
| `com.unity.inputsystem` | Gestion des entrées clavier et XR |
| `com.unity.ugui` + TextMeshPro | Interface utilisateur |

La build Android utilise **Player Settings → plateforme Android**, avec **OpenXR** et la feature set **PICO** activée via le package local.

---

## 3. Architecture logicielle

```
┌──────────────┐       StartMode / UI        ┌─────────────┐
│  MenuManager │ ─────────────────────────── ▶│ GameManager │
└──────────────┘                              └──────┬──────┘
                                                     │ OnGameStart / OnGameOver
                                              ┌──────▼──────┐
                                              │   Spawner   │
                                              └──────┬──────┘
                                          Instantiate │
                                    ┌────────────┬───▼──────────┐
                               FruitTarget     Bomb        (armes)
                                    │            │       LaserShooter
                                    │            │           Slicer
                    ProcessFruitSlice│   AddBombHit│
                                    └──────┬─────┘
                                     ┌─────▼──────┐
                                     │ GameManager │◀── UnityEvents ── RecipeUI
                                     └─────┬──────┘
                                           │ EndGame
                                     ┌─────▼──────┐
                                     │ MenuManager │
                                     └────────────┘
```

**Composants principaux :**

- **`GameManager`** (singleton) — État global de la partie : mode, difficulté, arme, timer, score, strikes, combo et recette en cours. Gère l'audio (BGM / SFX), les entrées de navigation rapide (boutons A, B, Menu, clavier) et publie tous les UnityEvents.
- **`MenuManager`** — Menus en world canvas générés à l'exécution. Gère la sélection du mode, de la difficulté et de l'arme, le lancement de la partie, le classement (`PlayerPrefs`) et le placement via ancres de scène.
- **`RecipeUI`** — HUD de partie et tablette de commandes en monde. Écoute les UnityEvents du `GameManager`, affiche la progression de la recette et déclenche le retour visuel / haptique à chaque validation.
- **`Spawner`** — Coroutine de vagues, paramètres interpolés en fonction du temps écoulé et de la difficulté choisie.

---

## 4. Inventaire des scripts (`Assets/Projet/Scripts/`)

| Script | Responsabilités |
|---|---|
| `GameManager.cs` | Modes (`GameMode` : Défouloir / Recette), `Difficulty`, `WeaponType` · Timer, score, strikes, combos · Génération et suivi de recette (`ProcessFruitSlice`, `GenerateRandomRecipe`) · `StartGame` / `EndGame` · Audio · Entrées manette (A, B, Menu) et clavier (M, R) |
| `MenuManager.cs` | UI menu principal et configuration · Panneaux mode / difficulté · Lancement de partie · Classement local par mode · Placement via ancres de scène |
| `RecipeUI.cs` | Layout HUD · Canvas tablette monde · `ShowRecipeCompleted` (coroutine : flash + impulsions haptiques) |
| `XRHaptics.cs` | `InputDevices.SendHapticImpulse` sur les deux contrôleurs |
| `Spawner.cs` | Spawn aléatoire, vagues, forces · Courbe de difficulté · Écoute `OnGameStart` / `OnGameOver` |
| `FruitTarget.cs` | Gestion du fruit entier et de ses moitiés · Collision avec le slicer · Notification au `GameManager` |
| `Bomb.cs` | Collision → incrémentation des strikes via `GameManager` |
| `LaserShooter.cs` | Détection type capsule / overlap le long de la lame · Nœud XR main droite |
| `Slicer.cs` | Lame physique / trigger sur objet tenu |
| `KunaiProjectile.cs` | Physique et détection du couteau en vol |
| `BillboardToCamera.cs` | Orientation UI vers la caméra principale |
| `HeadLockedHud.cs` | HUD fixé à la tête (optionnel selon configuration de scène) |

---

## 5. Données et événements (`GameManager`)

### Champs publics principaux

`gameDuration` · `currentMode` · `difficulty` · `weapon` · `currentTime` · `score` · `isPlaying` · `strikes` · `combo`

### UnityEvents publiés

| Événement | Déclencheur |
|---|---|
| `OnGameStart` | Début de partie |
| `OnGameOver` | Fin de partie (temps écoulé ou 3 strikes) |
| `OnScoreChanged` | Mise à jour du score |
| `OnTimeChanged` | Chaque seconde écoulée |
| `OnStrikeChanged` | Nouveau strike enregistré |
| `OnComboTriggered` | Combo déclenché |
| `OnRecipeStringUpdated` | Texte de commande mis à jour |
| `OnRecipeProgressChanged` | Progression de la recette modifiée |
| `OnRecipeCompleted` | Recette entièrement validée |

### Logique de recette

Un dictionnaire interne (`fruitName → quantité`) suit l'état de la commande. À chaque appel de `ProcessFruitSlice` :
- Si le fruit est dans la commande : décrémentation de la quantité.
- Si le fruit est absent : ajout d'un strike.
- Si toutes les quantités atteignent zéro : `recipesCompleted++`, mise à jour du score, invocation de `OnRecipeCompleted` puis appel de `GenerateRandomRecipe`.

---

## 6. Persistance des données

Le classement est stocké via **`PlayerPrefs`**, avec des clés séparées par mode (format `edn.leaderboard.*`, géré dans `MenuManager`). La logique actuelle conserve le **meilleur score** par nom de joueur.

---

## 7. Ancres de scène

`MenuManager` et `RecipeUI` positionnent leurs canvases par recherche de **`GameObject.Find`** sur les noms suivants :

| Nom de l'objet | Usage |
|---|---|
| `UI_Anchor` | Ancre du menu principal |
| `HUD_Anchor` | Ancre du HUD de partie |
| `OrderTablet_Anchor` | Ancre de la tablette recette |
| `Leaderboard_Anchor` | Ancre du classement |

En l'absence d'une ancre dans la scène, le canvas est positionné devant le joueur par défaut.

Pour le détail de la configuration (XR Origin, Canvas world space, Spawner, prefabs fruits / bombes), voir [`Assets/Projet/Guide_Configuration_Scene.md`](Assets/Projet/Guide_Configuration_Scene.md).

---

## 8. Procédure de build PICO

1. Basculer la plateforme cible sur **Android** dans Build Settings.
2. Activer **OpenXR** dans XR Plug-in Management et activer le profil / feature set **PICO** (package local).
3. Vérifier les paramètres Player : **Minimum API Level**, **Scripting Backend IL2CPP**, architecture **ARM64**.
4. Renseigner le **Product Name** (`FruitShoot`) et le **Package Name** (`com.EDN.FruitShoot`) — ne modifier le Package Name qu'en connaissance de cause (entraîne une nouvelle installation distincte sur le casque).
5. Lancer **Build** ou **Build and Run**.

---

## 9. Points d'attention et dette technique connue

| Sujet | Détail |
|---|---|
| **UI générée en code** | `MenuManager` et `RecipeUI` créent leurs éléments à l'exécution, ce qui accélère l'itération mais rend le versionnement visuel moins immédiat qu'avec des prefabs purs. |
| **Gestion mémoire** | Les fruits et bombes sont instanciés dynamiquement. Pour des sessions prolongées, envisager un système de **pooling** afin de limiter la pression sur le GC. |
| **Mapping des boutons XR** | Les boutons A / B sont mappés via `CommonUsages` sur la main droite. Valider le comportement sur chaque modèle de casque PICO cible. |

---

## 10. Pistes d'extension

| Besoin | Approche recommandée |
|---|---|
| Nouveau fruit | Créer un prefab + composant `FruitTarget` ; ajouter le nom dans la liste de génération de recette (`GameManager`). |
| Nouveau mode de jeu | Étendre l'enum `GameMode` ; brancher le nouveau mode dans `MenuManager` et définir ses règles dans `GameManager`. |
| Options audio / haptique | Exposer des champs sérialisés + persistance via `PlayerPrefs` ; appeler `XRHaptics` conditionnellement. |
| Classement en ligne | Remplacer ou compléter `PlayerPrefs` par une API HTTP légère avec authentification. |

---

## 11. Références internes

| Document | Rôle |
|---|---|
| [README.md](README.md) | Présentation générale du projet |
| [MANUEL_UTILISATION.md](MANUEL_UTILISATION.md) | Contrôles et modes du jeu |
| [Sujet.md](Sujet.md) | Cahier des charges pédagogique |
| [Assets/Projet/Guide_Configuration_Scene.md](Assets/Projet/Guide_Configuration_Scene.md) | Configuration de la scène Unity |