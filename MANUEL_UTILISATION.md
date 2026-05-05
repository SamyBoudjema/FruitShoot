# Manuel d'utilisation — FruitShoot

Ce document décrit les **modes de jeu**, les **contrôles** et les **conseils** pour jouer à FruitShoot en réalité virtuelle sur casque **PICO**. Toute l'interface du jeu est en **français**.

---

## 1. Prérequis

- Casque **PICO** compatible avec l'APK du projet.
- Casque **chargé** avec le **tracking** des manettes actif.
- Un espace suffisant pour **se tenir debout** et **bouger les bras** librement (aucun déplacement physique requis).

---

## 2. Installation et démarrage

1. Installer l'application **FruitShoot** sur le casque (le nom affiché correspond au *Product Name* défini dans Unity).
2. Lancer l'application.
3. Le **menu principal** apparaît devant le joueur sous la forme d'un canvas monde. Utiliser les **rayons des manettes** pour pointer un bouton et appuyer sur la **gâchette** (interaction XRI) pour le valider.

---

## 3. Modes de jeu

### 3.1 Mode Défouloir

| Élément | Description |
|---|---|
| **Objectif** | Obtenir le meilleur score en tranchant un maximum de fruits. |
| **Combos** | Enchaîner des coupes dans la fenêtre de temps affichée multiplie les points. |
| **Strikes** | Chaque bombe touchée ou erreur commise ajoute un strike (croix rouge). À **3 strikes**, la partie se termine (game over). |
| **Temps** | Un chronomètre décompte ; à zéro, la partie s'arrête. |
| **Configuration** | La difficulté et l'arme (couteaux ou sabre laser) sont choisies avant le lancement. |

### 3.2 Mode Recette

| Élément | Description |
|---|---|
| **Objectif** | Réaliser un maximum de recettes complètes dans le temps imparti. |
| **Commande** | Une liste de fruits et de quantités à couper est affichée sur la tablette. Une barre de progression indique l'avancement. |
| **Erreur** | Couper un fruit absent de la commande ou toucher une bombe génère un strike. |
| **Arme** | Uniquement les **couteaux**, conformément à la mécanique « chef cuisinier ». |
| **Validation** | À la fin de chaque recette : message de réussite, flash sur la tablette et vibration des manettes. Une nouvelle commande est automatiquement générée. |

---

## 4. Armes disponibles

| Arme | Description |
|---|---|
| **Couteaux (kunai)** | Objets saisissables ; peuvent être lancés ou utilisés au contact selon la configuration de la scène. Disponibles dans les deux modes. |
| **Sabre laser** | Détection de lame le long de l'arme. Disponible en mode Défouloir uniquement. |

> Conserver les gestes **devant soi** pour optimiser la détection et éviter de sortir de la zone de jeu.

---

## 5. Contrôles manette (PICO / OpenXR)

Les actions suivantes sont actives **pendant une partie** (sauf mention contraire).

| Action | Commande |
|---|---|
| **Retour au menu principal** | Bouton **Menu** (gauche ou droite) *ou* bouton **B** (secondaire main droite) — appui bref. |
| **Relancer la partie** avec les mêmes réglages | Bouton **A** (principal main droite) — appui bref. |
| **Lancer des couteaux ou activer le sabre** en partie uniquement | Gachette de droite — appui bref. pour le mode couteau ou appui long pour le sabre |

> Sur PICO, les boutons **A** et **B** correspondent aux usages *primary* / *secondary* de la main droite. Le bouton **Menu** est le bouton système dédié.

### Raccourcis clavier (éditeur Unity / démo PC uniquement)

| Touche | Effet |
|---|---|
| **M** | Quitter la partie et afficher le menu principal. |
| **R** | Relancer la partie (équivalent au bouton A). |

---

## 6. Interface en cours de partie

- **HUD** : affiche le score, le temps restant, les strikes et le combo en cours (selon le mode).
- **Mode Recette** : une **tablette de commandes** en monde affiche la recette en cours (positionnée via l'ancre de scène `OrderTablet_Anchor`).
- **Fin de partie** : un écran de résultats permet de saisir un nom pour le **classement local** (selon la version de la scène).

---

## 7. Conseils pour une session optimale (5–10 min)

1. Commencer en **difficulté facile** pour se familiariser avec les armes et les mécaniques.
2. En **mode Recette**, lire attentivement la commande avant de commencer à couper.
3. En **mode Défouloir**, prioriser l'évitement des bombes plutôt que la maximisation du score.
4. Utiliser le bouton **B** ou **Menu** pour revenir au menu principal sans fermer l'application.

---

## 8. Résolution des problèmes courants

| Problème | Solution |
|---|---|
| Aucun rayon affiché dans le menu | Vérifier que les manettes sont correctement suivies (tracking actif). Rapprocher les manettes de la tête. |
| Les boutons A / B ne répondent pas | Effectuer un appui bref et relâcher complètement entre deux actions. |
| Texte difficile à lire | Ajuster la hauteur du casque ou demander un repositionnement des ancres `UI_Anchor` / `OrderTablet_Anchor` dans la scène Unity. |

---

Pour la configuration de la scène dans Unity, consulter [Assets/Projet/Guide_Configuration_Scene.md](Assets/Projet/Guide_Configuration_Scene.md).