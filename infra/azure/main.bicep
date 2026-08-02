targetScope = 'resourceGroup'

@description('Short lowercase workload prefix, for example startpack.')
@minLength(3)
@maxLength(20)
param workloadName string = 'startpack'

@allowed([
  'staging'
  'production'
])
param environmentName string

param location string = resourceGroup().location

@description('New immutable image reference used by the one-shot migration job.')
param containerImage string

@description('Image kept on the API while the migration job runs. The deployment workflow promotes containerImage only after migration succeeds.')
param applicationImage string

@description('ACR name created by foundation.bicep.')
param registryName string

@secure()
@minLength(20)
param postgresAdministratorPassword string

@secure()
@minLength(20)
param postgresRuntimePassword string

@minLength(1)
param postgresAdministratorLogin string = 'startpackadmin'

@minLength(1)
param postgresRuntimeRole string = 'startpack_runtime'

param databaseName string = 'startpack'

@allowed([
  'Disabled'
  'ZoneRedundant'
])
param postgresHighAvailability string = 'Disabled'

param emailHost string
param emailPort int = 587
param emailFromAddress string
param emailUsername string

@secure()
param emailPassword string

@description('CPU cores allocated to each API replica.')
@allowed([
  '1.0'
  '2.0'
  '4.0'
])
param apiCpu string = '2.0'

@description('Memory allocated to each API replica.')
@allowed([
  '2Gi'
  '4Gi'
  '8Gi'
])
param apiMemory string = '4Gi'

@minValue(1)
@maxValue(20)
param apiMinReplicas int = environmentName == 'production' ? 5 : 1

@minValue(1)
@maxValue(20)
param apiMaxReplicas int = 10

var baseName = toLower('${workloadName}-${environmentName}')
var compactName = toLower(replace(baseName, '-', ''))
var uniqueSuffix = uniqueString(resourceGroup().id)
var environmentResourceName = take('${baseName}-env', 32)
var appName = take('${baseName}-api', 32)
var migrationJobName = take('${baseName}-migrate', 31)
var postgresName = take('${compactName}-${uniqueSuffix}', 63)
var vaultName = take('${compactName}${uniqueSuffix}', 24)
var redisName = take('${baseName}-${uniqueSuffix}', 60)
var vnetAddressPrefix = '10.42.0.0/16'
var acaSubnetPrefix = '10.42.0.0/23'
var databaseSubnetPrefix = '10.42.4.0/24'
var privateEndpointSubnetPrefix = '10.42.5.0/24'
var aspNetEnvironment = environmentName == 'staging' ? 'Staging' : 'Production'

resource registry 'Microsoft.ContainerRegistry/registries@2025-04-01' existing = {
  name: registryName
}

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${baseName}-identity'
  location: location
}

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${baseName}-logs'
  location: location
  properties: {
    retentionInDays: 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    sku: {
      name: 'PerGB2018'
    }
  }
}

resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${baseName}-insights'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logs.id
    // The exporter is configured with its connection string. App Insights Entra-only
    // ingestion requires a separate token-credential exporter configuration.
    DisableLocalAuth: false
    RetentionInDays: 90
  }
}

resource network 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: '${baseName}-vnet'
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [vnetAddressPrefix]
    }
  }
}

resource acaSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' = {
  parent: network
  name: 'container-apps'
  properties: {
    addressPrefix: acaSubnetPrefix
    delegations: [
      {
        name: 'container-apps-environment'
        properties: {
          serviceName: 'Microsoft.App/environments'
        }
      }
    ]
  }
}

resource databaseSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' = {
  parent: network
  name: 'postgres'
  properties: {
    addressPrefix: databaseSubnetPrefix
    delegations: [
      {
        name: 'postgres-flexible-server'
        properties: {
          serviceName: 'Microsoft.DBforPostgreSQL/flexibleServers'
        }
      }
    ]
  }
}

resource privateEndpointSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' = {
  parent: network
  name: 'private-endpoints'
  properties: {
    addressPrefix: privateEndpointSubnetPrefix
    privateEndpointNetworkPolicies: 'Disabled'
  }
}

