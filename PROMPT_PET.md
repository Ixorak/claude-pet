# Projet « Pet » : animal de compagnie de bureau pour Windows 11

Tu vas créer **Pet**, un petit animal de compagnie qui vit sur l'écran d'Ixorak. C'est une boule orange en pixel art qui se balade sur ses écrans, sur ses fenêtres et sur sa souris.

## Règles de travail (à lire en premier)

* **Ne me pose pas de questions sur le cahier des charges.** Tout ce qu'Ixorak a décidé est écrit ci-dessous. Si un détail manque, choisis la meilleure option et note ton choix dans `RAPPORT.md`, section « Choix que j'ai faits ».
* **Rien d'irréversible sans l'accord d'Ixorak** : ne supprime rien en dehors du dossier du projet, ne modifie pas le registre ni les réglages de Windows pendant le développement (le démarrage auto, c'est l'installateur qui s'en occupe), ne publie rien en ligne. Tu peux installer des outils de dev (SDK .NET, Inno Setup…) via `winget` ou `dotnet tool`.
* **Dossier du projet** : `F:\\protos\\Pet`.
* **Git** : initialise un dépôt local et fais un commit à la fin de chaque étape. Configure-le uniquement pour ce dépôt, avec `git config user.name "Ixorak"` et `git config user.email "Ixorak@users.noreply.github.com"`. Ne pousse rien sur GitHub, Ixorak décidera plus tard.
* **Ixorak est à côté pour tester.** À la fin de bheaucoup d etape, lance l'appli et donne-lui une **courte checklist** de ce qu'il doit tester. S'il signale un bug, corrige-le avant de passer à la suite.
* **Technologie** : choisis la plus adaptée et justifie ton choix dans le rapport. Je recommande C# / .NET 8 avec de l'interop Win32 (fenêtres layered à transparence par pixel), mais c'est toi qui décides.
* **Performance** : l'appli tourne en permanence. Elle doit rester légère, avec un CPU quasi nul quand Pet ne fait rien (pas de boucle à fond, mettre la boucle en pause quand il dort ou qu'il est caché).

## Le setup d'Ixorak

* Windows 11, **3 écrans côte à côte** : écran 1 à gauche, **écran 2 = le Dell, au milieu, écran principal**, écran 3 à droite.
* L'appli doit être **DPI-aware par écran** (Per-Monitor V2) et gérer des résolutions ou échelles différentes d'un écran à l'autre. Elle doit aussi supporter le branchement ou débranchement d'un écran.

## Apparence

* la masquotte de claude avce c pattes et bras
- Agrandissement en « nearest neighbor », pour garder les pixels nets.
* **Taille par défaut** : environ 5× une icône du bureau, soit environ 240 px à 100 % d'échelle. **Taille réglable** dans les Réglages.
* Animations nécessaires (au minimum) : marche, course, saut, chute, rebond, sonné (petites étoiles), se relever, assis, dormir (zZz), s'étirer, regarder la souris, grimper, lever les bras, pendu à la souris, assis sur la souris, tapé, énervé (rouge + fumée), bouche grande ouverte, manger, creuser, sortir du sol, sortir de la porte, rentrer dans la porte.
* Nom de l'animal et de l'appli : **Claude Pet**.

## La maison (icône de l'appli)

* L'icône de l'appli est une **maison en pixel art avec une porte**, dans le même style que Pet. Fais un `.ico` multi-tailles.
* Le raccourci sur le bureau s'appelle **Pet**. Ixorak le met sur le Dell.
* **Apparition** (au lancement, et donc au démarrage du PC) : Pet sort **par la porte de l'icône maison, là où elle est posée sur le bureau**. Pour ça, lis la position réelle de l'icône sur le bureau, par exemple via `IShellWindows` / `IFolderView::GetItemPosition`, ou la ListView du bureau. Il sort à **la taille de la porte**, grandit jusqu'à sa taille normale, puis tombe jusqu'au sol. Si l'icône est introuvable, il apparaît en bas du Dell. Note ce que tu as fait dans le rapport.
* **Quitter** = il **rentre dans sa maison** : il va jusqu'à l'icône, rétrécit et passe par la porte, puis l'appli se ferme.
* **Une seule instance.** Si Ixorak double-clique sur la maison pendant que Pet est déjà là, Pet rentre dans la maison et l'appli se ferme.

## Le sol, les écrans et les bords

