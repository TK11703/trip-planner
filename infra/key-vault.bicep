// Key Vault holding production runtime credentials and the data-protection wrapping key.
// Azure RBAC only (no access policies), soft delete + purge protection enabled so a
// deleted vault cannot take the production credentials with it.
@minLength(1)
param environmentName string
param location string = resourceGroup().location
param tags object = {}

@description('Object id of the human/service principal running the deployment. Empty = skip the operator role assignment.')
param deployerPrincipalId string = ''

@description('PostgreSQL administrator password. Seeded as a secret; never emitted as an output.')
@secure()
param postgresPassword string

@description('Full PostgreSQL connection string. Seeded so the API reads one Key Vault reference instead of assembling credentials at runtime.')
@secure()
param postgresConnectionString string

var resourceToken = uniqueString(resourceGroup().id, environmentName)
var vaultName = take(toLower(replace('kv-${environmentName}-${resourceToken}', '--', '-')), 24)

var keyVaultSecretsOfficerRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'
)

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: vaultName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
    // Reachable publicly but not readable publicly: the vault is RBAC-only, so every
    // request needs a role assignment on a managed identity. A private endpoint would
    // require a VNet and NAT egress, which this cost posture does not carry.
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

resource postgresPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'postgres-password'
  properties: {
    value: postgresPassword
  }
}

resource postgresConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'postgres-connection-string'
  properties: {
    value: postgresConnectionString
  }
}

// Wraps the ASP.NET data-protection key ring so persisted keys are useless on their own.
resource dataProtectionKey 'Microsoft.KeyVault/vaults/keys@2023-07-01' = {
  parent: vault
  name: 'dataprotection'
  properties: {
    kty: 'RSA'
    keySize: 2048
    keyOps: [
      'wrapKey'
      'unwrapKey'
    ]
  }
}

// Lets the deploying operator rotate secrets via the data plane after provisioning.
resource deployerSecretsOfficer 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(deployerPrincipalId)) {
  name: guid(vault.id, deployerPrincipalId, keyVaultSecretsOfficerRoleId)
  scope: vault
  properties: {
    roleDefinitionId: keyVaultSecretsOfficerRoleId
    principalId: deployerPrincipalId
  }
}

// Only names and non-secret URIs are emitted. Consumers build a secret reference as
// '<uri>secrets/<name>'; the secret values themselves never leave the vault.
output id string = vault.id
output name string = vault.name
output uri string = vault.properties.vaultUri
output dataProtectionKeyUri string = dataProtectionKey.properties.keyUriWithVersion
// These are secret *names*, not values. The linter matches on the identifier text only.
#disable-next-line outputs-should-not-contain-secrets
output postgresPasswordSecretName string = postgresPasswordSecret.name
#disable-next-line outputs-should-not-contain-secrets
output postgresConnectionSecretName string = postgresConnectionSecret.name
