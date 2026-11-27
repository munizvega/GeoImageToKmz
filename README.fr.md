# GeoImageToKmz Map Editor

**Guides :** 🇪🇸 [Español](README.md) · 🇬🇧 [English](README.en.md) · 🇫🇷 [Français](README.fr.md) · 🇩🇪 [Deutsch](README.de.md)

Préparez des cartes, sélectionnez une zone et créez un fichier KMZ pour votre visionneuse ou votre appareil compatible.

## Sommaire

- [Premiers pas](#inicio)
- [Garmin Custom Maps](#garmincustommaps)
- [Interface et navigation](#interfaz)
- [Projets](#proyectos)
- [Cartes et tracés](#mapas-tracks)
- [Exportation KMZ](#exportacion)
- [Sélection des tuiles](#teselas)
- [Procédure avec de petites tuiles](#teselas-pequenas)
- [Procédure avec de grandes tuiles](#teselas-grandes)
- [Sélection à partir d'un tracé](#track)
- [Paramètres de sortie et luminosité](#ajustes-salida)
- [Langue et dossiers temporaires](#directorios)
- [Sources cartographiques et à propos](#fuentes-mapas-acerca)
- [Dépannage](#problemas)
- [Développement](#developpement)

<a id="inicio"></a>
## Premiers pas

1. Ouvrez l'éditeur et chargez un ou plusieurs fichiers raster géoréférencés pris en charge par GDAL avec **Charger des cartes** ou en les faisant glisser dans la fenêtre.
2. Pour utiliser un itinéraire, chargez un fichier GPX avec **Charger des tracés**. Cela vous aidera à créer une carte autour du tracé.
3. Vérifiez les couches visibles, sélectionnez la zone et les paramètres d'exportation, puis choisissez la destination du KMZ.
4. Cliquez sur **Exporter au format KMZ**, attendez la confirmation et vérifiez le résultat dans la visionneuse ou sur l'appareil cible.

Sous Windows, lancez l'application à partir de son fichier `.exe`. Sous Linux, exécutez `./GeoImageToKmzMapEditor`.

![Écran de démarrage de GeoImageToKmz Map Editor](GeoImageToKmzMapEditor/Help/inicio.png)

<a id="GarminCustomMaps"></a>
## Garmin Custom Maps

Cette application convient parfaitement à la création de Garmin Custom Maps au format KMZ. Leur création manuelle peut être fastidieuse, car chaque appareil impose ses propres limites. Vérifiez les limites de votre appareil et configurez les options en conséquence.

Garmin BaseCamp peut vous aider : si vous essayez de copier une carte contenant trop de tuiles, le logiciel signale que la limite est dépassée. L'application a été testée avec un Garmin Edge 850, dont la limite est de 250 tuiles. La compatibilité et les limites varient selon les appareils ; vous devrez peut-être tester les limites du vôtre.

Le nombre et la taille des tuiles étant limités, la qualité et la résolution de la carte dépendent du nombre de tuiles. Un plus grand nombre de tuiles peut couvrir une zone plus vaste ou fournir davantage de détails : 250 tuiles sur un kilomètre carré offrent plus de détails que 100 tuiles sur la même zone. Essayez d'utiliser autant de tuiles que votre appareil le permet. Vous pouvez sélectionner de très petites tuiles le long du parcours, puis dézoomer et ajouter autour des tuiles plus grandes et moins détaillées pour couvrir les environs ou d'éventuels itinéraires de repli.

![Plusieurs tuiles sélectionnées sur la carte](GeoImageToKmzMapEditor/Help/teselasvarias.png)

<a id="interfaz"></a>
## Interface et navigation

La fenêtre regroupe l'arborescence des projets, cartes et tracés, la visionneuse centrale et le panneau **Options d'exportation**. La barre d'état affiche notamment les coordonnées, le chargement et les erreurs.

Faites glisser la carte pour la déplacer ; utilisez la molette ou les boutons **+** et **−** pour zoomer, et **Afficher la carte entière** pour revenir à l'étendue complète. Les coordonnées affichées dépendent de la projection de la carte.

<a id="proyectos"></a>
## Projets

Dans **Fichier**, vous pouvez créer, ouvrir, enregistrer ou enregistrer sous un projet. Les projets utilisent l'extension `.gtk`. L'arborescence permet de renommer ou de fermer des éléments. Si des modifications ne sont pas enregistrées à la fermeture, l'éditeur vous demande si vous souhaitez les enregistrer.

Le projet conserve les références aux fichiers et aux options de travail, y compris les tuiles sélectionnées. Conservez les fichiers raster et GPX d'origine et veillez à ce que leurs chemins restent accessibles : enregistrer un projet n'équivaut pas à exporter un KMZ.

<a id="mapas-tracks"></a>
## Cartes et tracés

Utilisez **Charger des cartes** pour sélectionner des fichiers raster compatibles, tels que `.tif`, `.tiff` ou `.jp2`, et **Charger des tracés** pour sélectionner des fichiers `.gpx`. L'arborescence permet de rechercher des éléments, de modifier leur visibilité et de les fermer ; son menu contextuel propose notamment d'ouvrir le dossier contenant le fichier.

Les couches raster visibles sont incluses dans l'exportation. Masquez celles que vous ne souhaitez pas inclure et vérifiez que les cartes et le tracé couvrent la même zone. Le GPX sert à afficher l'itinéraire et à sélectionner une zone ; ne partez pas du principe que le tracé sera exporté comme une ligne distincte dans le KMZ.

<a id="exportacion"></a>
## Exportation KMZ

Dans **Zone à exporter**, choisissez l'une des options suivantes :

- **Cartes sélectionnées** : traite toutes les couches raster visibles.
- **Tuiles sélectionnées** : traite les zones rectangulaires choisies dans la visionneuse.
- **Réduire la carte au contour du tracé** : utilise le rectangle englobant le tracé, auquel s'ajoute la marge indiquée ; il ne s'agit pas d'un masque suivant exactement l'itinéraire.

Dans le panneau d'exportation, indiquez le dossier et le nom du fichier ; l'extension `.kmz` est ajoutée si nécessaire. Cliquez sur **Exporter au format KMZ** et suivez la progression globale et celle de chaque étape. Vous pouvez demander **Annuler** pendant le traitement ; attendez le message d'annulation avant de continuer.

<a id="teselas"></a>
## Sélection des tuiles

Sélectionnez **Tuiles sélectionnées**. En mode navigation, cliquez sur une cellule de la carte pour l'ajouter ; cliquez dans une tuile sélectionnée pour la retirer. Faites glisser la carte pour la déplacer. Le compteur indique le nombre sélectionné et le maximum ; une fois le maximum atteint, vous ne pouvez plus ajouter de tuiles manuellement.

La grille représente des étendues géographiques : tant que la taille n'est pas verrouillée, le zoom détermine les dimensions au sol de chaque cellule. **Verrouiller la taille des tuiles** réutilise les dimensions géographiques de la première sélection pour les suivantes, même si vous changez de zoom. Désactivez cette option pour choisir de nouvelles dimensions à un autre niveau de zoom ; les sélections existantes sont conservées.

**Effacer les tuiles** supprime toute la sélection. **Optimiser les tuiles** subdivise uniformément les zones sélectionnées en utilisant le maximum disponible ; cette option ne fusionne pas les tuiles et n'augmente pas la limite. Si le nombre de tuiles restantes est insuffisant, la sélection peut rester inchangée. La taille géographique de ces sélections diffère de la **Taille des tuiles (px)**, qui définit les pixels de sortie.

<a id="teselas-pequenas"></a>
## Procédure avec de petites tuiles

1. Sélectionnez le mode tuiles, effacez la sélection si nécessaire et zoomez jusqu'à ce que les cellules couvrent de petites zones.
2. Sélectionnez les cellules voulues. Pour conserver leur taille géographique pendant la navigation, activez **Verrouiller la taille des tuiles** après avoir sélectionné la première tuile.
3. Vérifiez la couverture et le compteur ; augmentez le maximum si nécessaire ou réduisez la zone. Configurez la destination et la sortie, puis exportez.

Les petites zones permettent une sélection plus précise, mais peuvent nécessiter davantage de tuiles. Vérifiez les limites de l'appareil cible. À taille de sortie en pixels identique, une tuile couvrant une zone plus petite peut préserver davantage de détails, si la résolution de l'image source est suffisante. Par exemple, sélectionnez d'abord de petites tuiles le long du tracé ; désactivez ensuite le verrouillage, dézoomez et ajoutez des tuiles plus grandes et moins détaillées autour pour étendre la couverture.

<a id="teselas-grandes"></a>
## Procédure avec de grandes tuiles

1. Effacez la sélection et dézoomez avant de sélectionner afin que chaque cellule couvre une zone plus vaste. Verrouillez la taille après la première sélection si vous souhaitez la conserver pendant les changements de zoom.
2. Sélectionnez les zones nécessaires et vérifiez qu'elles couvrent l'itinéraire ou le territoire prévu. Une grande cellule peut inclure une zone inutile.
3. Pour subdiviser ces zones sans modifier leur couverture, cliquez sur **Optimiser les tuiles** et vérifiez le compteur. Configurez ensuite la sortie et exportez.

Pour sélectionner une autre taille, effacez les tuiles ou désactivez le verrouillage et changez de zoom. L'optimisation crée davantage de petites sélections ; elle ne garantit pas un nombre précis.

<a id="track"></a>
## Sélection à partir d'un tracé

Chargez un GPX et choisissez **Réduire la carte au contour du tracé**, le tracé et la **Distance minimale au tracé** en mètres. L'exportation utilise le rectangle englobant le tracé, élargi de cette marge ; elle peut donc inclure des zones éloignées de l'itinéraire.

Vous pouvez aussi, dans **Tuiles sélectionnées**, activer **Sélectionner les tuiles du tracé**, choisir le tracé et la marge, puis cliquer sur **Sélectionner les tuiles**. Les cellules proches du parcours sont sélectionnées. Cette action remplace la sélection précédente ; vérifiez la carte et le compteur. Lorsque le maximum est atteint, aucune tuile supplémentaire n'est ajoutée et une partie du tracé peut rester non couverte. Modifiez le zoom, la marge et le maximum, puis relancez la sélection si vous souhaitez une autre couverture.

![Sélection de tuiles le long d'un tracé](GeoImageToKmzMapEditor/Help/seleccion.png)

<a id="ajustes-salida"></a>
## Paramètres de sortie et luminosité

- **Taille des tuiles (px)** : de 256 à 4096 ; valeur par défaut : 1024. Pour Garmin Custom Maps, 1024 × 1024 pixels est une référence courante ; des images plus grandes peuvent perdre en résolution ou ne pas être compatibles avec certains appareils.
- **Nombre maximal de tuiles** : de 1 à 1000 ; valeur par défaut : 250. Cette limite s'applique à la sélection et à la génération du KMZ. Les limites Garmin Custom Maps dépendent du modèle : certains appareils acceptent 100 tuiles, d'autres 500, en comptant aussi les autres cartes installées. Consultez les caractéristiques de votre appareil ; 250 ne garantit pas la compatibilité.
- **Qualité JPEG (%)** : de 1 à 100 dans le panneau principal ; les valeurs élevées préservent généralement davantage de détails au prix d'un fichier plus volumineux.
- **Éclaircir les images avant de générer le KMZ** : active les réglages gamma, luminosité et contraste. Le gamma et le contraste vont de 0,1 à 10 ; la luminosité, de 0 à 100 %. Un gamma supérieur à 1 éclaircit les tons moyens ; la luminosité éclaircit les zones sombres et le contraste accentue l'écart entre les zones claires et foncées.

Le traitement des images peut être lent et nécessiter beaucoup de mémoire et d'espace disque, même si le résultat final ne fait que quelques mégaoctets. Essayez d'abord une petite zone et vérifiez la lisibilité et la taille avant d'en traiter une grande. Les paramètres enregistrés dans le projet peuvent modifier les valeurs initiales.

<a id="directorios"></a>
## Langue et dossiers temporaires

Dans **Options → Langue**, sélectionnez espagnol, anglais, français ou allemand. Dans **Options → Dossiers**, vérifiez le chemin temporaire utilisé et l'espace occupé. Saisissez ou recherchez un dossier personnalisé, puis cliquez sur **Enregistrer** ; **Rétablir les valeurs par défaut** rétablit le dossier temporaire de l'application.

**Attention :** **Vider le dossier temporaire** supprime les fichiers et dossiers du dossier temporaire actuel. Utilisez un dossier dédié, qui ne contient pas de données personnelles, et ne le videz pas pendant un traitement.

<a id="fuentes-mapas-acerca"></a>
## Sources cartographiques et à propos

Dans **Aide → Cartes**, choisissez **Espagne** pour ouvrir le [catalogue de téléchargement du Centre national d'information géographique espagnol](https://centrodedescargas.cnig.es/CentroDescargas/catalogo), ou **France** pour ouvrir le [jeu de données BD ORTHO de l'IGN français](https://cartes.gouv.fr/rechercher-une-donnee/dataset/IGNF_BD-ORTHO). L'application a été testée avec des fichiers `.tif` du PNOA et des fichiers `.jp2` de l'IGNF.

**Aide → À propos** affiche la mention « Créée par Antonio Muñiz Vega à l'aide de l'IA » et précise que l'application est fournie sans garantie et que son utilisation relève de la responsabilité et des risques de l'utilisateur. L'application est distribuée sous la GNU General Public License, version 3 (GPL-3.0). Consultez le [texte officiel de la licence](https://www.gnu.org/licenses/gpl-3.0.html) et le [dépôt du projet sur GitHub](https://github.com/munizvega/GeoImageToKmz). L'application utilise la bibliothèque géospatiale GDAL ; consultez son [site officiel](https://gdal.org).

<a id="problemas"></a>
## Dépannage

- **Aucune couche visible :** chargez un fichier raster pris en charge et activez sa visibilité.
- **Aucune tuile ou aucun tracé sélectionné :** sélectionnez au moins une cellule, ou chargez et choisissez un GPX selon le mode utilisé.
- **Une partie de l'itinéraire manque :** vérifiez la couverture dans la visionneuse et le compteur ; modifiez le zoom, la marge ou le maximum, puis recommencez la sélection.
- **Impossible de charger ou d'exporter :** vérifiez l'existence des fichiers, leur géoréférencement, les autorisations, le chemin de sortie, l'espace temporaire disponible et le message d'erreur. Utilisez un dossier temporaire disposant de suffisamment d'espace.
- **Le KMZ ne s'affiche pas comme prévu :** vérifiez les couches visibles, la zone et le système de référence ; testez la taille, la qualité et la luminosité dans la visionneuse ou sur l'appareil cible.

<a id="developpement"></a>
## Développement

Cette solution .NET autonome contient la bibliothèque de traitement et l'éditeur de cartes interactif ; elle n'inclut ni l'application console d'origine ni `TifToKmzUI`.

- `GeoImageToKmzLib` : traitement d'images géospatiales, utilitaires GDAL, géométrie et services d'exportation KMZ. Les espaces de noms `TifToKmz` et `TifToKmzLib` sont conservés pour la compatibilité du code source et de l'API.
- `GeoImageToKmzMapEditor` : éditeur et visionneuse cartographique de bureau Avalonia, référençant `GeoImageToKmzLib`.

Les deux projets ciblent .NET 10. Les versions des packages et les dépendances natives GDAL restent identiques à celles des projets source. Les packages d'exécution GDAL configurés couvrent `linux-x64` et `win-x64`.

Depuis ce répertoire, compilez et lancez :

```bash
dotnet build GeoImageToKmz.slnx -c Release
dotnet run --project GeoImageToKmzMapEditor/GeoImageToKmzMapEditor.csproj
```

Publiez pour le système d'exploitation et l'architecture cibles afin d'inclure le runtime natif GDAL correspondant :

```bash
dotnet publish GeoImageToKmzMapEditor/GeoImageToKmzMapEditor.csproj -c Release -r linux-x64 --self-contained true -o artifacts/publish/linux-x64
dotnet publish GeoImageToKmzMapEditor/GeoImageToKmzMapEditor.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/win-x64
```