* **Sol** : le **haut de la barre des tâches** si elle est visible. Si elle est en masquage automatique, c'est le **bas de l'écran**. Ça se gère écran par écran.
* Pet peut aller sur **les 3 écrans**. Il passe d'un écran à l'autre là où ils se touchent.
* Il **rebondit** sur les bords extérieurs (gauche de l'écran 1, droite de l'écran 3) et sur le plafond. Les zones mortes entre des écrans de hauteurs différentes comptent comme des murs.
* Il peut **grimper sur les bords de l'écran** (les murs).
* Il est **toujours au premier plan**, au-dessus de toutes les fenêtres, sauf sur un écran en plein écran (voir plus bas).
* **Invisible dans la barre des tâches.** Ajoute une icône dans la zone de notification (icônes cachées), avec un petit menu au clic droit : Calme, Muet, Rentrer à la maison. Comme ça, Ixorak peut toujours le contrôler, même si Pet est coincé.

## Comportement autonome

* Pet fait des choses **tout seul** et il est **assez actif** : marcher, courir, s'asseoir, dormir, s'étirer, regarder la souris, grimper aux murs, sauter sur les fenêtres.
* **Fenêtres** : il peut monter et marcher **sur le haut des fenêtres** normales (non agrandies, non réduites, visibles).

  * Si la fenêtre bouge, il bouge avec.
  * Si elle est fermée ou réduite, il tombe.
  * S'il y a une autre fenêtre à côté, il essaie de **sauter dessus**. Fréquence moyenne. Il **rate son saut environ 1 fois sur 6** et tombe. Parfois, il saute même quand c'est trop loin, et du coup il tombe.
  * **Fenêtres agrandies** : il ne marche pas sur leur haut (tout en haut de l'écran), il reste en bas sur le sol.
  * Ignore les fenêtres invisibles ou « cloaked », les fenêtres outils, le bureau et la barre des tâches.
* **Plein écran** (jeu ou vidéo, une fenêtre qui couvre tout l'écran) :

  * Si un écran passe en plein écran pendant que Pet est dessus, **il creuse direct dans le sol** et ressort du sol sur un écran libre.
  * Ixorak l'a décidé pour le Dell. Applique la même règle aux écrans 1 et 3, et note-le dans le rapport.
  * Si le Dell est en plein écran et que Pet veut aller de l'écran 1 à l'écran 3 (ou l'inverse), il **creuse un tunnel** sous le Dell. Si les écrans libres se touchent, pas besoin de tunnel, il marche.
  * Si les 3 écrans sont en plein écran, il se cache complètement jusqu'à ce qu'un écran se libère.

## Interactions avec la souris

* **Clics** : Pet est cliquable seulement sur ses pixels visibles. Partout ailleurs, les clics passent au travers.
* **Attraper et lancer** : Ixorak peut le prendre avec la souris (clic maintenu) et le **jeter en l'air**.

  * La physique doit être **réaliste** : vitesse du lancer prise sur le mouvement de la souris, gravité, **rebonds qui s'amortissent**, frottement au sol.
  * Quand il a fini de rebondir, il est **sonné** (petites étoiles), puis il **se relève sur ses pattes**. La durée de l'étourdissement dépend de la hauteur de la chute.
  * S'il tombe sur le haut d'une fenêtre, il atterrit dessus.
* **Clic simple = taper** : petit bruit de tape, et Pet fait une animation **sonné**. Le clic simple réagit environ 0,25 s après, pour le distinguer du double-clic.
* **Énervé** : s'il se fait taper plein de fois vite (seuil à toi de choisir), il devient **rouge, il fume, et il fait n'importe quoi pendant environ 30 secondes** : il saute partout et s'accroche partout, la folie. Ensuite, il se calme.
* **S'accrocher à la souris** (mode normal) : quand le pointeur passe près de lui, il **saute pour l'attraper**. Il se pend au pointeur, puis il essaie de **grimper pour s'asseoir dessus**.

  * Pendant qu'il est accroché, **les clics d'Ixorak passent à travers lui**, pour qu'il puisse utiliser son PC normalement.
  * Pour le faire tomber, Ixorak doit **secouer la souris**. Il tombe alors avec la physique normale.
* **Double-clic sur Pet = bulles d'options** au-dessus de lui (voir plus bas).

## Mode calme

* Il **arrête de s'accrocher** à la souris tout seul, **arrête de sauter partout** sur les fenêtres et de grimper, et **marche beaucoup plus lentement**. Il s'assoit souvent sans rien faire.
* Si le pointeur passe près de lui, il **lève les bras** vers le pointeur et s'y accroche. Mais **dès qu'Ixorak bouge la souris, il tombe**, sans avoir besoin de secouer.
* Le mode calme est **mémorisé**, il reste actif après un redémarrage.

## Bulles d'options (double-clic)

* Des petites bulles apparaissent au-dessus de Pet, **avec une icône et un texte** (en français). Elles se ferment quand Ixorak **clique ailleurs**.
* Options :

  * **Calme** : active ou désactive le mode calme (l'état est visible).
  * **Nourrir** : voir plus bas.
  * **Muet** : coupe ou remet le son (l'état est visible).
  * **Dormir** : il dort (zZz) jusqu'à ce qu'Ixorak clique sur lui.
  * **Rentrer à la maison** : il rentre dans la maison et l'appli se ferme.
  * **Réglages** : **volume** (curseur) et **taille** de Pet.
* La **jauge de faim** est visible dans les bulles.

## Nourrir et faim

* **Nourrir** : une **baie** apparaît au bout du pointeur et le suit jusqu'à ce que Pet la mange. **Clic droit = annuler** : la baie disparaît, et ce clic droit ne doit pas ouvrir de menu.
* Les deux marchent : Ixorak peut approcher la baie de Pet, **et** Pet court vers la baie quand il la voit.
* Quand la baie est proche, il **ouvre grand la bouche**, puis il **mange** (bruit de mastication). La jauge de faim remonte.
* **Faim** : il a faim au bout d'environ **3 heures pendant que le PC est allumé**. Le temps où le PC est éteint ne compte pas. La jauge est sauvegardée entre les redémarrages.

  * Quand il a faim : une petite bulle « j'ai faim » au-dessus de lui, et **il devient plus lent**.
  * S'il a très faim longtemps : **rien de grave**. Il devient de plus en plus lent, et à la limite il ne bouge plus et reste assis. Dès qu'il mange, tout revient à la normale.

## Sons

* Des petits bruitages pour un peu tout : sortir de la maison, rentrer, pas (discrets), saut, rebond (plus fort quand le choc est fort), tape, énervé, étoiles, attraper la souris, chute, bouche qui s'ouvre, manger, bulles, ronflement, creuser, sortir du sol.
* Style **8-bit doux et mignon**. Les sons sont **générés par le code** (synthèse), sans fichiers audio récupérés ailleurs.
* **Muet** et **volume** sont mémorisés.

## Sauvegarde

Mémorise dans un fichier de config dans `%AppData%\\Pet` : le mode calme, le muet, le volume, la taille et la jauge de faim.

## Installateur

* Un vrai **installateur** (`PetSetup.exe`, avec Inno Setup ou ce que tu juges le mieux) :

  * installation pour l'utilisateur, sans droits admin si possible ;
  * crée le raccourci **Pet** avec l'icône maison sur le bureau ;
  * ajoute le **démarrage automatique avec Windows** ;
  * fournit un **désinstalleur** qui enlève tout, démarrage auto compris.
* Un script `build.bat` (ou `.ps1`) qui recompile l'appli et régénère l'installateur en une commande.

## Étapes (un commit et une checklist de test pour Ixorak à la fin de chaque étape)

1. **Base** : fenêtre transparente au premier plan, Pet affiché, multi-écrans + DPI, détection du sol (barre des tâches visible ou masquée), marche, gravité, attraper/lancer avec rebonds, sonné puis se relever, icône dans la zone de notification.
2. **Maison** : icône maison, sortie par la porte à l'emplacement réel de l'icône, retour à la maison pour quitter, instance unique.
3. **Interactions** : taper, énervé, s'accrocher/s'asseoir sur la souris, secouer, mode calme, bulles, réglages (taille, volume), sauvegarde.
4. **Nourrir et faim**.
5. **Fenêtres et écrans** : marcher sur les fenêtres, sauts (avec 1 raté sur 6), grimper aux murs, plein écran (creuser / tunnel / se cacher).
6. **Sons** et finitions des comportements autonomes (dormir, s'étirer, regarder la souris…).
7. **Installateur**, démarrage auto, tests finaux.

## Rapport final

À la fin, écris `RAPPORT.md` avec :

* ce qui marche ;
* ce qui ne marche pas ou reste fragile ;
* les choix que tu as faits toi-même ;
* comment installer, désinstaller et recompiler.

