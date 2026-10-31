# Rapport — Claude Pet

Claude Pet est la mascotte de Claude (une boule orange en pixel art, avec des pattes et des bras) qui vit sur les
trois écrans d'Ixorak. Livré : un exécutable natif (`ClaudePet.exe`, 2,5 Mo, aucun runtime à installer), un
installateur (`dist\PetSetup.exe`, 2,9 Mo) et un script qui refait les deux en une commande (`build.bat`).

> **Avant d'installer : Bitdefender.** Pendant le développement, Bitdefender a mis une ancienne version de Pet en
> quarantaine (voir « Ce qui reste fragile »). Il bloque encore les chemins qu'il a nettoyés, dont le raccourci
> `Bureau\Pet.lnk`. Supprime d'abord les éléments « ClaudePet » et `Pet.lnk` de sa quarantaine (ne les restaure pas :
> c'est l'ancienne version). Sinon l'installateur prévient qu'il n'a pas pu créer le raccourci, et Pet sortira du sol
> au lieu de sortir de sa maison.

## Ce qui marche

**Apparence.** Pet est dessiné par le code en pixel art (aucune image récupérée ailleurs), agrandi au plus proche
voisin, environ 240 px de large par défaut à 100 %, taille réglable. Toutes les animations demandées existent :
marche, course, saut, chute, rebond, sonné (étoiles), se relever, assis, dormir (zZz), s'étirer, regarder la souris,
grimper, lever les bras, pendu à la souris, assis sur la souris, tapé, énervé (rouge et fumée), bouche grande ouverte,
manger, creuser, sortir du sol, sortir de la porte, rentrer dans la porte. Il cligne des yeux et suit la souris du regard.

**La maison.** L'icône est une maison en pixel art avec une porte (`.ico` en 13 tailles, de 16 à 256 px, fabriqué par
`tools\AssetGen`). Au lancement, Pet sort par la porte de l'icône « Pet », là où elle est posée sur le bureau : il sort
à la taille de la porte, grandit, puis tombe jusqu'au sol. Sans icône, il sort du sol en bas du Dell. Pour quitter, il
court jusqu'à la maison, saute vers la porte en rétrécissant et rentre, puis l'appli se ferme. Une seule instance : un
double-clic sur la maison pendant qu'il est là le fait rentrer et ferme l'appli.

**Écrans.** Les trois écrans, DPI par écran (Per-Monitor V2), branchement et débranchement d'écran. Le sol est le haut
de la barre des tâches, ou le bas de l'écran quand elle est masquée, écran par écran. Il passe d'un écran à l'autre là
où ils se touchent, rebondit sur les bords extérieurs et le plafond ; les zones mortes sont des murs. Toujours au
premier plan (sauf sur un écran en plein écran), invisible dans la barre des tâches. Icône de notification avec
Calme, Muet et Rentrer à la maison.

**Comportement autonome.** Il marche, court, s'assoit, s'étire, regarde autour de lui et regarde la souris, fait de
petits sauts, des siestes, grimpe aux bords de l'écran et saute sur les fenêtres. Il s'endort quand Ixorak ne touche plus
au PC depuis 3 minutes, et se réveille en s'étirant dès son retour.

**Fenêtres.** Il marche sur le haut visible des fenêtres normales (pas agrandies, pas réduites, visibles). Il bouge avec
la fenêtre, tombe si elle est fermée, réduite ou agrandie. Il saute de fenêtre en fenêtre, rate environ une fois sur six
et tente parfois un saut trop long (et tombe). Les fenêtres hautes, il les atteint en grimpant au bord de l'écran jusqu'à
leur hauteur, puis en sautant. Il ignore les fenêtres agrandies, invisibles, « cloaked », les fenêtres outils, le bureau
et la barre des tâches.

