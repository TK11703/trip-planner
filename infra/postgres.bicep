// Azure Database for PostgreSQL Flexible Server, Burstable B1ms.
// Replaces the self-managed postgres container app: cheaper at this scale, point-in-time
// restore instead of a nightly dump, and Entra authentication so the API needs no password.
param location string = resourceGroup().location
param tags object = {}

@minLength(1)
@description('Naming seed, from AZURE_ENV_NAME.')
param environmentName string

@description('Compute SKU. Burstable is sized for a low-traffic app; it supports neither HA nor read replicas.')
param skuName string = 'Standard_B1ms'

@description('Provisioned storage. 32 GB is the service minimum.')
param storageSizeGB int = 32

@description('Major PostgreSQL version.')
param postgresVersion string = '16'

@description('Database created for the application.')
param databaseName string = 'tripplanner'

@description('Days of point-in-time restore history. 7 is the service minimum.')
@minValue(7)
@maxValue(35)
param backupRetentionDays int = 7

@description('Administrator login, used for schema bootstrap and break-glass only.')
param administratorLogin string = 'pgadmin'

@description('Administrator password. Stored in Key Vault; never surfaced as an output.')
@secure()
param administratorPassword string

@description('Tenant that owns the Entra principals allowed to authenticate.')
param entraTenantId string

@description('Object id of the Entra administrator. Empty skips the assignment.')
param entraAdminObjectId string = ''

@description('Display name of the Entra administrator, shown in the server configuration.')
param entraAdminPrincipalName string = ''

@allowed([
  'User'
  'Group'
  'ServicePrincipal'
])
@description('Principal type of the Entra administrator.')
param entraAdminPrincipalType string = 'User'

var resourceToken = uniqueString(resourceGroup().id, environmentName)
var serverName = take(toLower('psql-${environmentName}-${resourceToken}'), 63)

resource server 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: serverName
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: 'Burstable'
  }
  properties: {
    version: postgresVersion
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorPassword
    storage: {
      storageSizeGB: storageSizeGB
      // Running out of disk corrupts availability far more expensively than the ~$0.12
      // per extra GB-month that growth costs.
      autoGrow: 'Enabled'
    }
    backup: {
      backupRetentionDays: backupRetentionDays
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      // Burstable does not offer zone-redundant HA. The General Purpose tier that does
      // costs roughly five times this server before HA doubles it again.
      mode: 'Disabled'
    }
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      // Retained for the schema bootstrap and for break-glass access when Entra is
      // unavailable. The application itself authenticates with its managed identity.
      passwordAuth: 'Enabled'
      tenantId: entraTenantId
    }
    network: {
      // No VNet in this topology, so the server keeps a public endpoint reachable only
      // from Azure and only by principals that pass authentication.
      publicNetworkAccess: 'Enabled'
    }
  }
}

// Flexible Server serialises child writes; deploying these in parallel fails with a
// conflict, so each one waits for the previous.
resource allowAzureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: server
  // The all-zero range is the portal's "allow Azure services" rule. It does not open the
  // server to the internet; it admits Azure-internal callers, which is how the Container
  // Apps environment reaches it without a VNet.
  name: 'AllowAllAzureServicesAndResourcesWithinAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: server
  name: databaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
  dependsOn: [
    allowAzureServices
  ]
}

// The Entra admin is the only principal that can create database roles for the workload
// identities, which is a data-plane operation the runbook performs after deployment.
resource entraAdmin 'Microsoft.DBforPostgreSQL/flexibleServers/administrators@2024-08-01' = if (!empty(entraAdminObjectId)) {
  parent: server
  name: entraAdminObjectId
  properties: {
    principalType: entraAdminPrincipalType
    principalName: empty(entraAdminPrincipalName) ? entraAdminObjectId : entraAdminPrincipalName
    tenantId: entraTenantId
  }
  dependsOn: [
    database
  ]
}

output name string = server.name
output fqdn string = server.properties.fullyQualifiedDomainName
output databaseName string = database.name
output administratorLogin string = administratorLogin
