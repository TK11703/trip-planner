// Least-privilege role assignments, each scoped to the single resource the identity needs.
// No identity receives a subscription-level Owner or Contributor role.
@description('Container registry name that the pull identity may read from.')
param registryName string

@description('Key Vault name holding runtime secrets and the data-protection key.')
param keyVaultName string

@description('Storage account name holding the data-protection key ring.')
param storageAccountName string

@description('Blob container holding the ASP.NET data-protection key ring. Web is scoped to this container only.')
param dataProtectionContainerName string

@description('Resource id of the Azure OpenAI account. Empty = skip the inference role assignment.')
param azureOpenAiResourceId string = ''

@description('Resource id of the Azure Maps account. Empty = skip the search role assignment.')
param azureMapsResourceId string = ''

param acrPullPrincipalId string
param webPrincipalId string
param apiPrincipalId string

var acrPullRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '7f951dda-4ed3-4680-a7ca-43fe172d538d'
)
var keyVaultSecretsUserRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '4633458b-17de-408a-b874-0445c86b69e6'
)
var keyVaultCryptoUserRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '12338af0-0e69-4776-bea7-57ae8d297424'
)
var storageBlobDataContributorRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
)
var cognitiveServicesOpenAiUserRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
)
// Azure Maps Data Reader. The narrower Search and Render Data Reader role does not cover the
// timezone API that email ingestion calls to infer a zone from a booking's location.
var azureMapsDataReaderRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '423170ca-a8f6-4b0f-8487-9e4eb8f49bfa'
)

resource registry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' existing = {
  name: registryName
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

// Container-scoped, not account-scoped. A compromise of the web app should not reach
// anything in the account beyond its own data-protection key ring.
resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' existing = {
  parent: storageAccount
  name: 'default'
}

resource dataProtectionContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' existing = {
  parent: blobService
  name: dataProtectionContainerName
}

// --- Image pull -------------------------------------------------------------

resource acrPullAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, acrPullPrincipalId, acrPullRoleId)
  scope: registry
  properties: {
    roleDefinitionId: acrPullRoleId
    principalId: acrPullPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// --- Web runtime ------------------------------------------------------------

resource webSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, webPrincipalId, keyVaultSecretsUserRoleId)
  scope: vault
  properties: {
    roleDefinitionId: keyVaultSecretsUserRoleId
    principalId: webPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Wrap/unwrap the data-protection key ring.
resource webCryptoUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, webPrincipalId, keyVaultCryptoUserRoleId)
  scope: vault
  properties: {
    roleDefinitionId: keyVaultCryptoUserRoleId
    principalId: webPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource webBlobContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(dataProtectionContainer.id, webPrincipalId, storageBlobDataContributorRoleId)
  scope: dataProtectionContainer
  properties: {
    roleDefinitionId: storageBlobDataContributorRoleId
    principalId: webPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// --- API runtime ------------------------------------------------------------

resource apiSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, apiPrincipalId, keyVaultSecretsUserRoleId)
  scope: vault
  properties: {
    roleDefinitionId: keyVaultSecretsUserRoleId
    principalId: apiPrincipalId
    principalType: 'ServicePrincipal'
  }
}

module apiOpenAi 'rbac-openai.bicep' = if (!empty(azureOpenAiResourceId)) {
  scope: resourceGroup(split(azureOpenAiResourceId, '/')[2], split(azureOpenAiResourceId, '/')[4])
  params: {
    accountName: last(split(azureOpenAiResourceId, '/'))
    principalId: apiPrincipalId
    roleDefinitionId: cognitiveServicesOpenAiUserRoleId
  }
}

module apiMaps 'rbac-maps.bicep' = if (!empty(azureMapsResourceId)) {
  scope: resourceGroup(split(azureMapsResourceId, '/')[2], split(azureMapsResourceId, '/')[4])
  params: {
    accountName: last(split(azureMapsResourceId, '/'))
    principalId: apiPrincipalId
    roleDefinitionId: azureMapsDataReaderRoleId
  }
}
