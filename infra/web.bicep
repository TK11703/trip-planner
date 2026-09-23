// Web (Blazor) container app — external HTTPS ingress. Reaches the API over the
// environment's internal network via .NET service discovery config keys.
param location string = resourceGroup().location
param tags object = {}

param environmentId string
param containerImage string
param registryLoginServer string

@description('Identity used to pull images from the container registry.')
param acrPullIdentityId string

@description('Runtime identity used for Key Vault and the data-protection key ring.')
param webIdentityId string

@description('Client id of the runtime identity, so the Azure SDK selects the right UAMI.')
param webIdentityClientId string

@description('Blob URI the ASP.NET data-protection key ring is persisted to.')
param dataProtectionBlobUri string

@description('Key Vault key URI used to encrypt the data-protection key ring.')
param dataProtectionKeyUri string

@description('Ingress FQDN of the API container app.')
param apiFqdn string

param entraInstance string = environment().authentication.loginEndpoint
param entraTenantId string
param entraWebClientId string
param entraDomain string = ''

@description('Scope requested when calling the API on behalf of the signed-in user.')
param entraApiScope string

@description('Release identifier surfaced in telemetry.')
param releaseId string = ''

@description('Default domain of the Container Apps environment.')
param environmentDefaultDomain string

@description('Container app resource name.')
param appName string

// Ingress FQDN is deterministic from the app name and the environment domain, so the
// public base URI can be set without a second deployment pass.
var publicBaseUri = 'https://${appName}.${environmentDefaultDomain}'

// The managed Aspire dashboard ingests OTLP over gRPC on the environment-internal
// endpoint, so telemetry never leaves the managed environment.
var otlpEndpoint = 'http://aspire-dashboard.ext.${environmentDefaultDomain}:4317'

resource web 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  tags: union(tags, { 'azd-service-name': 'web' })
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${acrPullIdentityId}': {}
      '${webIdentityId}': {}
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
          identity: acrPullIdentityId
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
              // The key stays `api` because the web app resolves `https+http://api`. The value
              // must be the full internal FQDN: the bare app name is not what the ingress
              // certificate covers, so an HTTPS call to it never completes the handshake.
              name: 'services__api__http__0'
              value: 'http://${apiFqdn}'
            }
            {
              name: 'services__api__https__0'
              value: 'https://${apiFqdn}'
            }
            {
              name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
              value: 'true'
            }
            {
              name: 'ReleaseId'
              value: releaseId
            }
            {
              // Selects the user-assigned identity for DefaultAzureCredential.
              name: 'AZURE_CLIENT_ID'
              value: webIdentityClientId
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
            {
              // The web identity is registered as a federated credential on the app
              // registration, so MSAL signs a client assertion with it instead of
              // presenting a secret. Nothing to store, rotate, or leak into a log.
              name: 'AzureEntra__ClientCredentials__0__SourceType'
              value: 'SignedAssertionFromManagedIdentity'
            }
            {
              name: 'AzureEntra__ClientCredentials__0__ManagedIdentityClientId'
              value: webIdentityClientId
            }
            {
              // Array setting: .NET binds indexed keys to AzureEntra:ApiScopes[0].
              name: 'AzureEntra__ApiScopes__0'
              value: entraApiScope
            }
            {
              // Replicas are ephemeral and scale to zero, so the key ring lives in blob
              // storage and is encrypted with a Key Vault key.
              name: 'DataProtection__BlobUri'
              value: dataProtectionBlobUri
            }
            {
              name: 'DataProtection__KeyVaultKeyUri'
              value: dataProtectionKeyUri
            }
            {
              name: 'PublicBaseUri'
              value: publicBaseUri
            }
            {
              name: 'OTEL_EXPORTER_OTLP_ENDPOINT'
              value: otlpEndpoint
            }
            {
              name: 'OTEL_EXPORTER_OTLP_PROTOCOL'
              value: 'grpc'
            }
            {
              name: 'OTEL_SERVICE_NAME'
              value: appName
            }
          ]
          probes: [
            {
              type: 'Startup'
              httpGet: {
                path: '/alive'
                port: 8080
              }
              periodSeconds: 5
              timeoutSeconds: 3
              failureThreshold: 30
            }
            {
              type: 'Liveness'
              httpGet: {
                path: '/alive'
                port: 8080
              }
              periodSeconds: 30
              timeoutSeconds: 3
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              periodSeconds: 10
              timeoutSeconds: 5
              failureThreshold: 3
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 3
        rules: [
          {
            name: 'http-scale'
            http: {
              metadata: {
                concurrentRequests: '20'
              }
            }
          }
        ]
      }
    }
  }
}

output name string = web.name
output url string = 'https://${web.properties.configuration.ingress.fqdn}'
