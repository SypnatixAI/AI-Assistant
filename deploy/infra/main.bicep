targetScope = 'resourceGroup'

param location string = resourceGroup().location

@allowed([
  'dev'
  'certif'
])
param environmentName string

@minLength(3)
@maxLength(8)
param nameSuffix string

@description('Resource group containing the shared ACR.')
param sharedResourceGroupName string

@description('Immutable backend image tag, normally sha-<Git commit SHA>.')
param backendImageTag string

@description('Immutable SPA image tag, normally sha-<Git commit SHA>.')
param spaImageTag string

param sqlAdministratorLogin string = 'assistantadmin'

param azureAdTenantId string = 'organizations'
param azureAdApiClientId string = 'f70fb50b-52d5-4346-b769-1121cb3ab3e2'
param microsoft365ClientId string = environmentName == 'certif'
  ? '558d6670-3549-423e-ae92-c5ff1d3b326b'
  : '00000000-0000-0000-0000-000000000001'
param spaEntraClientId string = '97fda345-b54e-4243-b05a-31623871df18'
@description('Microsoft Entra tenant that authenticates SQLPad users.')
param sqlpadEntraTenantId string

@description('Client ID of the environment-specific SQLPad App Registration.')
param sqlpadEntraClientId string

@description('Object ID of the Microsoft Entra group allowed to access SQLPad.')
param sqlpadAllowedGroupObjectId string

param azureSearchEndpoint string = 'https://synaptixsearch.search.windows.net'
param azureSearchIndexName string = 'microsoft-content-${environmentName}'

param tags object = {
  application: 'assistant'
  environment: environmentName
  managedBy: 'bicep'
}

var isDev = environmentName == 'dev'
var acrName = 'acrassistant${nameSuffix}'
var acrLoginServer = '${acrName}.azurecr.io'
var keyVaultEnvironmentName = environmentName == 'certif' ? 'cert' : environmentName
var keyVaultName = 'kv-assistant-${keyVaultEnvironmentName}-${nameSuffix}'
var containerEnvironmentName = 'cae-assistant-${environmentName}'
var apiAppName = 'ca-assistant-api-${environmentName}'
var workerAppName = 'ca-assistant-worker-${environmentName}'
var spaAppName = 'ca-assistant-spa-${environmentName}'
var devSpaCustomDomain = 'assistant-dev.onpremia.ca'
var devBffCustomDomain = 'assistant-bff-dev.onpremia.ca'
var devSpaCertificateName = 'assistant-dev.onpremia.ca-cae-assi-260903024140'
var devBffCertificateName = 'assistant-bff-dev.onpremia.c-cae-assi-260903025937'
var wiremockAppName = 'ca-assistant-wiremock-${environmentName}'
var sqlpadAppName = 'ca-assistant-sqlpad-${environmentName}'
var migrationsJobName = 'caj-assistant-migrations-${environmentName}'
var workloadIdentityName = 'id-assistant-workload-${environmentName}'
var acrPullIdentityName = 'id-assistant-acr-${environmentName}'
var sqlpadStorageAccountName = 'stasqlpad${environmentName}${nameSuffix}'
var sqlpadStorageName = 'sqlpad-files'
var sqlpadFileShareName = 'sqlpad'

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource workloadIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: workloadIdentityName
  location: location
  tags: tags
}

resource acrPullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: acrPullIdentityName
  location: location
  tags: tags
}

resource keyVaultSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, workloadIdentity.id, 'KeyVaultSecretsUser')
  scope: keyVault
  properties: {
    principalId: workloadIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '4633458b-17de-408a-b874-0445c86b69e6'
    )
  }
}

module acrPullRole './modules/acr-pull-role.bicep' = {
  name: 'acr-pull-${environmentName}'
  scope: resourceGroup(sharedResourceGroupName)
  params: {
    acrName: acrName
    principalId: acrPullIdentity.properties.principalId
  }
}

module sql './modules/sql.bicep' = {
  name: 'sql-${environmentName}'
  params: {
    location: location
    environmentName: environmentName
    nameSuffix: nameSuffix
    keyVaultName: keyVault.name
    administratorLogin: sqlAdministratorLogin
    administratorPassword: keyVault.getSecret('sql-admin-password')
    tags: tags
  }
}

resource containerEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: containerEnvironmentName
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'azure-monitor'
    }
    zoneRedundant: false
  }
}

resource sqlpadStorageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: sqlpadStorageAccountName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource sqlpadFileService 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' = {
  parent: sqlpadStorageAccount
  name: 'default'
}

