// Root deployment for the Trip Planner production environment.
// Deployed at resource-group scope (the workflow creates the RG first).
// Incremental/idempotent: create-if-missing, preserves the postgres Azure Files data.
targetScope = 'resourceGroup'

@description('Naming seed for all resources (from AZURE_ENV_NAME).')
param environmentName string

param location string = resourceGroup().location

@description('Full image reference for the web app, e.g. <acr>.azurecr.io/web:<sha>.')
param webImage string = 'mcr.microsoft.com/k8se/quickstart:latest'

@description('Full image reference for the api app, e.g. <acr>.azurecr.io/api:<sha>.')
param apiImage string = 'mcr.microsoft.com/k8se/quickstart:latest'

@secure()
param postgresPassword string

param entraInstance string = environment().authentication.loginEndpoint
param entraTenantId string
param entraWebClientId string
param entraApiClientId string

@description('API token audience. Defaults to the API client id.')
param entraApiAudience string = entraApiClientId

param entraDomain string = ''

var tags = {
  'azd-env-name': environmentName
  workload: 'trip-planner'
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
    principalId: identity.outputs.principalId
  }
}

module postgres 'postgres.bicep' = {
  name: 'postgres'
  params: {
    location: location
    tags: tags
    environmentId: appEnvironment.outputs.environmentId
    pgStorageName: appEnvironment.outputs.pgStorageName
    postgresPassword: postgresPassword
  }
}

module api 'api.bicep' = {
  name: 'api'
  params: {
    location: location
    tags: tags
    environmentId: appEnvironment.outputs.environmentId
    containerImage: apiImage
    identityId: identity.outputs.id
    registryLoginServer: registry.outputs.loginServer
    connectionString: 'Host=${postgres.outputs.name};Port=5432;Database=tripplanner;Username=postgres;Password=${postgresPassword};SSL Mode=Disable'
    entraInstance: entraInstance
    entraTenantId: entraTenantId
    entraApiClientId: entraApiClientId
    entraApiAudience: entraApiAudience
  }
}

module web 'web.bicep' = {
  name: 'web'
  params: {
    location: location
    tags: tags
    environmentId: appEnvironment.outputs.environmentId
    containerImage: webImage
    identityId: identity.outputs.id
    registryLoginServer: registry.outputs.loginServer
    apiAppName: api.outputs.name
    entraInstance: entraInstance
    entraTenantId: entraTenantId
    entraWebClientId: entraWebClientId
    entraDomain: entraDomain
  }
}

output acrLoginServer string = registry.outputs.loginServer
output acrName string = registry.outputs.name
output webUrl string = web.outputs.url
output apiName string = api.outputs.name
output webName string = web.outputs.name
