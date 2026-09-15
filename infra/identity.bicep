// Three user-assigned managed identities, one per trust boundary, so a compromised
// workload cannot reach another workload's secrets. Role assignments live in rbac.bicep.
param environmentName string
param location string = resourceGroup().location
param tags object = {}

resource acrPullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${environmentName}-acrpull'
  location: location
  tags: tags
}

resource webIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${environmentName}-web'
  location: location
  tags: tags
}

resource apiIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${environmentName}-api'
  location: location
  tags: tags
}

output acrPull object = {
  id: acrPullIdentity.id
  principalId: acrPullIdentity.properties.principalId
  clientId: acrPullIdentity.properties.clientId
}

output web object = {
  id: webIdentity.id
  principalId: webIdentity.properties.principalId
  clientId: webIdentity.properties.clientId
}

output api object = {
  id: apiIdentity.id
  principalId: apiIdentity.properties.principalId
  clientId: apiIdentity.properties.clientId
  name: apiIdentity.name
}
