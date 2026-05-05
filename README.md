# FruitShoot — Projet VR

**FruitShoot** est un jeu de réalité virtuelle de type arcade développé dans le cadre du module **EDN** sur le thème imposé *Saveur Savante*. Le joueur, debout et immobile, tranche des fruits et évite des bombes à l'aide de différentes armes virtuelles. Le projet est livré sous forme d'**APK** installable sur casque **PICO**, sans connexion PC requise.

---

## Sommaire

- [Contexte et cahier des charges](#1-contexte-et-cahier-des-charges)
- [Concept du jeu](#2-concept-du-jeu)
- [Réalisation technique](#3-réalisation-technique)
- [Démonstration](#4-démonstration)
- [Documents du projet](#5-documents-du-projet)
- [Auteurs](#6-auteurs)

---

## 1. Contexte et cahier des charges

Le projet répond aux contraintes imposées par l'énoncé pédagogique ([Sujet.md](Sujet.md)) :

| Contrainte | Réponse apportée dans FruitShoot |
|---|---|
| Thème *Saveur Savante* | Fruits, recettes culinaires, ambiance cuisine / arcade |
| Unity · APK · PICO · autonome | Build Android OpenXR ciblant l'écosystème PICO, sans PC |
| Un seul joueur | Session solo, scores enregistrés localement |
| Position statique (pas de room-scale) | Le joueur reste debout ; les cibles se déplacent vers lui |
| Expérience interactive | Découpe au sabre laser ou aux couteaux, choix de mode et de difficulté |
| Prise en main sans formation | Menus et HUD intégralement en français, retour menu / relance simplifiés |
| Relance de session sans redémarrage | Menu principal accessible à tout moment, raccourcis manette dédiés |
| Session de 5 à 10 minutes satisfaisante | Chronomètre, niveaux de difficulté, combos, mode Recette |
| Gestion de projet et Git | Dépôt versionné, itérations documentées |

---

## 2. Concept du jeu

FruitShoot propose deux modes de jeu complémentaires.

**Mode Défouloir** — Le joueur enchaîne les coupes de fruits pour accumuler le maximum de points et déclencher des combos. Les bombes et les erreurs génèrent des *strikes* (croix rouges) ; trois strikes provoquent un game over. L'arme est au choix : couteaux (kunai) ou sabre laser.

**Mode Recette** — Une commande de fruits avec quantités précises est affichée sur une tablette en monde. Couper un fruit absent de la commande constitue une erreur (strike). Le score reflète le nombre de recettes complétées. Un retour visuel (flash sur la tablette) et haptique (vibration des manettes) confirme chaque recette réussie.

La boucle arcade est courte et lisible, adaptée aussi bien à une démo en salle qu'à une séance casque autonome.

---

## 3. Réalisation technique

**Stack :** Unity · OpenXR · XR Interaction Toolkit · intégration PICO (package local).

La logique de jeu est centralisée dans un `GameManager` (singleton). L'interface est générée en code à l'exécution et positionnée via des ancres de scène pour garantir le confort en VR. Les interactions (grab, ray, tir) reposent entièrement sur XR Interaction Toolkit.

Pour le détail de l'architecture, des scripts et de la procédure de build, consulter [DOCUMENTATION_TECHNIQUE.md](DOCUMENTATION_TECHNIQUE.md).

---

## 4. Démonstration (5 à 10 min)

1. Installer et lancer l'APK sur le casque PICO — le menu principal apparaît en français.
2. Lancer une partie **Défouloir** (difficulté moyenne) : illustrer les combos, les strikes et la fin de partie.
3. Lancer le mode **Recette** : lire la commande sur la tablette, valider une recette, observer le retour haptique et afficher le classement si disponible dans la scène.

---

## 5. Documents du projet

| Document | Rôle |
|---|---|
| [MANUEL_UTILISATION.md](MANUEL_UTILISATION.md) | Contrôles, modes de jeu, conseils joueur |
| [DOCUMENTATION_TECHNIQUE.md](DOCUMENTATION_TECHNIQUE.md) | Architecture, scripts, build, configuration de scène |
| [Sujet.md](Sujet.md) | Énoncé officiel du projet |
| [Assets/Projet/Guide_Configuration_Scene.md](Assets/Projet/Guide_Configuration_Scene.md) | Montage Unity de la scène |

---

## 6. Auteurs

Projet réalisé dans le cadre du module **EDN** — développement sur casque PICO en séances dédiées, évaluation selon le calendrier du cours (voir [Sujet.md](Sujet.md)).