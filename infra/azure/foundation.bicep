targetScope = 'resourceGroup'

@description('Short lowercase workload prefix, for example startpack.')
@minLength(3)
@maxLength(20)
param workloadName string = 'startpack'

@allowed([
  'staging'
  'production'
])
param environmentName string

param location string = resourceGroup().location

var compactName = toLower(replace('${workloadName}${environmentName}', '-', ''))
var registryName = take('${compactName}${uniqueString(resourceGroup().id)}', 50)

resource registry 'Microsoft.ContainerRegistry/registries@2025-04-01' = {
  name: registryName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
    anonymousPullEnabled: false
    publicNetworkAccess: 'Enabled'
    zoneRedundancy: 'Disabled'
  }
}

output registryName string = registry.name
output registryLoginServer string = registry.properties.loginServer