resource sqlpadFileShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  parent: sqlpadFileService
  name: sqlpadFileShareName
  properties: {
    accessTier: 'TransactionOptimized'
    enabledProtocols: 'SMB'
    shareQuota: 5
  }
}

resource sqlpadEnvironmentStorage 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  parent: containerEnvironment
  name: sqlpadStorageName
  properties: {
    azureFile: {
      accessMode: 'ReadWrite'
      accountName: sqlpadStorageAccount.name
      accountKey: sqlpadStorageAccount.listKeys().keys[0].value
      shareName: sqlpadFileShare.name
    }
  }
}

var generatedApiBaseUrl = 'https://${apiAppName}.${containerEnvironment.properties.defaultDomain}'
var generatedSpaBaseUrl = 'https://${spaAppName}.${containerEnvironment.properties.defaultDomain}'
var apiBaseUrl = isDev ? 'https://${devBffCustomDomain}' : generatedApiBaseUrl
var spaBaseUrl = isDev ? 'https://${devSpaCustomDomain}' : generatedSpaBaseUrl
var wiremockPublicBaseUrl = 'https://${wiremockAppName}.${containerEnvironment.properties.defaultDomain}'
var wiremockInternalBaseUrl = 'http://${wiremockAppName}'
var sqlpadBaseUrl = 'https://${sqlpadAppName}.${containerEnvironment.properties.defaultDomain}'
var keyVaultBaseUrl = '${keyVault.properties.vaultUri}secrets'

var devSpaCertificateId = resourceId(
  'Microsoft.App/managedEnvironments/managedCertificates',
  containerEnvironmentName,
  devSpaCertificateName
)
var devBffCertificateId = resourceId(
  'Microsoft.App/managedEnvironments/managedCertificates',
  containerEnvironmentName,
  devBffCertificateName
)

var managedIdentities = {
  '${workloadIdentity.id}': {}
  '${acrPullIdentity.id}': {}
}

var registryConfiguration = [
  {
    server: acrLoginServer
    identity: acrPullIdentity.id
  }
]

var databaseSecret = {
  name: 'database-connection'
  keyVaultUrl: sql.outputs.connectionSecretUri
  identity: workloadIdentity.id
}

var apiSecrets = isDev
  ? [
      databaseSecret
      {
        name: 'dev-jwt-signing-key'
        keyVaultUrl: '${keyVaultBaseUrl}/dev-jwt-signing-key'
        identity: workloadIdentity.id
      }
    ]
  : [
      databaseSecret
      {
        name: 'microsoft365-client-secret'
        keyVaultUrl: '${keyVaultBaseUrl}/microsoft365-client-secret'
        identity: workloadIdentity.id
      }
      {
        name: 'openai-api-key'
        keyVaultUrl: '${keyVaultBaseUrl}/openai-api-key'
        identity: workloadIdentity.id
      }
      {
        name: 'azure-search-api-key'
        keyVaultUrl: '${keyVaultBaseUrl}/azure-search-api-key'
        identity: workloadIdentity.id
      }
    ]

var commonApiEnvironmentVariables = [
  {
    name: 'ASPNETCORE_ENVIRONMENT'
    value: isDev ? 'Dev' : 'Certif'
  }
  {
    name: 'ConnectionStrings__AssistantCoreDatabase'
    secretRef: 'database-connection'
  }
  {
    name: 'Cors__AllowedOrigins__0'
    value: spaBaseUrl
  }
  {
    name: 'Microsoft365__ConsentCallbackUrl'
    value: '${apiBaseUrl}/api/microsoft365/consent/callback'
  }
  {
    name: 'Microsoft365__ConsentSuccessRedirectUrl'
    value: '${spaBaseUrl}/microsoft365/consent/success'
  }
  {
    name: 'Microsoft365__ConsentErrorRedirectUrl'
    value: '${spaBaseUrl}/microsoft365/consent/error'
  }
  {
    name: 'Microsoft365__WebhookBaseUrl'
    value: apiBaseUrl
  }
]

var devApiEnvironmentVariables = [
  {
    name: 'Authentication__LocalJwt__SigningKey'
    secretRef: 'dev-jwt-signing-key'
  }
  {
    name: 'Microsoft365__AuthorityBaseUrl'
    value: '${wiremockPublicBaseUrl}/microsoft'
  }
  {
    name: 'Microsoft365__GraphBaseUrl'
    value: '${wiremockPublicBaseUrl}/graph'
  }
  {
    name: 'Microsoft365__EmbeddingEndpoint'
    value: '${wiremockPublicBaseUrl}/openai/v1'
  }
  {
    name: 'AzureSearch__Endpoint'
    value: '${wiremockPublicBaseUrl}/azure-search'
  }
  {
    name: 'AiModels__Providers__OpenAI__Endpoint'
    value: '${wiremockPublicBaseUrl}/openai/v1'
  }
]

