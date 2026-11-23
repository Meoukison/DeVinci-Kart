# Amélioration du kart joueur

Compatible avec DeVinci Kart sous Unity 6000.6.3f1 (URP). Le dossier
`Assets/KartUpgrade` contient la variante de prefab, le composant de réglage
ainsi que les sons et le matériau de carrosserie.

- Vitesse maximale sur ligne droite hors boost : 20 m/s (72 km/h) sur écran,
  contre 18 m/s avant. Le coefficient VR reste à 0,72.
- Accélération progressive au départ, poussée plus forte à mi-vitesse et
  atténuée près de la vitesse limite.
- Son de moteur issu d’un enregistrement réel, avec deux boucles pour le ralenti
  et la montée en régime. La licence CC0 est dans `Assets/KartUpgrade/Audio`.
- Peinture orange cuivrée sur la carrosserie existante, avec le volant DeVinci.

## Comparer sur City Slicker

1. Ouvrir `Assets/Scenes/Piste_CitySlicker.unity` et lancer **Play**.
2. Sélectionner **Player Kart** dans la Hierarchy.
3. Choisir **DeVinci Kart → Kart Upgrade → Tester sur le kart sélectionné (Play)**.
   Relancer la même commande pour désactiver ou réactiver l’amélioration.
4. Cliquer dans Game. **W / S** pour accélérer / freiner, **A / D** pour tourner,
   **C** pour voir la carrosserie et **H** pour klaxonner.

Cette commande de menu agit sur le kart de la session Play. Pour intégrer
durablement l’amélioration tout en conservant les références de la scène,
copier le composant **Kart Upgrade** du prefab
`Assets/KartUpgrade/PlayerKart_DeVinci_Upgrade.prefab` et le coller comme
nouveau composant sur le kart joueur de la scène.

## Validation

Vérifié dans Unity sur City Slicker avec le kart déjà en place. Sur une ligne
droite de test, le nouveau réglage atteint 20 m/s, le turbo atteint 29 m/s et
le freinage fonctionne. Le moteur se coupe pendant la pause et le replacement,
et le basculement restaure le son et la peinture d’origine. La vitesse en
course complète et le ressenti avec le casque restent à valider.
