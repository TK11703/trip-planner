// API container app — public ingress so the external email-ingestion relay can reach it.
// Every route is behind Entra; /api/email-ingestion/messages additionally requires the
// EmailIngestion.Relay app role. Reaches postgres over the environment's internal network;
// applies schema migrations on startup (RunDatabaseMigrations=true).
param location string = resourceGroup().location
param tags object = {}

param environmentId string
param containerImage string
param registryLoginServer string

@description('Identity used to pull images from the container registry.')
param acrPullIdentityId string

@description('Runtime identity used for Key Vault, Azure OpenAI, and Graph access.')
param apiIdentityId string

@description('Client id of the runtime identity, so the Azure SDK selects the right UAMI.')
param apiIdentityClientId string

@description('Key Vault base URI, for example https://kv-example.vault.azure.net/.')
param keyVaultUri string

@description('Key Vault secret holding the full PostgreSQL connection string.')
param postgresConnectionSecretName string

@description('Client id (unique id) of the Azure Maps account. Empty disables place lookup.')
param azureMapsClientId string = ''

@description('Azure Maps endpoint. Blank uses the shared https://atlas.microsoft.com/ endpoint.')
param azureMapsEndpoint string = ''

param entraInstance string = environment().authentication.loginEndpoint
param entraTenantId string
param entraApiClientId string
param entraApiAudience string

param azureOpenAiEndpoint string
param azureOpenAiDeploymentName string

@description('Release identifier recorded against applied migrations and telemetry.')
param releaseId string = ''

@description('Container Apps environment default domain, used to reach the managed Aspire dashboard.')
param environmentDefaultDomain string

@description('Container app resource name.')
param appName string

var hasAzureMaps = !empty(azureMapsClientId)

// The managed Aspire dashboard ingests OTLP over gRPC on the environment-internal
// endpoint, so telemetry never leaves the managed environment.
var otlpEndpoint = 'http://aspire-dashboard.ext.${environmentDefaultDomain}:4317'

// Secrets are Key Vault references, not literals: the value never enters the template,
// the deployment history, or the container app resource.
var secrets = [
  {
    name: 'db-connection'
    keyVaultUrl: '${keyVaultUri}secrets/${postgresConnectionSecretName}'
    identity: apiIdentityId
  }
]

var baseEnv = [
  {
    name: 'ConnectionStrings__tripplanner'
    secretRef: 'db-connection'
  }
  {
    name: 'RunDatabaseMigrations'
    value: 'true'
  }
  {
    name: 'ReleaseId'
    value: releaseId
  }
  {
    // Selects the user-assigned identity for DefaultAzureCredential.
    name: 'AZURE_CLIENT_ID'
    value: apiIdentityClientId
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
  {
    name: 'AzureOpenAI__Endpoint'
    value: azureOpenAiEndpoint
  }
  {
    name: 'AzureOpenAI__DeploymentName'
    value: azureOpenAiDeploymentName
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

// Azure Maps authenticates with the API's managed identity, so only the account selector
// and endpoint are configuration — there is no key to store or rotate.
var mapsEnv = hasAzureMaps
  ? [
      {
        name: 'AzureMaps__ClientId'
        value: azureMapsClientId
      }
      {
        name: 'AzureMaps__Endpoint'
        value: azureMapsEndpoint
      }
    ]
  : []

resource api 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  tags: union(tags, { 'azd-service-name': 'api' })
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${acrPullIdentityId}': {}
      '${apiIdentityId}': {}
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
      secrets: secrets
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
          env: concat(baseEnv, mapsEnv)
          probes: [
            {
              // Wide failure window: a scale-from-zero start also runs serialized
              // migrations, and the replica must not be killed while they apply.
              type: 'Startup'
              httpGet: {
                path: '/alive'
                port: 8080
              }
              periodSeconds: 5
              timeoutSeconds: 3
              failureThreshold: 60
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
              // Readiness covers migrations, PostgreSQL, and Azure OpenAI configuration,
              // so a dependency failure removes the replica from traffic without a restart.
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

output name string = api.name
output fqdn string = api.properties.configuration.ingress.fqdn