var certifApiEnvironmentVariables = [
  {
    name: 'AzureAd__TenantId'
    value: azureAdTenantId
  }
  {
    name: 'AzureAd__ClientId'
    value: azureAdApiClientId
  }
  {
    name: 'AzureAd__Audience'
    value: azureAdApiClientId
  }
  {
    name: 'Microsoft365__ClientId'
    value: microsoft365ClientId
  }
  {
    name: 'Microsoft365__ClientSecret'
    secretRef: 'microsoft365-client-secret'
  }
  {
    name: 'Microsoft365__EmbeddingApiKey'
    secretRef: 'openai-api-key'
  }
  {
    name: 'AzureSearch__Endpoint'
    value: azureSearchEndpoint
  }
  {
    name: 'AzureSearch__IndexName'
    value: azureSearchIndexName
  }
  {
    name: 'AzureSearch__ApiKey'
    secretRef: 'azure-search-api-key'
  }
  {
    name: 'AiModels__Providers__OpenAI__ApiKey'
    secretRef: 'openai-api-key'
  }
]

resource api 'Microsoft.App/containerApps@2024-03-01' = {
  name: apiAppName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: managedIdentities
  }
  properties: {
    managedEnvironmentId: containerEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        allowInsecure: false
        targetPort: 8080
        transport: 'auto'
        customDomains: isDev ? [
          {
            name: devBffCustomDomain
            certificateId: devBffCertificateId
            bindingType: 'SniEnabled'
          }
        ] : []
      }
      registries: registryConfiguration
      secrets: apiSecrets
    }
    template: {
      containers: [
        {
          name: 'api'
          image: '${acrLoginServer}/assistant-api:${backendImageTag}'
          env: concat(commonApiEnvironmentVariables, isDev ? devApiEnvironmentVariables : certifApiEnvironmentVariables)
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 10
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 10
              periodSeconds: 15
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
      }
    }
  }
  dependsOn: [acrPullRole, keyVaultSecretsUser]
}

var workerSecrets = isDev
  ? [databaseSecret]
  : [
      databaseSecret
      {
        name: 'microsoft365-client-secret'
        keyVaultUrl: '${keyVaultBaseUrl}/microsoft365-client-secret'
        identity: workloadIdentity.id
      }
      {
        name: 'openai-api-key'
        keyVaultUrl: '${keyVaultBaseUrl}/openai-api-key'
        identity: workloadIdentity.id
      }
      {
        name: 'azure-search-api-key'
        keyVaultUrl: '${keyVaultBaseUrl}/azure-search-api-key'
        identity: workloadIdentity.id
      }
    ]

var commonWorkerEnvironmentVariables = [
  {
    name: 'DOTNET_ENVIRONMENT'
    value: isDev ? 'Dev' : 'Certif'
  }
  {
    name: 'ConnectionStrings__AssistantCoreDatabase'
    secretRef: 'database-connection'
  }
]

var devWorkerEnvironmentVariables = [
  {
    name: 'Microsoft365__AuthorityBaseUrl'
    value: '${wiremockInternalBaseUrl}/microsoft'
  }
  {
    name: 'Microsoft365__GraphBaseUrl'
    value: '${wiremockInternalBaseUrl}/graph'
  }
  {
    name: 'Microsoft365__EmbeddingEndpoint'
    value: '${wiremockInternalBaseUrl}/openai/v1'
  }
  {
    name: 'AzureSearch__Endpoint'
    value: '${wiremockInternalBaseUrl}/azure-search'
  }
]

var certifWorkerEnvironmentVariables = [
  {
    name: 'Microsoft365__ClientId'
    value: microsoft365ClientId
  }
  {
    name: 'Microsoft365__ClientSecret'
    secretRef: 'microsoft365-client-secret'
  }
  {
    name: 'Microsoft365__EmbeddingApiKey'
    secretRef: 'openai-api-key'
  }
  {
    name: 'AzureSearch__Endpoint'
    value: azureSearchEndpoint
  }
  {
    name: 'AzureSearch__IndexName'
    value: azureSearchIndexName
  }
  {
    name: 'AzureSearch__ApiKey'
    secretRef: 'azure-search-api-key'
  }
]