resource privateDns 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: 'private.postgres.database.azure.com'
  location: 'global'
}

resource privateDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: privateDns
  name: '${baseName}-link'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: network.id
    }
  }
}

resource redisPrivateDns 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: 'privatelink.redis.azure.net'
  location: 'global'
}

resource redisPrivateDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: redisPrivateDns
  name: '${baseName}-link'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: network.id
    }
  }
}

resource keyVaultPrivateDns 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: 'privatelink.vaultcore.azure.net'
  location: 'global'
}

resource keyVaultPrivateDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: keyVaultPrivateDns
  name: '${baseName}-link'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: network.id
    }
  }
}

resource redis 'Microsoft.Cache/redisEnterprise@2025-07-01' = {
  name: redisName
  location: location
  sku: {
    name: 'Balanced_B0'
  }
  properties: {
    encryption: {}
    highAvailability: environmentName == 'production' ? 'Enabled' : 'Disabled'
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Disabled'
  }
}

resource redisDatabase 'Microsoft.Cache/redisEnterprise/databases@2025-07-01' = {
  parent: redis
  name: 'default'
  properties: {
    accessKeysAuthentication: 'Disabled'
    clientProtocol: 'Encrypted'
    clusteringPolicy: 'OSSCluster'
    evictionPolicy: 'VolatileLRU'
    modules: []
    port: 10000
  }
}

resource redisIdentityAccess 'Microsoft.Cache/redisEnterprise/databases/accessPolicyAssignments@2025-07-01' = {
  parent: redisDatabase
  name: 'appidentity'
  properties: {
    accessPolicyName: 'default'
    user: {
      objectId: identity.properties.principalId
    }
  }
}

resource redisPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: '${baseName}-redis-pe'
  location: location
  properties: {
    subnet: {
      id: privateEndpointSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: '${baseName}-redis-connection'
        properties: {
          privateLinkServiceId: redis.id
          groupIds: [
            'redisEnterprise'
          ]
        }
      }
    ]
  }
  dependsOn: [redisDatabase]
}

resource redisPrivateDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: redisPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'redis'
        properties: {
          privateDnsZoneId: redisPrivateDns.id
        }
      }
    ]
  }
  dependsOn: [redisPrivateDnsLink]
}

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: postgresName
  location: location
  sku: {
    name: postgresHighAvailability == 'ZoneRedundant' ? 'Standard_D2ds_v5' : 'Standard_B2s'
    tier: postgresHighAvailability == 'ZoneRedundant' ? 'GeneralPurpose' : 'Burstable'
  }
  properties: {
    administratorLogin: postgresAdministratorLogin
    administratorLoginPassword: postgresAdministratorPassword
    version: '16'
    network: {
      delegatedSubnetResourceId: databaseSubnet.id
      privateDnsZoneArmResourceId: privateDns.id
      publicNetworkAccess: 'Disabled'
    }
    highAvailability: {
      mode: postgresHighAvailability
    }
    storage: {
      autoGrow: 'Enabled'
      storageSizeGB: 64
    }
    backup: {
      backupRetentionDays: 14
      geoRedundantBackup: 'Disabled'
    }
    maintenanceWindow: {
      customWindow: 'Enabled'
      dayOfWeek: 0
      startHour: 2
      startMinute: 0
    }
  }
  dependsOn: [privateDnsLink]
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgres
  name: databaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: vaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enablePurgeProtection: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    publicNetworkAccess: 'Disabled'
    sku: {
      family: 'A'
      name: 'standard'
    }
  }
}

resource dataProtectionKey 'Microsoft.KeyVault/vaults/keys@2023-07-01' = {
  parent: vault
  name: 'data-protection'
  properties: {
    kty: 'RSA'
    keySize: 3072
    keyOps: [
      'wrapKey'
      'unwrapKey'
    ]
    attributes: {
      enabled: true
    }
  }
}

resource keyVaultPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: '${baseName}-vault-pe'
  location: location
  properties: {
    subnet: {
      id: privateEndpointSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: '${baseName}-vault-connection'
        properties: {
          privateLinkServiceId: vault.id
          groupIds: [
            'vault'
          ]
        }
      }
    ]
  }
}

