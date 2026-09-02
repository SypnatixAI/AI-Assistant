targetScope = 'resourceGroup'

param location string = resourceGroup().location

@minLength(3)
@maxLength(8)
@description('Globally unique lowercase alphanumeric suffix shared by all environments.')
param nameSuffix string

param tags object = {
  application: 'assistant'
  managedBy: 'bicep'
  scope: 'shared'
}

var acrName = 'acrassistant${nameSuffix}'

resource registry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: acrName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
    anonymousPullEnabled: false
    dataEndpointEnabled: false
    publicNetworkAccess: 'Enabled'
    policies: {
      quarantinePolicy: {
        status: 'disabled'
      }
      retentionPolicy: {
        days: 7
        status: 'disabled'
      }
      trustPolicy: {
        type: 'Notary'
        status: 'disabled'
      }
    }
  }
}

output acrName string = registry.name
output acrLoginServer string = registry.properties.loginServer
output acrResourceId string = registry.id

