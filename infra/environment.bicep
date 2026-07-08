// Log Analytics + Container Apps managed environment + Azure Files environment storage
// (used as the durable volume for the PostgreSQL container app).
param environmentName string
param location string = resourceGroup().location
param tags object = {}

@description('Name of the environment storage entry the postgres app mounts.')
param pgStorageName string = 'pgdata'

@description('Azure Files share name for postgres data.')
param fileShareName string = 'pgdata'

var resourceToken = uniqueString(resourceGroup().id, environmentName)
var storageAccountName = toLower(replace('st${environmentName}${resourceToken}', '-', ''))

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'law-${environmentName}'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: length(storageAccountName) > 24 ? substring(storageAccountName, 0, 24) : storageAccountName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

resource fileService 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource pgShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  parent: fileService
  name: fileShareName
  properties: {
    // Small, cheap share for a low-traffic app database.
    shareQuota: 16
    enabledProtocols: 'SMB'
  }
}

resource managedEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-${environmentName}'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

resource pgEnvStorage 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  parent: managedEnvironment
  name: pgStorageName
  properties: {
    azureFile: {
      accountName: storageAccount.name
      accountKey: storageAccount.listKeys().keys[0].value
      shareName: fileShareName
      accessMode: 'ReadWrite'
    }
  }
}

output environmentId string = managedEnvironment.id
output defaultDomain string = managedEnvironment.properties.defaultDomain
output pgStorageName string = pgEnvStorage.name
output storageAccountName string = storageAccount.name
