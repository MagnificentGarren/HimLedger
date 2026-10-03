@description('Short, lowercase project prefix used in Azure resource names.')
@minLength(3)
@maxLength(12)
param namePrefix string = 'himledger'

@description('Deploy each environment into a separate resource group.')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environmentName string

@description('Azure region for this environment.')
param location string = resourceGroup().location

@description('SQL Server administrator login. Prefer a dedicated, non-human account.')
param sqlAdminLogin string

@description('SQL Server administrator password; supply securely as a deployment parameter.')
@secure()
@minLength(16)
param sqlAdminPassword string

@description('The browser application origin allowed to call this API, including scheme.')
param frontendOrigin string

@description('Entra ID tenant authority, for example https://login.microsoftonline.com/<tenant-id>/v2.0.')
param entraAuthority string

@description('Audience configured on the Entra ID API registration.')
param entraAudience string

@description('App Service outbound IPv4 addresses. Deploy once with an empty array, then set this from the template output and deploy again.')
param appOutboundIps array = []

var suffix = uniqueString(resourceGroup().id)
var compactPrefix = toLower(replace(namePrefix, '-', ''))
var appServiceName = '${namePrefix}-${environmentName}-${suffix}'
var sqlServerName = '${namePrefix}-${environmentName}-${suffix}'
var storageAccountName = take('${suffix}${compactPrefix}${environmentName}', 24)
var keyVaultName = take('${suffix}${compactPrefix}${environmentName}', 24)
var storageContainerName = 'receipts'
var connectionStringSecretUri = '${keyVault.properties.vaultUri}secrets/DefaultConnection'
var blobConnectionStringSecretUri = '${keyVault.properties.vaultUri}secrets/AzureBlobStorage'
var sqlConnectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabase.name};Persist Security Info=False;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
var blobConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageKeys.keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
var storageKeys = storageAccount.listKeys()
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6' // Public Azure built-in role ID, not a credential. gitleaks:allow

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    networkAcls: {
      bypass: 'None'
      defaultAction: 'Deny'
      ipRules: [
        for ip in appOutboundIps: {
          action: 'Allow'
          value: ip
        }
      ]
    }
  }
}

resource storageContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: '${storageAccount.name}/default/${storageContainerName}'
  properties: {
    publicAccess: 'None'
  }
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: 'HimLedger'
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648
    readScale: 'Disabled'
    zoneRedundant: false
  }
}

resource sqlBackupRetention 'Microsoft.Sql/servers/databases/backupShortTermRetentionPolicies@2021-11-01' = {
  parent: sqlDatabase
  name: 'default'
  properties: {
    retentionDays: 7
    diffBackupIntervalInHours: 24
  }
}

resource sqlFirewallRules 'Microsoft.Sql/servers/firewallRules@2021-11-01' = [
  for (ip, index) in appOutboundIps: {
    parent: sqlServer
    name: 'app-${environmentName}-${index}'
    properties: {
      startIpAddress: ip
      endIpAddress: ip
    }
  }
]

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 30
    publicNetworkAccess: 'Enabled'
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${namePrefix}-${environmentName}-insights-${suffix}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    RetentionInDays: 30
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${namePrefix}-${environmentName}-plan-${suffix}'
  location: location
  kind: 'linux'
  sku: {
    name: 'B1'
    tier: 'Basic'
    capacity: 1
  }
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: appServiceName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      healthCheckPath: '/healthz'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environmentName == 'prod' ? 'Production' : (environmentName == 'staging' ? 'Staging' : 'Development')
        }
        {
          name: 'ConnectionStrings__DefaultConnection'
          value: '@Microsoft.KeyVault(SecretUri=${connectionStringSecretUri})'
        }
        {
          name: 'ConnectionStrings__AzureBlobStorage'
          value: '@Microsoft.KeyVault(SecretUri=${blobConnectionStringSecretUri})'
        }
        {
          name: 'AzureBlobStorage__ContainerName'
          value: storageContainerName
        }
        {
          name: 'EntraId__Authority'
          value: entraAuthority
        }
        {
          name: 'EntraId__Audience'
          value: entraAudience
        }
        {
          name: 'Cors__AllowedOrigins__0'
          value: frontendOrigin
        }
        {
          name: 'ApplicationInsights__ConnectionString'
          value: appInsights.properties.ConnectionString
        }
      ]
    }
  }
}

resource keyVaultSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, webApp.id, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource sqlConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'DefaultConnection'
  properties: {
    value: sqlConnectionString
  }
}

resource blobConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AzureBlobStorage'
  properties: {
    value: blobConnectionString
  }
}

output apiName string = webApp.name
output apiUrl string = 'https://${webApp.properties.defaultHostName}'
output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDatabase.name
output storageAccountName string = storageAccount.name
output keyVaultName string = keyVault.name
output appOutboundIpAddresses string = webApp.properties.possibleOutboundIpAddresses
