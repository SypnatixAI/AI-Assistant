# AssistantCore Microsoft 365 ingestion worker

Le Worker démarre séparément de l'API. Dans le socle du ticket 45, il peut
effectuer une vérification contrôlée d'une connexion avant que les files et les
traitements documentaires soient ajoutés par les tickets suivants.

Les secrets ne sont pas présents dans `appsettings.json`. Le Worker et l'API
partagent le même magasin local `user-secrets` :

```bash
dotnet user-secrets --project AssistantCore.Service set "Microsoft365:ClientSecret" "<secret>"
dotnet user-secrets --project AssistantCore.Service set "ConnectionStrings:AssistantCoreDatabase" "<connection-string>"
```

Pour vérifier une connexion précise au démarrage sans placer son identifiant
dans Git :

```bash
dotnet user-secrets --project AssistantCore.Service set "Microsoft365Worker:RunStartupConnectionCheck" "true"
dotnet user-secrets --project AssistantCore.Service set "Microsoft365Worker:StartupConnectionId" "<connection-id>"
dotnet run --project AssistantCore.Ingestion.Worker
```

Une connexion `Active` est acceptée. Une connexion `PendingConsent`, `Error` ou
`Revoked` arrête la vérification avec une erreur et aucun appel Microsoft n'est
effectué.
