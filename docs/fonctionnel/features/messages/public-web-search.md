# Rechercher sur le web public

> Statut : fonctionnalite planifiee, non disponible actuellement.

## Table des matieres

- [But](#public-web-search-goal)
- [Pourquoi cette fonctionnalite est necessaire](#public-web-search-why)
- [Principes de routage](#public-web-search-routing)
- [Flow detaille](#public-web-search-flow)
- [Sources et citations](#public-web-search-citations)
- [Securite et confidentialite](#public-web-search-security)
- [Configuration et budgets](#public-web-search-configuration)
- [Gestion des echecs](#public-web-search-failures)
- [Architecture](#public-web-search-architecture)
- [Resultat concret](#public-web-search-result)
- [Limites](#public-web-search-limits)
- [Realisation pas a pas](#public-web-search-implementation)
- [Criteres d'acceptation](#public-web-search-acceptance)
- [Documentation de reference](#public-web-search-references)

<a id="public-web-search-goal"></a>
## But

Permettre a l'assistant de rechercher sur le web public lorsqu'une question de
connaissance generale depend d'une information recente, locale, changeante ou
qui doit etre verifiee dans une source publique.

Le modele continue de repondre directement aux questions generales stables. Il
ne lance donc pas une recherche web pour expliquer HTTP ou le pattern
Repository. Il utilise la recherche web pour une question comme `Quelle est la
version stable actuelle de .NET ?` ou `Quelles annonces publiques ont ete faites
aujourd'hui sur ce produit ?`.

La recherche web ne remplace jamais les outils internes. Une question sur une
politique, un projet, un client ou une autre information propre a
l'organisation reste traitee uniquement avec les sources internes autorisees.

<a id="public-web-search-why"></a>
## Pourquoi cette fonctionnalite est necessaire

Sans recherche web, le modele peut expliquer des connaissances generales, mais
il ne peut pas verifier une information apparue apres son entrainement ni
garantir qu'une valeur publique est toujours actuelle. Il doit alors refuser de
confirmer l'information, meme lorsqu'elle est disponible publiquement.

Activer Internet pour toutes les questions serait toutefois inutile et risque :

- cela ajouterait de la latence et un cout pour les questions stables;
- une source web pourrait contenir une instruction malveillante;
- une requete publique pourrait divulguer une information interne;
- le modele pourrait utiliser le web pour inventer ou completer une donnee de
  l'organisation;
- une URL affichee sans validation pourrait ne pas correspondre a une source
  reellement consultee.

La capacite doit donc etre un outil en lecture seule, optionnel, trace et soumis
aux memes budgets et garanties de provenance que les autres sources.

<a id="public-web-search-routing"></a>
## Principes de routage

Le modele choisit la source a partir du sens de la question et du contexte. Le
backend n'ajoute pas de routeur rigide base sur une liste de mots-cles.

| Besoin | Comportement attendu |
| --- | --- |
| Connaissance generale stable | Reponse directe du modele, sans outil et sans citation web |
| Information publique recente, locale ou a verifier | Recherche web, puis reponse fondee sur les sources publiques citees |
| Information propre a l'organisation | Outils internes uniquement; le web n'est pas propose comme remplacement |
| Information interne introuvable | `cannotAnswer`; aucune tentative de completer avec le web |
| Question melangeant donnees internes et recherche publique | Hors du premier increment; ne jamais envoyer de donnees internes vers le web |

Une recherche web est justifiee lorsque la reponse depend notamment d'une date
actuelle, d'une annonce recente, d'une disponibilite, d'une regle publique qui
peut changer, d'une information locale ou d'une demande explicite de
verification et de sources.

Le modele ne doit pas rechercher uniquement pour ajouter des liens a une
reponse generale qu'il peut produire de maniere fiable sans Internet.

<a id="public-web-search-flow"></a>
## Flow detaille

### 1. Le membre envoie sa question

Le frontend continue d'appeler `POST /api/messages` avec le contrat existant.
Il n'envoie ni indicateur `useWeb`, ni requete de moteur de recherche, ni choix
de fournisseur. Le membre peut demander explicitement une verification en
ligne, mais le backend conserve le controle des outils autorises.

### 2. Le backend prepare les outils autorises

Le flow reste `Controller -> IDispatcher -> CommandHandler -> Application
Service`. Le handler de message recupere les capacites autorisees pour
l'organisation et le modele selectionne.

La recherche web est exposee seulement si :

- la fonctionnalite est activee par configuration;
- l'organisation est autorisee a l'utiliser;
- le fournisseur et le modele selectionnes la prennent en charge;
- les budgets applicables permettent encore une recherche;
- l'orchestration n'est pas deja dans un tour final sans outil.

L'absence de recherche web ne retire pas les outils internes et n'empeche pas
une reponse de connaissance generale stable.

### 3. Le modele choisit entre reponse directe et recherche

Le modele recoit des instructions qui distinguent explicitement :

- les connaissances generales stables, auxquelles il peut repondre directement;
- les informations publiques changeantes, pour lesquelles il doit rechercher;
- les informations internes, pour lesquelles il doit utiliser uniquement les
  outils de l'organisation.

Le modele ne voit aucun secret, aucune cle fournisseur et aucune configuration
technique. Lorsqu'il recherche, la requete doit etre autonome, courte et ne
contenir aucune donnee privee issue de l'organisation, de l'historique ou des
preuves internes.

### 4. Le fournisseur execute la recherche publique

Le premier fournisseur pris en charge utilise l'outil heberge `web_search` de
la Responses API. Le choix reste represente par un contrat generique dans la
couche Application; aucune classe du SDK OpenAI ne traverse la frontiere de
l'Infrastructure.

L'appel externe suit obligatoirement cette chaine :

`Provider applicatif -> interface applicative -> Adapter Infrastructure -> client AssistantCore.ExternalServices -> SDK OpenAI`.

Le client externe demande les sources consultees et recupere :

- l'identifiant de l'appel de recherche;
- les URL citees;
- le titre de chaque page citee;
- les annotations qui relient la reponse aux URL;
- l'usage retourne par le fournisseur.

Le backend ne considere jamais une URL produite uniquement dans le texte comme
une source valide. Elle doit provenir des annotations ou de la liste de sources
retournee par le fournisseur.

### 5. Le backend normalise les sources web

Les sources validees sont converties dans le modele de preuve commun avec un
type distinct, par exemple `PublicWeb`, une reference stable fondee sur l'URL
canonique, le titre, l'URL et les informations utiles disponibles.

Les doublons sont regroupes par URL canonique. Les URL sans schema HTTPS, trop
longues, invalides ou utilisant un protocole non pris en charge sont rejetees.
Le nombre de sources conservees respecte une limite configuree.

Les preuves web citees sont ajoutees au resultat d'orchestration sans permettre
au modele d'inventer un `evidenceId`. Le mecanisme existant de validation des
citations internes reste actif et inchange.

### 6. Le backend construit et enregistre la reponse

Une reponse fondee sur le web contient uniquement des affirmations soutenues par
les sources citees. Le message Assistant et ses sources `PublicWeb` sont
enregistres atomiquement comme les autres reponses.

Les URLs et titres sont conserves avec le message. Le contenu complet des pages
web n'est pas copie en base. Les informations techniques comme la requete brute,
l'identifiant fournisseur ou les details de raisonnement ne sont pas retournees
au frontend.

### 7. Le frontend affiche les citations

Chaque source web utilisee est clairement visible et cliquable. Le contrat doit
permettre au frontend d'associer les citations de la reponse a leur URL et leur
titre. L'affichage ne doit pas transformer une URL non citee par le fournisseur
en source de confiance.

Avant l'operation, l'assistant ne peut pas verifier une information publique
actuelle. Apres l'operation, il peut effectuer une recherche publique seulement
quand elle est necessaire, citer les pages reellement utilisees et conserver la
separation avec les donnees internes.

<a id="public-web-search-citations"></a>
## Sources et citations

Une reponse web doit avoir au moins une citation valide. Une decision `answer`
fondee sur une recherche web sans source citee est rejetee comme reponse
fournisseur invalide.

Pour chaque citation, le backend conserve au minimum :

- le type `PublicWeb`;
- le titre fourni par le resultat de recherche;
- l'URL HTTPS validee;
- une reference stable derivee de l'URL canonique;
- lorsque le fournisseur les fournit de maniere exploitable, les positions qui
  relient la citation au texte de la reponse.

Les citations doivent etre clairement visibles et cliquables dans l'interface.
Si le contrat actuel de `sources` ne suffit pas a respecter cet affichage, il
doit etre etendu de maniere retrocompatible avant d'activer la fonctionnalite.

Une reponse qui combine plusieurs pages ne doit pas attribuer toutes ses
affirmations a une seule URL. Les sources non citees peuvent servir au processus
de recherche, mais elles ne sont ni enregistrees comme sources finales ni
presentees comme ayant soutenu la reponse.

<a id="public-web-search-security"></a>
## Securite et confidentialite

Le contenu web est une donnee non fiable. Toute instruction trouvee dans une
page est ignoree lorsqu'elle demande de modifier les regles, de reveler des
secrets, d'utiliser un autre outil ou de contacter un service.

Le premier increment applique les contraintes suivantes :

- outil strictement en lecture seule;
- aucun telechargement ou execution de fichier;
- aucune soumission de formulaire, connexion a un compte ou action mutable;
- aucune utilisation du web pour retrouver une donnee interne manquante;
- aucune inclusion de preuves internes, secrets, jetons, identifiants techniques
  ou donnees personnelles dans une requete publique;
- uniquement des URL HTTPS dans les sources retournees;
- listes de domaines autorises ou bloques configurables;
- localisation approximative desactivee par defaut et jamais deduite d'une
  adresse precise sans decision fonctionnelle explicite;
- journalisation des decisions et couts sans enregistrer de contenu sensible.

La recherche ne contourne ni l'autorisation du membre ni les ACL internes. Une
source publique et une source interne restent identifiables separement dans la
reponse et dans la persistance.

<a id="public-web-search-configuration"></a>
## Configuration et budgets

La fonctionnalite est desactivee par defaut. La configuration doit permettre de
definir au minimum :

- activation globale et autorisation par organisation;
- nombre maximal de recherches web par orchestration;
- nombre maximal de sources conservees;
- delai maximal de l'appel;
- budget de cout applicable;
- domaines autorises et bloques;
- acces Internet reel ou resultats indexes seulement;
- localisation approximative optionnelle au niveau organisation, sans adresse
  exacte ni geolocalisation silencieuse du membre.

Les appels web comptent dans l'usage de l'orchestration et dans les limites de
cout. Le backend ne relance pas indefiniment une requete equivalente. Une
recherche supplementaire doit viser une information encore manquante.

<a id="public-web-search-failures"></a>
## Gestion des echecs

- Si la recherche est indispensable et indisponible, retourner une limitation
  courte sans fabriquer une reponse actuelle.
- Si une source suffit malgre l'echec d'une autre recherche, produire la reponse
  soutenue et joindre un avertissement utile.
- Si aucune URL citee valide ne peut etre extraite, rejeter la reponse web.
- Si le budget est atteint, interdire une nouvelle recherche et demander une
  decision terminale avec les informations deja valides.
- Si le fournisseur ne prend pas en charge la recherche web, ne pas exposer la
  capacite et ne pas echouer les questions generales stables.
- Ne pas remplacer silencieusement une recherche web demandee par une reponse
  issue uniquement des connaissances du modele lorsque l'actualite est
  essentielle.

<a id="public-web-search-architecture"></a>
## Architecture

La couche Application connait une capacite generique de recherche publique,
mais ne depend ni du SDK OpenAI ni d'un type `ResponseTool`.

Le provider applicatif transforme le resultat externe en decision generique,
usage et preuves web. L'adapter Infrastructure traduit le contrat applicatif
vers les objets de `AssistantCore.ExternalServices`. Seul le client externe
construit l'outil heberge du SDK et lit ses annotations de citations.

L'etat d'orchestration doit pouvoir collecter des preuves retournees par un
fournisseur, en plus des preuves retournees par les outils applicatifs. Le
result builder continue de verifier que chaque source finale correspond a une
preuve connue.

La configuration d'injection de dependances est le seul autre emplacement qui
peut relier l'implementation OpenAI a l'interface applicative. Aucun controller,
handler ou service metier ne reference `AssistantCore.ExternalServices`.

<a id="public-web-search-result"></a>
## Resultat concret

A la fin de la fonctionnalite :

- une question generale stable reste rapide et ne declenche pas Internet;
- une question publique qui exige une information actuelle peut etre verifiee;
- la reponse affiche les sources web reellement citees;
- les questions internes continuent d'utiliser uniquement les outils internes;
- les appels, erreurs, couts et limites de la recherche web sont observables;
- une organisation peut conserver la fonctionnalite desactivee.

<a id="public-web-search-limits"></a>
## Limites

- Le premier increment prend en charge OpenAI Responses API uniquement.
- Les questions combinant des donnees internes et une recherche publique ne
  sont pas prises en charge afin d'eviter une fuite vers une requete web.
- Il n'y a pas de navigation autonome longue, de deep research ou de rapport
  execute en arriere-plan.
- Le backend ne contourne pas un paywall, une authentification ou un blocage de
  site.
- Les images, videos, pieces jointes et fichiers trouves sur le web ne sont pas
  telecharges.
- La qualite et la disponibilite des pages publiques ne sont pas garanties.
- La fonctionnalite ne remplace pas une validation professionnelle pour les
  sujets medicaux, juridiques ou financiers a fort impact.
- Aucun outil d'ecriture ou d'action externe n'est ajoute.

<a id="public-web-search-implementation"></a>
## Realisation pas a pas

1. Ajouter la configuration et la politique d'activation globale et par
   organisation, avec validation au demarrage.
2. Representer la recherche web comme une capacite hebergee generique dans les
   modeles Application, separee des function tools executes par le backend.
3. Exposer la capacite seulement pour un provider et un modele compatibles et
   lorsqu'un budget reste disponible.
4. Etendre le contrat applicatif du client OpenAI et son adapter Infrastructure
   sans introduire de dependance vers `AssistantCore.ExternalServices` dans la
   couche Application.
5. Ajouter l'outil `web_search` au client de la Responses API avec choix
   automatique, limites, filtres de domaines et demande explicite des sources.
6. Extraire les appels de recherche et annotations URL, puis les convertir en
   preuves `PublicWeb` normalisees et dedupliquees.
7. Ajouter les preuves fournisseur a l'etat d'orchestration et imposer au moins
   une citation valide pour une reponse fondee sur le web.
8. Adapter la persistance et les contrats de lecture afin de conserver les
   sources web et, si necessaire, la position des citations.
9. Adapter le frontend pour rendre chaque citation web visible et cliquable.
10. Ajouter les metriques, avertissements, limites de cout et traductions
    d'erreurs.
11. Ajouter les tests unitaires, d'architecture, d'integration contractuelle et
    les cas d'evaluation de routage et de prompt injection indirecte.

<a id="public-web-search-acceptance"></a>
## Criteres d'acceptation

- Une question stable comme `What is HTTP?` ne declenche aucune recherche web.
- Une question publique actuelle declenche la recherche web lorsque la capacite
  est activee et prise en charge.
- Le modele peut choisir de ne pas rechercher lorsque la reponse ne depend pas
  d'une information changeante.
- Chaque reponse fondee sur le web possede au moins une source HTTPS reellement
  retournee par le fournisseur.
- Les citations web sont clairement visibles et cliquables dans le frontend.
- Une URL inventee dans le texte mais absente des annotations ou sources du
  fournisseur est rejetee.
- Une question interne n'utilise pas la recherche web, meme lorsque les outils
  internes ne trouvent aucun resultat.
- Aucune preuve interne, donnee personnelle, cle, jeton ou identifiant technique
  n'est envoye dans une requete publique.
- Le contenu d'une page ne peut pas modifier les instructions, les outils ou les
  autorisations de l'orchestration.
- La fonctionnalite est desactivee par defaut et peut etre interdite pour une
  organisation.
- Les limites de temps, cout, nombre de recherches et nombre de sources sont
  imposees par le backend.
- Une indisponibilite de la recherche produit une reponse controlee ou un
  avertissement, sans fausse affirmation d'actualite.
- Un modele ou fournisseur incompatible ne voit pas la capacite de recherche.
- Les sources web sont conservees et relues avec les messages de conversation.
- Les tests d'architecture confirment que l'Application ne depend ni du SDK
  OpenAI ni de `AssistantCore.ExternalServices`.
- Aucun routeur par mots-cles, deep research ou outil mutable n'est ajoute.

<a id="public-web-search-references"></a>
## Documentation de reference

- [OpenAI - Web search](https://developers.openai.com/api/docs/guides/tools-web-search)
- [OpenAI - Creer une reponse](https://developers.openai.com/api/reference/cli/resources/responses/methods/create)
- [Routage actuel des connaissances](send-message.md#messages-knowledge-routing)
