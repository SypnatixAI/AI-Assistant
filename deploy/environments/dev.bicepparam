using '../infra/main.bicep'

param location = 'canadacentral'
param environmentName = 'dev'
param nameSuffix = 'replace'
param sharedResourceGroupName = 'rg-assistant-shared'
param backendImageTag = 'sha-replace'
param spaImageTag = 'sha-replace'

