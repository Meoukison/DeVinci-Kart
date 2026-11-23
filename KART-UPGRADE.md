# Amélioration du kart joueur

Compatible avec DeVinci Kart sous Unity 6000.6.3f1 (URP). Le dossier
`Assets/KartUpgrade` contient la variante de prefab, le composant de réglage
ainsi que les sons et le matériau de carrosserie.

- Vitesse maximale sur ligne droite hors boost : 27 m/s (97,2 km/h) sur écran.
  Le turbo sur écran est plafonné à 36 m/s. Sur sol plat, à pleine direction
  avec l'aide au pilotage, le kart passe d'environ 45 à 58 km/h.
  Le réglage dans le casque reste limité à 14,4 m/s.
- Accélération progressive au départ, poussée plus forte à mi-vitesse et
  atténuée près de la vitesse limite. Sur écran, la caméra recule légèrement
  et élargit le champ pendant une accélération mesurée. Elle revient ensuite à
  son cadrage normal.
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

## Glissières et retour sur la piste

Le kart vérifie le prochain déplacement de sa carrosserie devant les glissières
et clôtures de City Slicker, y compris côté dos des triangles. Si une glissière
est détectée, la vitesse vers elle est freinée et la composante parallèle est
conservée. Un obstacle sans collider doit être corrigé dans le circuit.

Sur City Slicker, le retour automatique se déclenche après environ une seconde
au sol et nettement hors de la zone de course. Les roues sondent les surfaces
de circulation, y compris les rampes et les voies en dénivelé séparées de la
ligne centrale. La limite de chute de la scène s'applique toujours.
Pour revenir manuellement, maintenir **R** environ une seconde. Le kart est remis sur la
dernière projection valide au centre de la piste et dans le sens de la course.
Après le retour, la peinture pulse deux fois en ambre sur écran (une fois en VR)
et le tableau de bord indique **BACK ON TRACK**.

## Validation

Vérifié dans Unity sur City Slicker avec le kart déjà en place. La ligne droite de test confirme
une vitesse de 97,2 km/h ainsi que l’élargissement bref du champ à l’accélération. Le moteur se coupe pendant la pause et le replacement,
et le basculement restaure le son et la peinture d’origine. La vitesse en
course complète et le ressenti avec le casque restent à valider.

Retour au centre et glissière contrôlés en Play Mode sur City Slicker. Sur la
glissière à une seule face du banc d'essai, le kart d'origine traverse tandis que
le kart équipé s'arrête avant le bord, même en turbo.

Vérifié sur les quatre bandes de turbo et sur trois points de rampe goudronnée
situés entre 20 et 23 m du centre. Une sortie réelle sur un sol hors piste
déclenche encore le retour en environ une seconde.
