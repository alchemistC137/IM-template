using './main.bicep'

// Valores de ejemplo para validar CI; definir nombres reales al aprovisionar una instalación.
param location = 'spaincentral'
param appName = readEnvironmentVariable('AZURE_APP_NAME', 'intranet-example')
param planName = readEnvironmentVariable('AZURE_PLAN_NAME', 'plan-intranet-example')
param keyVaultName = readEnvironmentVariable('AZURE_KEY_VAULT_NAME', 'kv-intranet-example')
param registryName = readEnvironmentVariable('AZURE_REGISTRY_NAME', 'acr-intranet-example')
param connectionStringSecretName = readEnvironmentVariable('AZURE_CONNECTION_SECRET_NAME', 'ConnectionStrings--IntranetTemplate')
param registryPullTokenName = readEnvironmentVariable('AZURE_REGISTRY_PULL_TOKEN_NAME', 'template-intranet-pull')
param registryPullTokenPasswordSecretName = readEnvironmentVariable('AZURE_REGISTRY_PULL_SECRET_NAME', 'TemplateAcrPullPassword')
param applicationName = readEnvironmentVariable('INTRANET_APPLICATION_NAME', 'Intranet')
param companyName = readEnvironmentVariable('INTRANET_COMPANY_NAME', 'Mi empresa')
// Overridden by CD with the commit that passed CI; never deploy this placeholder.
param imageTag = 'bootstrap'
