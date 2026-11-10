# DeVinci Kart

Jeu de kart en réalité virtuelle (Unity 6000.6.3f1, URP).

## Récupérer le projet
1. GitHub Desktop → **File → Clone Repository** → choisir ce dépôt.
2. Ouvrir le dossier cloné avec **Unity Hub**, avec **la même version d'Unity : 6000.6.3f1**.
3. Au premier lancement, Unity installe les packages (dont glTFast pour les modèles .glb) : patienter.
4. Menu **DeVinci Kart → Générer la piste Campus → Campus** : recrée la scène
   `Assets/Scenes/Piste_AncienCampus_NouveauCampus.unity`, dont la copie évidée de l'ancien campus,
   qui n'est pas sur GitHub (trop lourde).

## Contenu
- `Assets/3DModels` : modèles 3D (Tripo / Sketchfab) au format .glb.
- `Assets/DeVinciKart/Editor/CampusTrackGenerator.cs` : générateur de la piste
  (Pôle Léonard de Vinci → Le Pouce → statue → sous la Grande Arche → pont → Terrasses de Nanterre → Campus du Parc).
  Les positions et les échelles se règlent en haut du fichier.

## Travailler à plusieurs
- Avant de commencer : **Fetch** puis **Pull**.
- Une branche par fonctionnalité (`feature/...`), puis merge dans `main`.
- Éviter de modifier la même scène ou le même prefab en même temps.
