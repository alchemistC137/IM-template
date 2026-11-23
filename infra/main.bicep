targetScope = 'resourceGroup'

param location string = resourceGroup().location
param appName string
param planName string

@minLength(3)
@maxLength(24)
param keyVaultName string

param registryName string
param imageRepository string = 'intranet'

@minLength(1)
@maxLength(100)
param applicationName string = 'Intranet'

@minLength(1)
@maxLength(100)
param companyName string = 'Mi empresa'

@description('Git commit SHA tag. The image must already exist in ACR.')
@minLength(1)
param imageTag string

@minLength(1)
@maxLength(127)
param connectionStringSecretName string = 'ConnectionStrings--IntranetTemplate'

@minLength(1)
@maxLength(127)
param registryPullTokenName string = 'template-intranet-pull'

@minLength(1)
@maxLength(127)
param registryPullTokenPasswordSecretName string = 'TemplateAcrPullPassword'

@description('Optional existing Key Vault secret holding the Azure Blob attachment connection string.')
param attachmentConnectionSecretName string = ''

resource pullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${appName}-runtime'
  location: location
}

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: registryName
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' existing = {
  name: planName
}

resource connectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' existing = {
  parent: vault
  name: connectionStringSecretName
}

resource registryPullTokenSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' existing = {
  parent: vault
  name: registryPullTokenPasswordSecretName
}

resource attachmentSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' existing = {
  parent: vault
  name: empty(attachmentConnectionSecretName) ? connectionStringSecretName : attachmentConnectionSecretName
}

resource app 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  kind: 'app,linux,container'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${pullIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: plan.id
    keyVaultReferenceIdentity: pullIdentity.id
    httpsOnly: true
    clientAffinityEnabled: true
    siteConfig: {
      linuxFxVersion: 'DOCKER|${registry.properties.loginServer}/${imageRepository}:${imageTag}'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      webSocketsEnabled: true
      healthCheckPath: '/health'
    }
  }
}

resource secretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(connectionSecret.id, pullIdentity.id, 'KeyVaultSecretsUser')
  scope: connectionSecret
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: pullIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource registryPullTokenSecretUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registryPullTokenSecret.id, pullIdentity.id, 'KeyVaultRegistryPullSecretUser')
  scope: registryPullTokenSecret
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: pullIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource attachmentSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(attachmentConnectionSecretName)) {
  name: guid(attachmentSecret.id, pullIdentity.id, 'KeyVaultAttachmentSecretUser')
  scope: attachmentSecret
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: pullIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource settings 'Microsoft.Web/sites/config@2023-12-01' = {
  parent: app
  name: 'appsettings'
  properties: union({
    ASPNETCORE_ENVIRONMENT: 'Production'
    Intranet__ApplicationName: applicationName
    Intranet__CompanyName: companyName
    DataProtection__ApplicationName: appName
    ASPNETCORE_HTTP_PORTS: '8080'
    // App Service terminates TLS and only exposes the container through its proxy.
    ASPNETCORE_FORWARDEDHEADERS_ENABLED: 'true'
    WEBSITES_PORT: '8080'
    WEBSITES_ENABLE_APP_SERVICE_STORAGE: 'true'
    DOCKER_REGISTRY_SERVER_URL: 'https://${registry.properties.loginServer}'
    DOCKER_REGISTRY_SERVER_USERNAME: registryPullTokenName
    DOCKER_REGISTRY_SERVER_PASSWORD: '@Microsoft.KeyVault(SecretUri=${vault.properties.vaultUri}secrets/${registryPullTokenPasswordSecretName}/)'
    ConnectionStrings__IMtemplate: '@Microsoft.KeyVault(SecretUri=${vault.properties.vaultUri}secrets/${connectionStringSecretName}/)'
    Messaging__AttachmentStorage__LocalRootPath: '/home/.messaging-attachments'
  }, empty(attachmentConnectionSecretName) ? {} : {
    Messaging__AttachmentStorage__ConnectionString: '@Microsoft.KeyVault(SecretUri=${vault.properties.vaultUri}secrets/${attachmentConnectionSecretName}/)'
  })
  dependsOn: [
    secretsUser
    registryPullTokenSecretUser
    attachmentSecretsUser
  ]
}

resource ftpPolicy 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2023-12-01' = {
  parent: app
  name: 'ftp'
  properties: {
    allow: false
  }
}

resource scmPolicy 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2023-12-01' = {
  parent: app
  name: 'scm'
  properties: {
    allow: false
  }
}

output appName string = app.name
output appUrl string = 'https://${app.properties.defaultHostName}'
output keyVaultName string = vault.name
output runtimePrincipalId string = pullIdentity.properties.principalId
output image string = '${registry.properties.loginServer}/${imageRepository}:${imageTag}'
