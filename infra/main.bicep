// Root deployment for the Trip Planner production environment.
// Deployed at resource-group scope, so the azd environment must set AZURE_RESOURCE_GROUP.
// Incremental/idempotent: create-if-missing, preserves the PostgreSQL server and its data.
targetScope = 'resourceGroup'

@description('Naming seed for all resources (from AZURE_ENV_NAME).')
param environmentName string

param location string = resourceGroup().location

@description('Object id of the principal running the deployment. Used only to grant secret rotation rights.')
param deployerPrincipalId string = ''

@description('Display name of the deploying principal, recorded as the PostgreSQL Entra administrator.')
param deployerPrincipalName string = ''

@allowed([
  'User'
  'Group'
  'ServicePrincipal'
])
@description('Principal type of the deploying principal. CI deploys as a ServicePrincipal.')
param deployerPrincipalType string = 'User'

@description('Full image reference for the web app, e.g. <acr>.azurecr.io/web:<sha>.')
param webImage string = 'mcr.microsoft.com/k8se/quickstart:latest'

@description('Full image reference for the api app, e.g. <acr>.azurecr.io/api:<sha>.')
param apiImage string = 'mcr.microsoft.com/k8se/quickstart:latest'

@description('PostgreSQL administrator password. Used for schema bootstrap and break-glass; stored in Key Vault, never surfaced as an output.')
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

@description('Set to "true" to start the relay polling. The workflow always deploys; it stays disabled until its Office 365 connection is consented and the EmailIngestion.Relay role is granted.')
param emailRelayEnabled string = 'false'

@description('Mail folder the relay polls for new messages.')
param emailRelayFolderPath string = 'Inbox'

@description('Mail folder the relay moves a message to once the API accepts it. Must already exist in the mailbox.')
param emailRelayProcessedFolderPath string = 'Processed'

@description('Monthly cost threshold in subscription currency. Blank disables the budget alert.')
param budgetAmount string = ''

@description('Email address notified when the budget threshold is crossed.')
param budgetContact string = ''

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
var emailRelayConnectionName = 'con-${environmentName}-office365'

module storage 'storage.bicep' = {
  name: 'storage'
  params: {
    environmentName: environmentName
    location: location
    tags: tags
  }
}

module keyVault 'key-vault.bicep' = {
  name: 'key-vault'
  params: {
    environmentName: environmentName
    location: location
    tags: tags
    deployerPrincipalId: deployerPrincipalId
    postgresPassword: postgresPassword
    postgresConnectionString: postgresConnectionString
  }
}

module appEnvironment 'environment.bicep' = {
  name: 'environment'
  params: {
    environmentName: environmentName
    location: location
    tags: tags
  }
}

module identity 'identity.bicep' = {
  name: 'identity'
  params: {
    environmentName: environmentName
    location: location
    tags: tags
  }
}

module registry 'registry.bicep' = {
  name: 'registry'
  params: {
    environmentName: environmentName
    location: location
    tags: tags
  }
}

module rbac 'rbac.bicep' = {
  name: 'rbac'
  params: {
    registryName: registry.outputs.name
    keyVaultName: keyVault.outputs.name
    storageAccountName: storage.outputs.name
    dataProtectionContainerName: storage.outputs.dataProtectionContainerName
    azureOpenAiResourceId: azureOpenAiResourceId
    azureMapsResourceId: azureMapsResourceId
    acrPullPrincipalId: identity.outputs.acrPull.principalId
    webPrincipalId: identity.outputs.web.principalId
    apiPrincipalId: identity.outputs.api.principalId
  }
}

module postgres 'postgres.bicep' = {
  name: 'postgres'
  params: {
    environmentName: environmentName
    location: location
    tags: tags
    administratorPassword: postgresPassword
    entraTenantId: entraTenantId
    entraAdminObjectId: deployerPrincipalId
    entraAdminPrincipalName: deployerPrincipalName
    entraAdminPrincipalType: deployerPrincipalType
  }
}

module api 'api.bicep' = {
  name: 'api'
  params: {
    appName: 'ca-api-${environmentName}'
    location: location
    tags: tags
    environmentId: appEnvironment.outputs.environmentId
    environmentDefaultDomain: appEnvironment.outputs.defaultDomain
    containerImage: apiImage
    registryLoginServer: registry.outputs.loginServer
    acrPullIdentityId: identity.outputs.acrPull.id
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
    environmentId: appEnvironment.outputs.environmentId
    environmentDefaultDomain: appEnvironment.outputs.defaultDomain
    containerImage: webImage
    registryLoginServer: registry.outputs.loginServer
    acrPullIdentityId: identity.outputs.acrPull.id
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

// Skipped until the budget inputs are supplied.
module budget 'budget.bicep' = if (!empty(budgetAmount) && !empty(budgetContact)) {
  name: 'budget'
  params: {
    environmentName: environmentName
    amount: int(budgetAmount)
    contactEmail: budgetContact
  }
}

// --- azd conventional outputs ------------------------------------------------

output AZURE_LOCATION string = location
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = registry.outputs.loginServer
output AZURE_CONTAINER_REGISTRY_NAME string = registry.outputs.name
output AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = appEnvironment.outputs.environmentId
output AZURE_CONTAINER_APPS_ENVIRONMENT_NAME string = appEnvironment.outputs.environmentName
output AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = appEnvironment.outputs.defaultDomain
output AZURE_KEY_VAULT_NAME string = keyVault.outputs.name
output AZURE_KEY_VAULT_ENDPOINT string = keyVault.outputs.uri
output AZURE_STORAGE_ACCOUNT_NAME string = storage.outputs.name
output AZURE_LOG_ANALYTICS_WORKSPACE_ID string = appEnvironment.outputs.logAnalyticsWorkspaceId
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
