targetScope = 'resourceGroup'

param location string = resourceGroup().location
param planName string
param keyVaultName string
param registryName string

@allowed([
  'B1'
  'B2'
  'B3'
  'S1'
  'S2'
  'S3'
  'P1v3'
])
param planSku string = 'B1'

module containerRegistry './registry.bicep' = {
  name: 'container-registry'
  params: {
	location: location
	registryName: registryName
  }
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
	tenantId: tenant().tenantId
	sku: {
	  family: 'A'
	  name: 'standard'
	}
	enableRbacAuthorization: true
	enableSoftDelete: true
	softDeleteRetentionInDays: 90
	accessPolicies: []
	publicNetworkAccess: 'Enabled'
  }
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  kind: 'linux'
  sku: {
	name: planSku
	capacity: 1
  }
  properties: {
	reserved: true
  }
}

output registryName string = containerRegistry.outputs.registryName
output keyVaultName string = vault.name
output planName string = plan.name
