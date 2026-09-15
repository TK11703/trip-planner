// Monthly cost guardrail for the resource group. Alert-only: it notifies, it never
// blocks or deletes resources.
targetScope = 'resourceGroup'

param environmentName string

@description('Monthly threshold in the subscription billing currency.')
param amount int

@description('Email address notified at the actual and forecast thresholds.')
param contactEmail string

@description('Budget start date. Must be the first of a month.')
param startDate string = '${utcNow('yyyy-MM')}-01'

resource budget 'Microsoft.Consumption/budgets@2023-05-01' = {
  name: 'budget-${environmentName}'
  properties: {
    category: 'Cost'
    amount: amount
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: startDate
    }
    notifications: {
      actual80: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 80
        thresholdType: 'Actual'
        contactEmails: [
          contactEmail
        ]
      }
      forecast100: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 100
        thresholdType: 'Forecasted'
        contactEmails: [
          contactEmail
        ]
      }
    }
  }
}

output id string = budget.id
