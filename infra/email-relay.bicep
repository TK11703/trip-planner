// Consumption Logic App that polls the trip mailbox and relays each message to the API.
// The API never touches a mailbox; polling, retry, and run history live here.
//
// The Office 365 connection is provisioned unauthorized -- OAuth consent cannot be scripted.
// An operator must sign in to it once before the trigger will fire. The workflow also needs
// the EmailIngestion.Relay app role, which is a directory grant and not expressible in Bicep.
// Both steps are in docs/operations/production-runbook.md section 1.6, and until they are
// done the workflow deploys disabled so the trigger cannot fire against them.
param location string = resourceGroup().location
param tags object = {}

param workflowName string
param connectionName string

@description('Resource id of the user-assigned identity the workflow presents to the API.')
param relayIdentityId string

@description('Base URI of the API container app, without a trailing slash.')
param apiUri string

@description('Application ID URI the relay requests an access token for, e.g. api://<api-client-id>.')
param apiResourceUri string

@description('Mail folder polled for new messages.')
param mailFolderPath string = 'Inbox'

@description('Polling interval in minutes.')
@minValue(1)
@maxValue(60)
param pollingIntervalMinutes int = 3

@description('Whether the trigger polls. Leave false until the connection is authorized and the app role is granted.')
param enabled bool = false

var office365ApiId = subscriptionResourceId('Microsoft.Web/locations/managedApis', location, 'office365')

resource office365 'Microsoft.Web/connections@2016-06-01' = {
  name: connectionName
  location: location
  tags: tags
  properties: {
    displayName: 'Trip Planner mailbox'
    api: {
      id: office365ApiId
    }
  }
}

resource relay 'Microsoft.Logic/workflows@2019-05-01' = {
  name: workflowName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${relayIdentityId}': {}
    }
  }
  properties: {
    state: enabled ? 'Enabled' : 'Disabled'
    parameters: {
      '$connections': {
        value: {
          office365: {
            connectionId: office365.id
            connectionName: office365.name
            id: office365ApiId
          }
        }
      }
    }
    definition: {
      '$schema': 'https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#'
      contentVersion: '1.0.0.0'
      parameters: {
        '$connections': {
          type: 'Object'
          defaultValue: {}
        }
      }
      triggers: {
        // splitOn fans the polled batch out into one run per message, so a single poison
        // message cannot block the rest.
        When_a_new_email_arrives: {
          type: 'ApiConnection'
          recurrence: {
            frequency: 'Minute'
            interval: pollingIntervalMinutes
          }
          splitOn: '@triggerBody()?[\'value\']'
          inputs: {
            host: {
              connection: {
                name: '@parameters(\'$connections\')[\'office365\'][\'connectionId\']'
              }
            }
            method: 'get'
            path: '/v3/Mail/OnNewEmail'
            queries: {
              folderPath: mailFolderPath
              importance: 'Any'
              fetchOnlyWithAttachment: false
              includeAttachments: true
            }
          }
        }
      }
      actions: {
        Map_attachments: {
          type: 'Select'
          runAfter: {}
          inputs: {
            from: '@coalesce(triggerBody()?[\'attachments\'], json(\'[]\'))'
            select: {
              fileName: '@item()?[\'name\']'
              contentType: '@item()?[\'contentType\']'
              contentBase64: '@item()?[\'contentBytes\']'
            }
          }
        }
        Relay_to_api: {
          type: 'Http'
          runAfter: {
            Map_attachments: [
              'Succeeded'
            ]
          }
          inputs: {
            method: 'POST'
            uri: '${apiUri}/api/email-ingestion/messages'
            headers: {
              'Content-Type': 'application/json'
            }
            // bodyText is deliberately omitted: the connector only offers a truncated preview,
            // and a non-blank bodyText would win over the full HTML on the API side.
            body: {
              messageId: '@triggerBody()?[\'internetMessageId\']'
              sender: '@triggerBody()?[\'from\']'
              recipient: '@triggerBody()?[\'toRecipients\']'
              subject: '@triggerBody()?[\'subject\']'
              receivedAt: '@triggerBody()?[\'receivedDateTime\']'
              bodyHtml: '@triggerBody()?[\'body\']'
              attachments: '@body(\'Map_attachments\')'
            }
            authentication: {
              type: 'ManagedServiceIdentity'
              identity: relayIdentityId
              audience: apiResourceUri
            }
          }
        }
      }
    }
  }
}

output name string = relay.name
output connectionName string = office365.name
output state string = relay.properties.state