**Plein écran** (une fenêtre couvre tout l'écran sans être agrandie), sur les trois écrans : s'il y est, il creuse et
ressort sur l'écran libre le plus proche. Si le Dell est en plein écran, il passe en tunnel de l'écran 1 à l'écran 3.
Si les trois sont en plein écran, il se cache, et ressort dès qu'un écran se libère.

**Souris.** On ne peut cliquer que sur ses pixels visibles ; ailleurs, les clics passent. On l'attrape et on le lance :
vitesse prise sur la souris, gravité, rebonds amortis, frottement, puis sonné (plus longtemps si la chute est haute) et il
se relève. Clic simple = tape (0,25 s après, pour laisser la place au double-clic), avec son bruit et des étoiles.
Cinq tapes en quatre secondes : énervé pendant 30 s (rouge, fumée, il saute partout et s'accroche au pointeur).
Le pointeur passe près de lui : il saute s'y accrocher, se pend, puis grimpe s'asseoir dessus ; les clics passent à
travers lui ; il faut secouer la souris pour le faire tomber.

**Mode calme** (mémorisé) : plus d'accroche spontanée, ni de sauts sur les fenêtres, ni d'escalade ; il marche lentement
et s'assoit souvent. Pointeur près de lui : il lève les bras et s'y accroche, et tombe dès que la souris bouge.

**Bulles d'options** (double-clic) : Calme et Muet (avec leur état), Nourrir, Dormir (jusqu'à un clic sur lui), Rentrer à
la maison, Réglages (volume et taille), et la jauge de faim. Elles se ferment au premier clic ailleurs, ou avec Échap.

**Nourrir et faim.** Une baie pend au bout du pointeur et le suit. Pet court vers elle (et saute si elle est en hauteur),
ouvre grand la bouche quand elle est proche, la croque et mâche (bruit de mastication) ; la jauge remonte de moitié.
Clic droit : la baie disparaît, sans qu'aucun menu ne s'ouvre. Clic gauche : on la pose, elle tombe au sol et il va la
manger. Une baie calme Pet tout de suite quand il est énervé. Il a faim après 3 h de PC allumé (le PC éteint ou en
veille ne compte pas), la jauge est sauvegardée. Affamé : bulle « J'ai faim ! », plus lent, puis de plus en plus lent
jusqu'à rester assis vers 8 h ; une baie et tout revient.

**Sons.** Des bruitages 8-bit doux, fabriqués par le code (synthèse), pour tout ce qui est demandé : sortir et rentrer,
pas discrets, saut, rebond (plus fort si le choc est fort), tape, énervé, étoiles, attraper la souris, chute, bouche,
manger, bulles, ronflement, creuser, sortir du sol. Muet et volume sont mémorisés.

**Sauvegarde.** `%AppData%\Pet\config.json` : calme, muet, volume, taille, jauge de faim (et les repères de temps qui
servent à la compter).

**Performances** (mesurées sur le PC d'Ixorak, 20 cœurs logiques) : assis, 0,16 % d'un cœur ; endormi, 0,47 % ; caché ou
Ixorak absent, presque rien ; en mouvement, moins de 1 % d'un cœur ; environ 30 Mo de mémoire. La boucle ne tourne jamais
à fond : elle se règle selon ce que fait Pet (60 images/s en mouvement, 4 à 20 quand il est calme, une par seconde
quand il est caché), et la sortie audio se ferme après 3 s de silence.

## Ce qui reste fragile

- **Bitdefender.** Il a classé une version de développement comme dangereuse (« Process Injection ») et l'a mise en
  quarantaine, avec le raccourci du bureau, le journal et quelques raccourcis des « Éléments récents » de Windows. La
  cause : pour lire la position exacte de l'icône maison, Pet écrivait dans la mémoire de l'Explorateur. J'ai supprimé
  cette méthode, ainsi que le crochet souris global ; la version actuelle a tourné longtemps sans alerte. Mais un
  exécutable non signé et tout neuf peut encore être signalé un jour : si ça arrive, ajoute une exception pour Pet dans
  Bitdefender, ou signale-leur un faux positif. Je n'ai touché à aucun réglage de l'antivirus.
- **La position de l'icône** est maintenant calculée (position de l'élément, espacement, taille des icônes) au lieu
  d'être lue au pixel près : la porte peut être décalée d'un ou deux pixels.
- **Détection du plein écran** : un jeu en « fenêtre sans bordure agrandie » ou un navigateur agrandi ne comptent pas
  comme plein écran (c'est voulu, pour les fenêtres agrandies). Le jeu d'Ixorak, pendant mes tests, n'était pas vu comme
  plein écran. Une petite fenêtre « toujours au premier plan » posée sur un jeu peut aussi gêner la détection.
- **Fenêtres** : les parties cachées par d'autres fenêtres sont relues toutes les 0,4 à 2 s ; entre deux relevés, Pet
  peut rester un instant sur un bord qui vient d'être recouvert.
- **Bulles** : elles prennent le focus comme un menu (c'est ce qui permet de les fermer au premier clic ailleurs, sans
  crochet). Si Windows refuse ce focus, elles surveillent les boutons de la souris à la place.
- **Baie** : tant qu'elle pend au pointeur, une petite zone invisible autour de lui reçoit les clics (le clic gauche
  pose la baie, la molette ne fait rien). C'est ce qui permet d'annuler par clic droit sans ouvrir de menu.
- **Pas testé par moi** : l'installateur et le démarrage automatique (ils écrivent dans le registre, ce que le cahier des
  charges m'interdisait pendant le développement : c'est ton test), les sons à l'oreille (je ne peux pas écouter), une
  barre des tâches visible (les tiennes sont masquées), une échelle différente de 100 % (tes trois écrans sont à 100 %),
  le branchement d'un écran en vrai.
