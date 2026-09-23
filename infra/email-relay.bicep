// Consumption Logic App that polls the trip mailbox and relays each message to the API.
// The API never touches a mailbox; polling, retry, and run history live here.
//
// This uses the Outlook.com connector, not Office 365 Outlook. The trip mailbox is a personal
// Microsoft account, and the two connectors are mutually exclusive on account type: office365
// requires an Exchange Online work mailbox and returns 401 on every call for a personal
// account, even though consent succeeds and the connection reports Connected.
//
// The connection is provisioned unauthorized -- OAuth consent cannot be scripted. An operator
// must sign in to it once before the trigger will fire. The workflow also needs the
// EmailIngestion.Relay app role, which is a directory grant and not expressible in Bicep.
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

@description('Mail folder a message is moved to once the API accepts it. Must already exist in the mailbox.')
param processedFolderPath string = 'Processed'

@description('Polling interval in minutes.')
@minValue(1)
@maxValue(60)
param pollingIntervalMinutes int = 3

@description('Whether the trigger polls. Leave false until the connection is authorized and the app role is granted.')
param enabled bool = false

var outlookApiId = subscriptionResourceId('Microsoft.Web/locations/managedApis', location, 'outlook')

resource outlook 'Microsoft.Web/connections@2016-06-01' = {
  name: connectionName
  location: location
  tags: tags
  properties: {
    displayName: 'Trip Planner mailbox'
    api: {
      id: outlookApiId
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
          outlook: {
            connectionId: outlook.id
            connectionName: outlook.name
            id: outlookApiId
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
                name: '@parameters(\'$connections\')[\'outlook\'][\'connectionId\']'
              }
            }
            method: 'get'
            path: '/v2/Mail/OnNewEmail'
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
            from: '@coalesce(triggerBody()?[\'Attachments\'], json(\'[]\'))'
            select: {
              fileName: '@item()?[\'Name\']'
              contentType: '@item()?[\'ContentType\']'
              contentBase64: '@item()?[\'ContentBytes\']'
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
              messageId: '@triggerBody()?[\'InternetMessageId\']'
              sender: '@triggerBody()?[\'From\']'
              recipient: '@triggerBody()?[\'To\']'
              subject: '@triggerBody()?[\'Subject\']'
              receivedAt: '@triggerBody()?[\'DateTimeReceived\']'
              bodyHtml: '@triggerBody()?[\'Body\']'
              attachments: '@body(\'Map_attachments\')'
            }
            authentication: {
              type: 'ManagedServiceIdentity'
              identity: relayIdentityId
              audience: apiResourceUri
            }
          }
        }
        // The API answers 2xx for every conclusive outcome, so gate the move on the outcome
        // itself. An unrecognized status stays in the polled folder rather than being filed
        // away as done; moving it out is what stops the next poll picking it up again.
        Move_if_processed: {
          type: 'If'
          runAfter: {
            Relay_to_api: [
              'Succeeded'
            ]
          }
          expression: {
            or: [
              {
                equals: [
                  '@body(\'Relay_to_api\')?[\'status\']'
                  'parsed'
                ]
              }
              {
                equals: [
                  '@body(\'Relay_to_api\')?[\'status\']'
                  'no_content'
                ]
              }
              {
                equals: [
                  '@body(\'Relay_to_api\')?[\'status\']'
                  'duplicate'
                ]
              }
            ]
          }
          actions: {
            Move_to_processed: {
              type: 'ApiConnection'
              runAfter: {}
              inputs: {
                host: {
                  connection: {
                    name: '@parameters(\'$connections\')[\'outlook\'][\'connectionId\']'
                  }
                }
                method: 'post'
                path: '/Mail/Move/@{encodeURIComponent(triggerBody()?[\'Id\'])}'
                queries: {
                  folderPath: processedFolderPath
                }
              }
            }
          }
        }
      }
    }
  }
}

output name string = relay.name
output connectionName string = outlook.name
output state string = relay.properties.state