resource keyVaultPrivateDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: keyVaultPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'vault'
        properties: {
          privateDnsZoneId: keyVaultPrivateDns.id
        }
      }
    ]
  }
  dependsOn: [keyVaultPrivateDnsLink]
}

var adminConnectionString = 'Host=${postgres.properties.fullyQualifiedDomainName};Port=5432;Database=${databaseName};Username=${postgresAdministratorLogin};Password=${postgresAdministratorPassword};SSL Mode=Require;Trust Server Certificate=false'
var runtimeConnectionString = 'Host=${postgres.properties.fullyQualifiedDomainName};Port=5432;Database=${databaseName};Username=${postgresRuntimeRole};Password=${postgresRuntimePassword};SSL Mode=Require;Trust Server Certificate=false'

resource adminConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'postgres-admin-connection'
  properties: {
    value: adminConnectionString
  }
}

resource runtimeConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'postgres-runtime-connection'
  properties: {
    value: runtimeConnectionString
  }
}

resource runtimePasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'postgres-runtime-password'
  properties: {
    value: postgresRuntimePassword
  }
}

resource insightsConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'application-insights-connection'
  properties: {
    value: insights.properties.ConnectionString
  }
}

resource emailPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'smtp-password'
  properties: {
    value: emailPassword
  }
}

var keyVaultCryptoUserRole = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '12338af0-0e69-4776-bea7-57ae8d297424')
var keyVaultSecretsUserRole = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '4633458b-17de-408a-b874-0445c86b69e6')
var acrPullRole = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '7f951dda-4ed3-4680-a7ca-43fe172d538d')

resource keyVaultCryptoGrant 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, identity.id, keyVaultCryptoUserRole)
  scope: vault
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultCryptoUserRole
  }
}

resource keyVaultSecretGrant 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, identity.id, keyVaultSecretsUserRole)
  scope: vault
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultSecretsUserRole
  }
}

resource registryPullGrant 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, identity.id, acrPullRole)
  scope: registry
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: acrPullRole
  }
}

resource containerEnvironment 'Microsoft.App/managedEnvironments@2024-10-02-preview' = {
  name: environmentResourceName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
    vnetConfiguration: {
      infrastructureSubnetId: acaSubnet.id
      internal: false
    }
    zoneRedundant: false
  }
}

var publicHostName = '${appName}.${containerEnvironment.properties.defaultDomain}'
// Construct this explicitly rather than using a deployment-returned key-version URI.
// Data Protection must resolve the newest wrapping-key version after rotation.
var keyIdentifier = '${vault.properties.vaultUri}keys/${dataProtectionKey.name}'

