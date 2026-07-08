// PostgreSQL as a Container App (cheapest durable option) with an Azure Files volume.
// Internal TCP ingress only; kept at min 1 replica (no scale-to-zero for the database).
param location string = resourceGroup().location
param tags object = {}

@description('Container Apps managed environment resource id.')
param environmentId string

@description('Environment storage name to mount (Azure Files).')
param pgStorageName string

@description('Postgres image, pinned to a specific version for stability.')
param postgresImage string = 'postgres:16.4'

@description('Database name to create on first start.')
param databaseName string = 'tripplanner'

@secure()
param postgresPassword string

var appName = 'postgres'
var volumeName = 'pgdata-volume'

resource postgres 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  tags: tags
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        transport: 'tcp'
        targetPort: 5432
        exposedPort: 5432
      }
      secrets: [
        {
          name: 'postgres-password'
          value: postgresPassword
        }
      ]
    }
    template: {
      containers: [
        {
          name: appName
          image: postgresImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'POSTGRES_PASSWORD'
              secretRef: 'postgres-password'
            }
            {
              name: 'POSTGRES_DB'
              value: databaseName
            }
            {
              name: 'POSTGRES_USER'
              value: 'postgres'
            }
            {
              // Use a subdirectory of the Azure Files mount for the data dir to
              // avoid SMB permission/lost+found issues at the mount root.
              name: 'PGDATA'
              value: '/var/lib/postgresql/data/pgdata'
            }
          ]
          volumeMounts: [
            {
              volumeName: volumeName
              mountPath: '/var/lib/postgresql/data'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
      volumes: [
        {
          name: volumeName
          storageType: 'AzureFile'
          storageName: pgStorageName
        }
      ]
    }
  }
}

output name string = postgres.name
