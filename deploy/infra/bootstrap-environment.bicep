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

param tags object = {
  application: 'assistant'
  environment: environmentName
  managedBy: 'bicep'
}

var keyVaultName = 'kv-assistant-${environmentName}-${nameSuffix}'

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    tenantId: tenant().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: true
    publicNetworkAccess: 'Enabled'
    softDeleteRetentionInDays: 7
    enablePurgeProtection: true
  }
}

output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
