// The application database on the shared PostgreSQL Flexible Server. The server, its
// firewall, Entra administrators, and the azure.extensions allow-list (pgcrypto,vector) are
// platform-owned and deliberately not declared here, so this deployment cannot reset them.
param serverName string

@description('Database created for the application.')
param databaseName string = 'tripplanner'

resource server 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' existing = {
  name: serverName
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: server
  name: databaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

output name string = server.name
output fqdn string = server.properties.fullyQualifiedDomainName
output databaseName string = database.name
