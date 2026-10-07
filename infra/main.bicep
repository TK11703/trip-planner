// Root deployment for the Trip Planner production environment.
// Deployed into the shared apps resource group (AZURE_RESOURCE_GROUP, e.g. rg-apps), which
// holds the shared Container Apps environment. App-owned resources that belong with the
// platform (Key Vault, storage, database, image-pull identity) deploy into the platform group.
// Shared servers, registry, environment, and AI/Maps accounts are referenced, never managed.
targetScope = 'resourceGroup'

@description('Naming seed for all resources (from AZURE_ENV_NAME).')
param environmentName string

@description('Region of the apps resource group; must match the shared Container Apps environment.')
param location string = resourceGroup().location

@description('Resource group of the shared platform services (registry, PostgreSQL, Key Vault, storage).')
param platformResourceGroup string = 'rg-platform'

@description('Existing shared Container Apps environment in this resource group.')
param containerAppsEnvironmentName string = 'cae-shared'

@description('Existing Log Analytics workspace backing the shared environment, in this resource group.')
param logAnalyticsWorkspaceName string = 'law-shared'

@description('Existing shared PostgreSQL Flexible Server in the platform resource group.')
param postgresServerName string = 'accpsqlshared'

@description('Shared image-pull identity, created in the registry resource group and attached to every container app.')
param acrPullIdentityName string = 'id-shared-acrpull'

@description('Object id of the principal running the deployment. Used only to grant secret rotation rights.')
param deployerPrincipalId string = ''

@description('Full image reference for the web app, e.g. <acr>.azurecr.io/trip-planner-web:<sha>.')
param webImage string = 'mcr.microsoft.com/k8se/quickstart:latest'

@description('Full image reference for the api app, e.g. <acr>.azurecr.io/trip-planner-api:<sha>.')
param apiImage string = 'mcr.microsoft.com/k8se/quickstart:latest'

@description('Name of the existing shared container registry holding the web and api images.')
param registryName string = 'acccrshared'

@description('Resource group of the existing shared container registry (same subscription).')
param registryResourceGroup string = platformResourceGroup

@description('Administrator password of the shared PostgreSQL server. Stored in Key Vault for break-glass only; never surfaced as an output.')
@secure()
param postgresPassword string

@description('Client id (unique id) of the Azure Maps account. Optional — place lookup degrades gracefully when blank.')
param azureMapsClientId string = ''

@description('Azure Maps endpoint. Blank uses the shared https://atlas.microsoft.com/ endpoint.')
param azureMapsEndpoint string = ''

@description('Resource id of the Azure Maps account, used to scope the search role assignment.')
param azureMapsResourceId string = ''

param entraInstance string = environment().authentication.loginEndpoint
param entraTenantId string
param entraWebClientId string
param entraApiClientId string

@description('API token audience. Defaults to the API client id.')
param entraApiAudience string = entraApiClientId

param entraDomain string = ''

@description('Scope the web app requests when calling the API on behalf of the user. Blank derives <audience>/access_as_user.')
param entraApiScope string = ''

@description('Release identifier (commit SHA) recorded against migrations and telemetry.')
param releaseId string = ''

@description('Azure OpenAI endpoint used by email ingestion parsing.')
param azureOpenAiEndpoint string

@description('Azure OpenAI chat deployment name.')
param azureOpenAiDeploymentName string = ''

@description('Resource id of the Azure OpenAI account, used to scope the inference role assignment.')
param azureOpenAiResourceId string = ''

@description('Existing Foundry/Azure AI account resource id used to scope the chat inference role. Empty disables chat inference.')
param tripChatResourceId string = ''

@description('Existing Foundry project endpoint for trip chat. Empty disables trip chat.')
param tripChatEndpoint string = ''

@description('Existing chat model deployment name.')
param tripChatChatDeploymentName string = ''

@description('Existing embedding model deployment name.')
param tripChatEmbeddingDeploymentName string = ''

@minValue(1)
param tripChatEmbeddingDimensions int = 1536

@minValue(1)
param tripChatMaxMessageLength int = 2000

@minValue(0)
param tripChatMaxPriorUserTurns int = 6

@minValue(1)
param tripChatRetrievalTopK int = 12

@minValue(1)
param tripChatRateLimitPerMinute int = 10

@minValue(1)
param tripChatIndexBatchSize int = 100

@minValue(1)
param tripChatTargetP95Milliseconds int = 8000

@description('Set to "true" to start the relay polling. The workflow always deploys; it stays disabled until its Office 365 connection is consented and the EmailIngestion.Relay role is granted.')
param emailRelayEnabled string = 'false'