- **Git** : l'étape 3 n'a pas eu son propre commit (la session a été coupée avant) ; elle est dans le commit des
  étapes 3 et 4.

## Choix que j'ai faits

- **Technologie** : C# avec .NET 10 en compilation native (Native AOT) et l'interop Win32 directe (fenêtres « layered »
  à transparence par pixel). Un seul exe de 2,5 Mo, sans runtime à installer, qui démarre tout de suite et reste léger
  en tâche de fond. .NET 10 plutôt que 8 (recommandé) : c'est la version à support long actuelle.
- **Le dessin** : Pet (Clawd) est décrit par des poses (corps, bras, jambes, yeux, bouche) et dessiné par le code dans
  une grille de 48 × 48, puis agrandi au plus proche voisin. Taille par défaut : 9 pixels d'écran par pixel d'art
  (234 × 198 px à 100 %), réglable de 3 à 14. (Ta config actuelle garde la taille qu'elle avait, 7.)
- **La maison** : la position de l'icône est lue par `IShellWindows` → `IFolderView::GetItemPosition`, `GetSpacing` et
  `IFolderView2::GetViewModeAndIconSize`. Au démarrage du PC, si le bureau n'est pas encore prêt, Pet réessaie pendant
  8 s. Sans icône « Pet » (ou si son écran est en plein écran) : il sort du sol en bas du Dell.
- **Clics** : simple clic jugé 0,25 s après ; énervé après 5 tapes en 4 s, pendant 30 s ; quand le pointeur passe
  au-dessus de lui, il saute l'attraper 6 fois sur 10 (puis attend 6 à 12 s) ; assis sur le pointeur de 40 s à 2 min,
  puis il saute ; « secouer » = au moins 3 allers-retours rapides en 0,7 s.
- **Bulles** : disposées en deux colonnes au-dessus de sa tête, jauge en haut, panneau Réglages qui s'ouvre au-dessus.
- **Baie** : clic gauche pour la poser par terre (sinon Ixorak ne pourrait plus cliquer nulle part tant que Pet n'a pas
  mangé), clic droit pour l'annuler. Une baie oubliée au sol disparaît après 2 min 30.
- **Faim** : compteur « temps PC allumé » (`QueryUnbiasedInterruptTime`, qui ne compte ni la veille ni l'hibernation) ;
  vide après 3 h ; lent à 70 % dès 3 h, jusqu'à 0 à 8 h ; une baie rend la moitié de la jauge ; compteur plafonné à 12 h.
  Après un redémarrage, seul le temps depuis le démarrage compte. Bulle « J'ai faim ! » toutes les 40 à 80 s.
- **Plein écran** : la règle du Dell appliquée aux écrans 1 et 3 aussi, comme demandé. Un écran est en plein écran si la
  fenêtre active, ou la plus haute fenêtre de cet écran, le couvre entièrement sans être agrandie.
- **Sauts sur les fenêtres** : portée de 7 largeurs de corps et 5,5 hauteurs ; raté 1 fois sur 6 ; trop long 8 % du
  temps ; un raté qui finit de haut le laisse sonné. **Escalade** : sur les bords extérieurs et les zones mortes, jusqu'à la
  hauteur d'une fenêtre proche (3 fois sur 4) ou au hasard, puis il saute, lâche prise ou redescend.
- **Sommeil** : il s'endort après 3 min sans activité d'Ixorak (clavier et souris) ; siestes de 25 à 70 s.
- **Sons** : synthèse 8-bit (ondes triangle, carrée, sinus et dent de scie avec enveloppes douces, petits glissandos,
  bruit filtré), mixés sur un fil audio à part (waveOut).
- **Installateur** : Inno Setup 6, installation dans `%LocalAppData%\Programs\Claude Pet` sans droits administrateur,
  raccourci « Pet » sur le bureau (maison de Pet) et « Claude Pet » dans le menu Démarrer, démarrage automatique par la
  clé « Run » de l'utilisateur. Le désinstalleur enlève le programme, les raccourcis, le démarrage automatique et
  `%AppData%\Pet`. L'installateur et le désinstalleur ferment Pet proprement s'il tourne.
- **Outils de développement** gardés dans l'exe : `--log` (journal dans `%AppData%\Pet\journal.txt`), `--house <nom>`
  (une autre icône comme maison), `--cmd "<commande>"` (envoie une commande à Pet qui tourne : `state`, `feed`,
  `hunger 3.5`, `windows`, `fs 1 on`, `stats`, `debug climb`…). Sans ces options, pas de journal, et Pet vit sa vie.
- **Fichiers hors du projet** (créés par moi pour les tests, à supprimer quand tu veux) : le raccourci **PetDev** sur ton
  bureau (il lance la version de développement avec `--house PetDev`) ; une ligne `/Pet/` dans
  `F:\protos\.git\info\exclude` (pour que le dépôt de `F:\protos` ignore ce dossier, purement local). Les lancements faits
  depuis Claude rangent leur config dans le dossier virtualisé de l'appli Claude
  (`%LocalAppData%\Packages\Claude_…\LocalCache\Roaming\Pet`) ; tes lancements normaux utilisent `%AppData%\Pet`.
- **Sorties du build** dans `out\` (les anciens chemins `publish\` et `src\ClaudePet\bin\` restent verrouillés par la
  quarantaine de Bitdefender) ; fins de ligne normalisées (`.gitattributes`).

## Installer, désinstaller, recompiler

**Installer.** (Après avoir vidé la quarantaine de Bitdefender, voir en haut.) Double-clic sur `dist\PetSetup.exe`,
puis « Installer ». À la fin, Pet se lance. Tu trouves le raccourci **Pet** (la maison) sur le bureau : place-le sur le
Dell. Pet démarre ensuite tout seul avec Windows.

**Désinstaller.** Paramètres → Applications → Applications installées → Claude Pet → Désinstaller. Tout est enlevé :
programme, raccourcis, démarrage automatique et réglages.

**Recompiler.** Double-clic sur `build.bat` (ou `.\build.ps1` dans PowerShell). Il refait `out\app\ClaudePet.exe` puis
`dist\PetSetup.exe`. `.\build.ps1 -NoSetup` ne refait que l'exe. Il faut : le SDK .NET 10, les outils C++ de
Visual Studio (Build Tools, pour la compilation native) et Inno Setup 6 (`winget install JRSoftware.InnoSetup`).
Le script trouve tout seul l'éditeur de liens de Visual Studio, et demande à un Pet lancé depuis `out\app` de quitter
avant de recompiler.

## Les étapes (commits)

1. `Étape 1 : la base de Claude Pet`
2. `Étape 2 : la maison`
3. et 4. `Étapes 3 et 4 : interactions, nourrir et faim`
5. `Étape 5 : fenêtres, murs et plein écran`
6. `Étape 6 : sommeil, siestes, regarder la souris, sons`
7. `Étape 7 : installateur, build et rapport`
