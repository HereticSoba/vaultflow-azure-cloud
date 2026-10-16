param location string = 'eastus'
param environment string = 'dev'
param adminUsername string = 'vaultadmin'
@secure()
param adminPassword string

/* Red virtual */
resource vnet 'Microsoft.Network/virtualNetworks@2021-05-01' = {
  name: 'vnet-vaultflow-${environment}'
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: ['10.0.0.0/16']
    }
    subnets: [
      { name: 'snet-web', properties: { addressPrefix: '10.0.1.0/24' } }
      { name: 'snet-app', properties: { addressPrefix: '10.0.2.0/24' } }
      { name: 'snet-data', properties: { addressPrefix: '10.0.3.0/24' } }
    ]
  }
}

/* storage acc */
resource storage 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: 'stvf${environment}${take(uniqueString(resourceGroup().id),10)}'
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: { accessTier: 'Hot' }
}
resource blobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  name: '${storage.name}/default/statements'
}

/* azure keyvault */
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kvvf-${take(uniqueString(resourceGroup().id), 12)}'
  location: location
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    accessPolicies: []
    enableSoftDelete: true
  }
}

/* app insights */
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-vaultflow-${environment}-01'
  location: location
  kind: 'web'
  properties: { Application_Type: 'web' }
}

/* azure sql db */
resource sqlServer 'Microsoft.Sql/servers@2022-05-01-preview' = {
  name: 'sql-vaultflow-${uniqueString(resourceGroup().id)}'
  location: location
  properties: {
    administratorLogin: adminUsername
    administratorLoginPassword: adminPassword
  }
}

resource sqlDB 'Microsoft.Sql/servers/databases@2022-05-01-preview' = {
  parent: sqlServer
  name: 'sqldb-vaultflow'
  location: location
  sku: { name: 'Basic', tier: 'Basic' }
}

resource sqlFirewall 'Microsoft.Sql/servers/firewallRules@2022-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

/* app service plan and web app (front) */
resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: 'asp-vaultflow-${environment}'
  location: location
  sku: { name: 'F1', tier: 'Free' }
  properties: {}
}

resource webApp 'Microsoft.Web/sites@2022-09-01' = {
  name: 'app-vaultflow-web-${uniqueString(resourceGroup().id)}'
  location: location
  properties: { serverFarmId: appServicePlan.id }
}

/* virtual machine linux */
/* En comentario temporal por restricción en cuenta, para realizar pruebas correspondientes
resource publicIP 'Microsoft.Network/publicIPAddresses@2023-05-01' = {
  name: 'pip-vm-vaultflow'
  location: location
  sku: { name: 'Basic' }
  properties: { publicIPAllocationMethod: 'Dynamic' }
}

resource nic 'Microsoft.Network/networkInterfaces@2023-05-01' = {
  name: 'nic-vm-vaultflow'
  location: location
  properties: {
    ipConfigurations: [
      {
        name: 'ipconfig1'
        properties: {
          privateIPAllocationMethod: 'Dynamic'
          publicIPAddress: { id: publicIP.id }
          subnet: { id: '${vnet.id}/subnets/snet-app' }
        }
      }
    ]
  }
}

resource vm 'Microsoft.Compute/virtualMachines@2023-07-01' = {
  name: 'vm-vaultflow-tools'
  location: location
  properties: {
    hardwareProfile: { vmSize: 'Standard_B1s' }
    osProfile: {
      computerName: 'vaultvm'
      adminUsername: adminUsername
      adminPassword: adminPassword
    }
    storageProfile: {
      imageReference: {
        publisher: 'Canonical'
        offer: '0001-com-ubuntu-server-jammy'
        sku: '22_04-LTS'
        version: 'latest'
      }
      osDisk: {
        createOption: 'FromImage'
      }
    }
    networkProfile: { networkInterfaces: [{ id: nic.id }] }
  }
}
*/