@description('Mail folder the relay polls for new messages.')
param emailRelayFolderPath string = 'Inbox'

@description('Mail folder the relay moves a message to once the API accepts it. Must already exist in the mailbox.')
param emailRelayProcessedFolderPath string = 'Processed'

var tags = {
  'azd-env-name': environmentName
  workload: 'trip-planner'
  environment: 'production'
  costCenter: 'trip-planner'
}

// The API authenticates to PostgreSQL with its managed identity, so the connection string
// carries no password: Npgsql supplies an Entra access token in its place. The username is
// the identity name, which must exist as a database role (see the runbook bootstrap step).
var postgresConnectionString = 'Host=${postgres.outputs.fqdn};Port=5432;Database=${postgres.outputs.databaseName};Username=${identity.outputs.api.name};SSL Mode=Require'

// azd substitutes an empty string for unset environment variables, which would otherwise
// win over the parameter default.
var effectiveApiScope = empty(entraApiScope) ? '${entraApiAudience}/access_as_user' : entraApiScope
var effectiveOpenAiDeployment = empty(azureOpenAiDeploymentName) ? 'gpt-4o' : azureOpenAiDeploymentName
var emailRelayOn = toLower(emailRelayEnabled) == 'true'
var emailRelayWorkflowName = 'logic-${environmentName}-email-relay'
var emailRelayConnectionName = 'con-${environmentName}-outlook'

module storage 'storage.bicep' = {
  name: 'storage'
  scope: resourceGroup(platformResourceGroup)
  params: {
    environmentName: environmentName
    location: location
    tags: tags
  }
}

module keyVault 'key-vault.bicep' = {
  name: 'key-vault'
  scope: resourceGroup(platformResourceGroup)
  params: {
    environmentName: environmentName
    location: location
    tags: tags
    deployerPrincipalId: deployerPrincipalId
    postgresPassword: postgresPassword
    postgresConnectionString: postgresConnectionString
  }
}

resource managedEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: containerAppsEnvironmentName
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: logAnalyticsWorkspaceName
}

module identity 'identity.bicep' = {
  name: 'identity'
  params: {
    environmentName: environmentName
    location: location
    tags: tags
  }
}

module acrPull 'acr-pull.bicep' = {
  name: 'acr-pull'
  scope: resourceGroup(registryResourceGroup)
  params: {
    identityName: acrPullIdentityName
    registryName: registryName
    location: location
  }
}

module rbac 'rbac.bicep' = {
  name: 'rbac'
  scope: resourceGroup(platformResourceGroup)
  params: {
    keyVaultName: keyVault.outputs.name
    storageAccountName: storage.outputs.name
    dataProtectionContainerName: storage.outputs.dataProtectionContainerName
    azureOpenAiResourceId: azureOpenAiResourceId
    tripChatResourceId: tripChatResourceId
    azureMapsResourceId: azureMapsResourceId
    webPrincipalId: identity.outputs.web.principalId
    apiPrincipalId: identity.outputs.api.principalId
  }
}

module postgres 'postgres-database.bicep' = {
  name: 'postgres'
  scope: resourceGroup(platformResourceGroup)
  params: {
    serverName: postgresServerName
  }
}

module api 'api.bicep' = {
  name: 'api'
  params: {
    appName: 'ca-api-${environmentName}'
    location: location
    tags: tags
    environmentId: managedEnvironment.id
    environmentDefaultDomain: managedEnvironment.properties.defaultDomain
    containerImage: apiImage
    registryLoginServer: acrPull.outputs.loginServer
    acrPullIdentityId: acrPull.outputs.id
    apiIdentityId: identity.outputs.api.id
    apiIdentityClientId: identity.outputs.api.clientId
    keyVaultUri: keyVault.outputs.uri
    postgresConnectionSecretName: keyVault.outputs.postgresConnectionSecretName
    azureMapsClientId: azureMapsClientId
    azureMapsEndpoint: azureMapsEndpoint
    entraInstance: entraInstance
    entraTenantId: entraTenantId
    entraApiClientId: entraApiClientId
    entraApiAudience: entraApiAudience
    azureOpenAiEndpoint: azureOpenAiEndpoint
    azureOpenAiDeploymentName: effectiveOpenAiDeployment
    tripChatEndpoint: tripChatEndpoint
    tripChatChatDeploymentName: tripChatChatDeploymentName
    tripChatEmbeddingDeploymentName: tripChatEmbeddingDeploymentName
    tripChatEmbeddingDimensions: tripChatEmbeddingDimensions
    tripChatMaxMessageLength: tripChatMaxMessageLength
    tripChatMaxPriorUserTurns: tripChatMaxPriorUserTurns
    tripChatRetrievalTopK: tripChatRetrievalTopK
    tripChatRateLimitPerMinute: tripChatRateLimitPerMinute
    tripChatIndexBatchSize: tripChatIndexBatchSize
    tripChatTargetP95Milliseconds: tripChatTargetP95Milliseconds
    releaseId: releaseId
  }
  dependsOn: [
    rbac
  ]
}

