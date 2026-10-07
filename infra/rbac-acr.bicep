// Grants an identity a role on a container registry that may live in another resource group
// (the shared registry in rg-common). Cross-group role assignment scopes need a module.
param registryName string
param principalId string
param roleDefinitionId string

resource registry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' existing = {
  name: registryName
}

resource assignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, principalId, roleDefinitionId)
  scope: registry
  properties: {
    roleDefinitionId: roleDefinitionId
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
