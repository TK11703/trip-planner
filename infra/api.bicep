// API container app — internal ingress only. Reaches postgres over the environment's
// internal network; applies schema migrations on startup (RunDatabaseMigrations=true).
param location string = resourceGroup().location
param tags object = {}

param environmentId string
param containerImage string
param identityId string
param registryLoginServer string

@description('PostgreSQL connection string targeting the internal postgres app.')
@secure()
param connectionString string

param entraInstance string = environment().authentication.loginEndpoint
param entraTenantId string
param entraApiClientId string
param entraApiAudience string

var appName = 'api'

resource api 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  tags: union(tags, { 'azd-service-name': 'api' })
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: registryLoginServer
          identity: identityId
        }
      ]
      secrets: [
        {
          name: 'db-connection'
          value: connectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: appName
          image: containerImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'ConnectionStrings__tripplanner'
              secretRef: 'db-connection'
            }
            {
              name: 'RunDatabaseMigrations'
              value: 'true'
            }
            {
              name: 'AzureEntra__Instance'
              value: entraInstance
            }
            {
              name: 'AzureEntra__TenantId'
              value: entraTenantId
            }
            {
              name: 'AzureEntra__ClientId'
              value: entraApiClientId
            }
            {
              name: 'AzureEntra__Audience'
              value: entraApiAudience
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 3
      }
    }
  }
}

output name string = api.name
output fqdn string = api.properties.configuration.ingress.fqdn