resource app 'Microsoft.App/containerApps@2025-01-01' = {
  name: appName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        allowInsecure: false
        targetPort: 8080
        transport: 'auto'
      }
      registries: [
        {
          server: registry.properties.loginServer
          identity: identity.id
        }
      ]
      secrets: [
        {
          name: 'postgres-runtime'
          keyVaultUrl: runtimeConnectionSecret.properties.secretUriWithVersion
          identity: identity.id
        }
        {
          name: 'application-insights'
          keyVaultUrl: insightsConnectionSecret.properties.secretUriWithVersion
          identity: identity.id
        }
        {
          name: 'smtp-password'
          keyVaultUrl: emailPasswordSecret.properties.secretUriWithVersion
          identity: identity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: applicationImage
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: aspNetEnvironment }
            { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
            { name: 'AllowedHosts', value: publicHostName }
            { name: 'ConnectionStrings__Postgres', secretRef: 'postgres-runtime' }
            { name: 'Jwt__Issuer', value: 'https://${publicHostName}' }
            { name: 'ReverseProxy__Enabled', value: 'true' }
            { name: 'ReverseProxy__KnownNetworks__0', value: acaSubnetPrefix }
            { name: 'AuthCookies__RequireSecure', value: 'true' }
            { name: 'Cors__AllowedOrigins__0', value: 'https://${publicHostName}' }
            { name: 'Cors__CookieModeOrigins__0', value: 'https://${publicHostName}' }
            { name: 'WebAuthn__ServerDomain', value: publicHostName }
            { name: 'WebAuthn__Origins__0', value: 'https://${publicHostName}' }
            { name: 'Azure__DataProtectionKeyIdentifier', value: keyIdentifier }
            { name: 'Azure__ManagedIdentityClientId', value: identity.properties.clientId }
            { name: 'Telemetry__AzureMonitorExporterEnabled', value: 'true' }
            { name: 'Telemetry__AzureMonitorConnectionString', secretRef: 'application-insights' }
            { name: 'Redis__Enabled', value: 'true' }
            { name: 'Redis__Endpoint', value: '${redis.name}.${location}.redis.azure.net:10000' }
            { name: 'Redis__UseAzureIdentity', value: 'true' }
            { name: 'Redis__InstanceName', value: '${baseName}:' }
            { name: 'Email__Host', value: emailHost }
            { name: 'Email__Port', value: string(emailPort) }
            { name: 'Email__FromAddress', value: emailFromAddress }
            { name: 'Email__UseTls', value: 'true' }
            { name: 'Email__Username', value: emailUsername }
            { name: 'Email__Password', secretRef: 'smtp-password' }
          ]
          resources: {
            cpu: json(apiCpu)
            memory: apiMemory
          }
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 20
              periodSeconds: 10
              timeoutSeconds: 3
              failureThreshold: 5
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 5
              periodSeconds: 5
              timeoutSeconds: 3
              failureThreshold: 6
            }
          ]
        }
      ]
      scale: {
        minReplicas: apiMinReplicas
        maxReplicas: apiMaxReplicas
        rules: [
          {
            name: 'http-concurrency'
            http: {
              metadata: {
                concurrentRequests: '2'
              }
            }
          }
        ]
      }
    }
  }
  dependsOn: [
    database
    keyVaultCryptoGrant
    keyVaultSecretGrant
    keyVaultPrivateDnsGroup
    registryPullGrant
    redisIdentityAccess
    redisPrivateDnsGroup
  ]
}

resource migrationJob 'Microsoft.App/jobs@2024-03-01' = {
  name: migrationJobName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  properties: {
    environmentId: containerEnvironment.id
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 1800
      replicaRetryLimit: 0
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
      registries: [
        {
          server: registry.properties.loginServer
          identity: identity.id
        }
      ]
      secrets: [
        {
          name: 'postgres-admin'
          keyVaultUrl: adminConnectionSecret.properties.secretUriWithVersion
          identity: identity.id
        }
        {
          name: 'postgres-runtime-password'
          keyVaultUrl: runtimePasswordSecret.properties.secretUriWithVersion
          identity: identity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'migrator'
          image: containerImage
          args: [
            'operations'
            'migrate-database'
          ]
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: aspNetEnvironment }
            { name: 'ConnectionStrings__Postgres', secretRef: 'postgres-admin' }
            { name: 'DatabaseDeployment__RuntimeRole', value: postgresRuntimeRole }
            { name: 'DatabaseDeployment__RuntimePassword', secretRef: 'postgres-runtime-password' }
            { name: 'ReverseProxy__Enabled', value: 'true' }
            { name: 'ReverseProxy__KnownNetworks__0', value: acaSubnetPrefix }
            { name: 'Azure__DataProtectionKeyIdentifier', value: keyIdentifier }
            { name: 'Azure__ManagedIdentityClientId', value: identity.properties.clientId }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
    }
  }
  dependsOn: [
    database
    keyVaultCryptoGrant
    keyVaultSecretGrant
    keyVaultPrivateDnsGroup
    registryPullGrant
  ]
}

output apiName string = app.name
output apiUrl string = 'https://${publicHostName}'
output migrationJobName string = migrationJob.name
output registryLoginServer string = registry.properties.loginServer
output keyVaultName string = vault.name
output postgresServerName string = postgres.name
output redisName string = redis.name
