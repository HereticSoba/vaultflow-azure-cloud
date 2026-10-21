@description('Ubicación principal para recursos de Azure.')
param location string = resourceGroup().location

@description('Nombre de la aplicación')
param appName string = 'vaultflow'

@description('Entornos de despliegue')
param environment string = 'dev'

@description('Usuario administrador para Azure SQL Server')
param sqlAdminUsername string = 'vaultadmin'

@description('Contraseña para Azure SQL Server')
@secure()
param sqlAdminPassword string

@description('Tag de la imagen del backend a desplegar en Azure Container Apps.')
param imageTag string = 'latest'

@description('Clave de API de Groq para el agente de IA.')
@secure()
param groqApiKey string

/* Nombres */
var sqlServerName = 'sql-${appName}-${uniqueString(resourceGroup().id)}'
var sqlDbName = 'sqldb-${appName}'
var acrName = 'acr${appName}${environment}01'
var acaEnvName = 'cae-${appName}-${environment}'
var containerAppName = 'app-${appName}-api'
var keyVaultName = take('kv-${appName}-${uniqueString(resourceGroup().id)}', 24)
var appInsightsName = 'appins-${appName}-${environment}'
var dataFactoryName = 'adf-${appName}-${environment}'
var aiServiceName = 'ais-${appName}-${environment}'
var containerAppIdentityName = 'id-${appName}-${environment}'
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

/* ACR */
resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
  }
}

/* Azure SQL Server */
resource sqlServer 'Microsoft.Sql/servers@2022-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminUsername
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
  }
}

/* Azure SQL Database */
resource sqlDatabase 'Microsoft.Sql/servers/databases@2022-05-01-preview' = {
  parent: sqlServer
  name: sqlDbName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
}

/* Regla de firewall SQL */
resource allowAzureServicesFirewall 'Microsoft.Sql/servers/firewallRules@2022-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

/* Azure key vault */
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enabledForTemplateDeployment: true
  }
}

/* Cadena de conexión SQL (secreto) */
resource sqlConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'sql-connection-string'
  properties: {
    value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDbName};Persist Security Info=False;User ID=${sqlAdminUsername};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
  }
}

/* Clave de Groq (secreto) */
resource groqApiKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'groq-api-key'
  properties: {
    value: groqApiKey
  }
}

/* Identidad administrada de la Container App para leer Key Vault */
resource containerAppIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: containerAppIdentityName
  location: location
}

/* Permiso de la identidad sobre los secretos del Key Vault */
resource keyVaultSecretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, containerAppIdentity.id, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: containerAppIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

/* App insights */
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    RetentionInDays: 30
  }
}

/* Entorno azure container apps */
resource containerAppEnv 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: acaEnvName
  location: location
  properties: {
    zoneRedundant: false
  }
}

/* Azure Container App API backend*/
resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: containerAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${containerAppIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
      }
      registries: [
        {
          server: '${acrName}.azurecr.io'
          username: acrName
          passwordSecretRef: 'acr-password'
        }
      ]
      secrets: [
        {
          name: 'acr-password'
          value: containerRegistry.listCredentials().passwords[0].value
        }
      ]
    }
    template: {
      containers: [
        {
          name: containerAppName
          image: '${acrName}.azurecr.io/vaultflow-api:${imageTag}'
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'KeyVaultUri'
              value: keyVault.properties.vaultUri
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: containerAppIdentity.properties.clientId
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: appInsights.properties.ConnectionString
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 3
        rules: [
          {
            name: 'http-scaler'
            http: {
              metadata: {
                concurrentRequests: '10'
              }
            }
          }
        ]
      }
    }
  }
  dependsOn: [
    keyVaultSecretsUserRole
  ]
}

resource dataFactory 'Microsoft.DataFactory/factories@2018-06-01' = {
  name: dataFactoryName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    publicNetworkAccess: 'Enabled'
  }
}

/* Azure IA services */
resource aiServices 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: aiServiceName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  properties: {
    publicNetworkAccess: 'Enabled'
    customSubDomainName: aiServiceName
  }
}

/* Endpoint de servicio IA secreto en keyvault */
resource aiServicesEndpointSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'ai-services-endpoint'
  properties: {
    value: aiServices.properties.endpoint
  }
}

/* Outputs */
output acrLoginServer string = containerRegistry.properties.loginServer
output sqlServerFQDN string = sqlServer.properties.fullyQualifiedDomainName
output containerAppFQDN string = containerApp.properties.configuration.ingress.fqdn
output keyVaultName string = keyVault.name
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output dataFactoryName string = dataFactory.name
output aiServicesEndpoint string = aiServices.properties.endpoint