module web 'web.bicep' = {
  name: 'web'
  params: {
    appName: 'ca-web-${environmentName}'
    location: location
    tags: tags
    environmentId: managedEnvironment.id
    environmentDefaultDomain: managedEnvironment.properties.defaultDomain
    containerImage: webImage
    registryLoginServer: acrPull.outputs.loginServer
    acrPullIdentityId: acrPull.outputs.id
    webIdentityId: identity.outputs.web.id
    webIdentityClientId: identity.outputs.web.clientId
    dataProtectionBlobUri: '${storage.outputs.dataProtectionContainerUri}/keys.xml'
    dataProtectionKeyUri: keyVault.outputs.dataProtectionKeyUri
    apiFqdn: api.outputs.fqdn
    entraInstance: entraInstance
    entraTenantId: entraTenantId
    entraWebClientId: entraWebClientId
    entraDomain: entraDomain
    entraApiScope: effectiveApiScope
    releaseId: releaseId
  }
  dependsOn: [
    rbac
  ]
}

// Always deployed so the connection exists to be consented, but the trigger stays off until
// that consent and the app role are in place -- a relay that polls early caches a role-less
// token for ~24h and 403s the whole time.
module emailRelay 'email-relay.bicep' = {
  name: 'email-relay'
  params: {
    location: location
    tags: tags
    workflowName: emailRelayWorkflowName
    connectionName: emailRelayConnectionName
    relayIdentityId: identity.outputs.relay.id
    apiUri: 'https://${api.outputs.fqdn}'
    apiResourceUri: 'api://${entraApiClientId}'
    mailFolderPath: emailRelayFolderPath
    processedFolderPath: emailRelayProcessedFolderPath
    enabled: emailRelayOn
  }
}

// --- azd conventional outputs ------------------------------------------------

output AZURE_LOCATION string = location
output AZURE_PLATFORM_RESOURCE_GROUP string = platformResourceGroup
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = acrPull.outputs.loginServer
output AZURE_CONTAINER_REGISTRY_NAME string = registryName
output AZURE_CONTAINER_REGISTRY_RESOURCE_GROUP string = registryResourceGroup
output AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = managedEnvironment.id
output AZURE_CONTAINER_APPS_ENVIRONMENT_NAME string = managedEnvironment.name
output AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = managedEnvironment.properties.defaultDomain
output AZURE_KEY_VAULT_NAME string = keyVault.outputs.name
output AZURE_KEY_VAULT_ENDPOINT string = keyVault.outputs.uri
output AZURE_STORAGE_ACCOUNT_NAME string = storage.outputs.name
output AZURE_LOG_ANALYTICS_WORKSPACE_ID string = logAnalytics.id
output AZURE_OPENAI_ENDPOINT string = azureOpenAiEndpoint
output AZURE_OPENAI_DEPLOYMENT_NAME string = effectiveOpenAiDeployment
output SERVICE_WEB_NAME string = web.outputs.name
output SERVICE_WEB_URI string = web.outputs.url
output SERVICE_API_NAME string = api.outputs.name
output SERVICE_API_URI string = 'https://${api.outputs.fqdn}'
output SERVICE_POSTGRES_NAME string = postgres.outputs.name
output SERVICE_POSTGRES_FQDN string = postgres.outputs.fqdn
output SERVICE_POSTGRES_DATABASE string = postgres.outputs.databaseName
output WEB_IDENTITY_CLIENT_ID string = identity.outputs.web.clientId
output API_IDENTITY_CLIENT_ID string = identity.outputs.api.clientId
output API_IDENTITY_NAME string = identity.outputs.api.name
output EMAIL_RELAY_IDENTITY_NAME string = identity.outputs.relay.name
output EMAIL_RELAY_IDENTITY_PRINCIPAL_ID string = identity.outputs.relay.principalId
output EMAIL_RELAY_WORKFLOW_NAME string = emailRelay.outputs.name
output EMAIL_RELAY_CONNECTION_NAME string = emailRelay.outputs.connectionName
output EMAIL_RELAY_STATE string = emailRelay.outputs.state