resource worker 'Microsoft.App/containerApps@2024-03-01' = {
  name: workerAppName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: managedIdentities
  }
  properties: {
    managedEnvironmentId: containerEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      registries: registryConfiguration
      secrets: workerSecrets
    }
    template: {
      containers: [
        {
          name: 'worker'
          image: '${acrLoginServer}/assistant-worker:${backendImageTag}'
          env: concat(commonWorkerEnvironmentVariables, isDev ? devWorkerEnvironmentVariables : certifWorkerEnvironmentVariables)
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
      scale: {
        // The DEV deployment starts the worker temporarily.
        // CERTIF remains controlled by the explicit start/stop workflow.
        minReplicas: 0
        maxReplicas: 1
      }
    }
  }
  dependsOn: [acrPullRole, keyVaultSecretsUser]
}

resource spa 'Microsoft.App/containerApps@2024-03-01' = {
  name: spaAppName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${acrPullIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        allowInsecure: false
        targetPort: 8080
        transport: 'auto'
        customDomains: isDev ? [
          {
            name: devSpaCustomDomain
            certificateId: devSpaCertificateId
            bindingType: 'SniEnabled'
          }
        ] : []
      }
      registries: registryConfiguration
    }
    template: {
      containers: [
        {
          name: 'spa'
          image: '${acrLoginServer}/assistant-spa:${spaImageTag}'
          env: [
            {
              name: 'SPA_API_BASE_URL'
              value: apiBaseUrl
            }
            {
              name: 'SPA_AUTHENTICATION_MODE'
              value: isDev ? 'LocalJwt' : 'MicrosoftEntra'
            }
            {
              name: 'SPA_LAUNCH_MODE'
              value: isDev ? 'Dev' : 'Certification'
            }
            {
              name: 'SPA_AUTHENTICATION_URL'
              value: isDev ? '${wiremockPublicBaseUrl}/local-auth/token' : '${apiBaseUrl}/local-auth/token'
            }
            {
              name: 'SPA_ENTRA_CLIENT_ID'
              value: spaEntraClientId
            }
            {
              name: 'SPA_ENTRA_AUTHORITY'
              value: '${environment().authentication.loginEndpoint}${azureAdTenantId}'
            }
            {
              name: 'SPA_ENTRA_SCOPE'
              value: 'api://${azureAdApiClientId}/access_as_user'
            }
          ]
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
      }
    }
  }
  dependsOn: [acrPullRole]
}

resource wiremock 'Microsoft.App/containerApps@2024-03-01' = if (isDev) {
  name: wiremockAppName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: managedIdentities
  }
  properties: {
    managedEnvironmentId: containerEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        allowInsecure: false
        targetPort: 8080
        transport: 'auto'
      }
      registries: registryConfiguration
      secrets: [
        {
          name: 'dev-jwt-signing-key'
          keyVaultUrl: '${keyVaultBaseUrl}/dev-jwt-signing-key'
          identity: workloadIdentity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'wiremock'
          image: '${acrLoginServer}/assistant-wiremock:${backendImageTag}'
          env: [
            {
              name: 'DEV_JWT_SIGNING_KEY'
              secretRef: 'dev-jwt-signing-key'
            }
            {
              name: 'SPA_ORIGIN'
              value: spaBaseUrl
            }
          ]
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
      }
    }
  }
  dependsOn: [acrPullRole, keyVaultSecretsUser]
}

