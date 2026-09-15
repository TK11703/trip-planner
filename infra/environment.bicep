// Log Analytics + Container Apps managed environment + the managed Aspire dashboard.
// No environment storage: the database is a managed service, so nothing mounts a share.
param environmentName string
param location string = resourceGroup().location
param tags object = {}

@description('Daily Log Analytics ingestion cap in GB. Caps runaway logging cost; -1 disables the cap.')
param logAnalyticsDailyQuotaGb int = 1

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'law-${environmentName}'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    // A log storm should cost a capped amount, not an unbounded one. Ingestion stops for
    // the rest of the UTC day once the cap is hit; the Aspire dashboard is unaffected
    // because it reads live OTLP rather than the workspace.
    workspaceCapping: {
      dailyQuotaGb: logAnalyticsDailyQuotaGb
    }
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

// Managed Aspire dashboard. Hosted by the platform at no extra compute cost, it gives
// live traces, metrics, and structured logs without standing up a separate app. Apps
// reach it through the OTLP endpoint the environment injects into every container.
resource aspireDashboard 'Microsoft.App/managedEnvironments/dotNetComponents@2024-10-02-preview' = {
  parent: managedEnvironment
  name: 'aspire-dashboard'
  properties: {
    componentType: 'AspireDashboard'
  }
}

output environmentId string = managedEnvironment.id
output environmentName string = managedEnvironment.name
output defaultDomain string = managedEnvironment.properties.defaultDomain
output logAnalyticsWorkspaceId string = logAnalytics.id
output logAnalyticsCustomerId string = logAnalytics.properties.customerId
output aspireDashboardName string = aspireDashboard.name
