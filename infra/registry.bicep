// Azure Container Registry (Basic) — stores the web and api images.
// Optionally grants AcrPull to a managed identity principal when provided.
@description('Globally-unique-ish name seed.')
param environmentName string
param location string = resourceGroup().location
param tags object = {}

@description('Principal id of the managed identity to grant AcrPull. Empty = skip role assignment.')
param principalId string = ''

var resourceToken = uniqueString(resourceGroup().id, environmentName)
var registryName = toLower(replace('acr${environmentName}${resourceToken}', '-', ''))
var acrPullRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')

resource registry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: length(registryName) > 50 ? substring(registryName, 0, 50) : registryName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
    anonymousPullEnabled: false
  }
}

resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(principalId)) {
  name: guid(registry.id, principalId, acrPullRoleId)
  scope: registry
  properties: {
    roleDefinitionId: acrPullRoleId
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

output loginServer string = registry.properties.loginServer
output name string = registry.name
output id string = registry.id
