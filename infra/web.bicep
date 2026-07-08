// Web (Blazor) container app — external HTTPS ingress. Reaches the API over the
// environment's internal network via .NET service discovery config keys.
param location string = resourceGroup().location
param tags object = {}

param environmentId string
param containerImage string
param identityId string
param registryLoginServer string

@description('Internal app name of the API to target via service discovery.')
param apiAppName string = 'api'

param entraInstance string = environment().authentication.loginEndpoint
param entraTenantId string
param entraWebClientId string
param entraDomain string = ''

var appName = 'web'

resource web 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  tags: union(tags, { 'azd-service-name': 'web' })
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
        external: true
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
              // .NET service discovery resolves `https+http://api` from these keys.
              name: 'services__${apiAppName}__http__0'
              value: 'http://${apiAppName}'
            }
            {
              name: 'services__${apiAppName}__https__0'
              value: 'https://${apiAppName}'
            }
            {
              name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
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
              value: entraWebClientId
            }
            {
              name: 'AzureEntra__Domain'
              value: entraDomain
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

output name string = web.name
output url string = 'https://${web.properties.configuration.ingress.fqdn}'
