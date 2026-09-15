// Azure Container Registry (Basic) — stores the web and api images.
// AcrPull is granted in rbac.bicep so all role assignments stay in one place.
@description('Globally-unique-ish name seed.')
param environmentName string
param location string = resourceGroup().location
param tags object = {}

var resourceToken = uniqueString(resourceGroup().id, environmentName)
var registryName = toLower(replace('acr${environmentName}${resourceToken}', '-', ''))

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

output loginServer string = registry.properties.loginServer
output name string = registry.name
output id string = registry.id
