// Grants an identity a role on an Azure OpenAI account that may live in another
// resource group. Separated from rbac.bicep because a role assignment scope must be
// resolved through a module when it crosses resource groups.
param accountName string
param principalId string
param roleDefinitionId string

resource account 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = {
  name: accountName
}

resource assignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(account.id, principalId, roleDefinitionId)
  scope: account
  properties: {
    roleDefinitionId: roleDefinitionId
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