resource sqlpad 'Microsoft.App/containerApps@2024-03-01' = {
  name: sqlpadAppName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${workloadIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        allowInsecure: false
        targetPort: 3000
        transport: 'auto'
      }
      secrets: [
        {
          name: 'sql-admin-password'
          keyVaultUrl: '${keyVaultBaseUrl}/sql-admin-password'
          identity: workloadIdentity.id
        }
        {
          name: 'sqlpad-entra-client-secret'
          keyVaultUrl: '${keyVaultBaseUrl}/sqlpad-entra-client-secret'
          identity: workloadIdentity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'sqlpad'
          image: 'sqlpad/sqlpad:7.5.7'
          env: [
            {
              name: 'SQLPAD_AUTH_DISABLED'
              value: 'true'
            }
            {
              name: 'SQLPAD_AUTH_DISABLED_DEFAULT_ROLE'
              value: 'admin'
            }
            {
              name: 'SQLPAD_APP_LOG_LEVEL'
              value: 'info'
            }
            {
              name: 'SQLPAD_DB_PATH'
              value: '/var/lib/sqlpad/db'
            }
            {
              name: 'SQLPAD_CONNECTIONS__assistantcore__name'
              value: 'AssistantCoreDb ${toUpper(environmentName)}'
            }
            {
              name: 'SQLPAD_CONNECTIONS__assistantcore__driver'
              value: 'sqlserver'
            }
            {
              name: 'SQLPAD_CONNECTIONS__assistantcore__host'
              value: sql.outputs.serverFqdn
            }
            {
              name: 'SQLPAD_CONNECTIONS__assistantcore__port'
              value: '1433'
            }
            {
              name: 'SQLPAD_CONNECTIONS__assistantcore__database'
              value: sql.outputs.databaseName
            }
            {
              name: 'SQLPAD_CONNECTIONS__assistantcore__username'
              value: sqlAdministratorLogin
            }
            {
              name: 'SQLPAD_CONNECTIONS__assistantcore__password'
              secretRef: 'sql-admin-password'
            }
            {
              name: 'SQLPAD_CONNECTIONS__assistantcore__encrypt'
              value: 'true'
            }
            {
              name: 'SQLPAD_CONNECTIONS__assistantcore__trustServerCertificate'
              value: 'false'
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          volumeMounts: [
            {
              volumeName: 'sqlpad-data'
              mountPath: '/var/lib/sqlpad'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
      }
      volumes: [
        {
          name: 'sqlpad-data'
          storageName: sqlpadEnvironmentStorage.name
          storageType: 'AzureFile'
        }
      ]
    }
  }
  dependsOn: [keyVaultSecretsUser]
}

resource sqlpadAuthentication 'Microsoft.App/containerApps/authConfigs@2024-03-01' = {
  parent: sqlpad
  name: 'current'
  properties: {
    platform: {
      enabled: true
      runtimeVersion: '~1'
    }
    globalValidation: {
      unauthenticatedClientAction: 'RedirectToLoginPage'
      redirectToProvider: 'azureactivedirectory'
    }
    httpSettings: {
      requireHttps: true
    }
    identityProviders: {
      azureActiveDirectory: {
        enabled: true
        registration: {
          clientId: sqlpadEntraClientId
          clientSecretSettingName: 'sqlpad-entra-client-secret'
          openIdIssuer: '${environment().authentication.loginEndpoint}${sqlpadEntraTenantId}/v2.0'
        }
        validation: {
          allowedAudiences: [
            sqlpadEntraClientId
            'api://${sqlpadEntraClientId}'
          ]
          defaultAuthorizationPolicy: {
            allowedPrincipals: {
              groups: [sqlpadAllowedGroupObjectId]
            }
          }
        }
      }
    }
  }
}

resource migrationsJob 'Microsoft.App/jobs@2024-03-01' = {
  name: migrationsJobName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: managedIdentities
  }
  properties: {
    environmentId: containerEnvironment.id
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 1800
      replicaRetryLimit: 0
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
      registries: registryConfiguration
      secrets: [
        {
          name: 'sql-admin-password'
          keyVaultUrl: '${keyVaultBaseUrl}/sql-admin-password'
          identity: workloadIdentity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'migrations'
          image: '${acrLoginServer}/assistant-migrations:${backendImageTag}'
          args: ['migrate']
          env: [
            {
              name: 'FLYWAY_URL'
              value: 'jdbc:sqlserver://${sql.outputs.serverFqdn}:1433;databaseName=${sql.outputs.databaseName};encrypt=true;trustServerCertificate=false;hostNameInCertificate=*.database.windows.net;loginTimeout=30'
            }
            {
              name: 'FLYWAY_USER'
              value: sqlAdministratorLogin
            }
            {
              name: 'FLYWAY_PASSWORD'
              secretRef: 'sql-admin-password'
            }
            {
              name: 'FLYWAY_CONNECT_RETRIES'
              value: '60'
            }
            {
              name: 'FLYWAY_BASELINE_ON_MIGRATE'
              value: 'true'
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
    }
  }
  dependsOn: [acrPullRole, keyVaultSecretsUser]
}

output apiName string = api.name
output apiUrl string = apiBaseUrl
output spaName string = spa.name
output spaUrl string = spaBaseUrl
output workerName string = worker.name
output wiremockName string = isDev ? wiremockAppName : ''
output sqlpadName string = sqlpad.name
output sqlpadUrl string = sqlpadBaseUrl
output migrationsJobName string = migrationsJob.name
output keyVaultName string = keyVault.name
output sqlServerName string = sql.outputs.serverName
output sqlDatabaseName string = sql.outputs.databaseName
