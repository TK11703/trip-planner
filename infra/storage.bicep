// Storage account backing a single concern: the private `dataprotection` blob container
// holding the ASP.NET data-protection key ring. Database durability belongs to
// PostgreSQL Flexible Server, which takes its own point-in-time backups.
@minLength(1)
param environmentName string
param location string = resourceGroup().location
param tags object = {}

@description('Blob container holding the ASP.NET data-protection key ring.')
param dataProtectionContainerName string = 'dataprotection'

var resourceToken = uniqueString(resourceGroup().id, environmentName)
var storageAccountName = take(toLower(replace('st${environmentName}${resourceToken}', '-', '')), 24)

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  // environmentName is @minLength(1) and resourceToken is a 13-char hash, so the name is
  // always well over the 3-character minimum; the checker just cannot prove take()'s floor.
  #disable-next-line BCP334
  name: storageAccountName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
    // Nothing mounts SMB any more, so every caller can use managed identity and the
    // account keys can stay switched off entirely.
    allowSharedKeyAccess: false
    // No VNet in this topology: private endpoints would add roughly the cost of the rest
    // of the environment combined. Exposure is bounded by RBAC-only data-plane auth,
    // no public blob access, and TLS 1.2.
    publicNetworkAccess: 'Enabled'
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource dataProtectionContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: dataProtectionContainerName
  properties: {
    publicAccess: 'None'
  }
}

output id string = storageAccount.id
output name string = storageAccount.name
output blobEndpoint string = storageAccount.properties.primaryEndpoints.blob
output dataProtectionContainerName string = dataProtectionContainer.name
output dataProtectionContainerUri string = '${storageAccount.properties.primaryEndpoints.blob}${dataProtectionContainer.name}'
