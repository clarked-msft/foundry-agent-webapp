param name string
param location string
param tags object
param logAnalyticsWorkspaceId string

@description('Resource ID of an existing subnet to inject the environment into (must be delegated to Microsoft.App/environments, /27 or larger). Leave empty for a non-VNet environment.')
param infrastructureSubnetId string = ''

// VNet injection requires the workload-profile environment model (subnet delegated to
// Microsoft.App/environments, /27+). Only added when a subnet is supplied so non-VNet
// deployments keep the classic Consumption-only environment.
var vnetProps = !empty(infrastructureSubnetId) ? {
  vnetConfiguration: {
    infrastructureSubnetId: infrastructureSubnetId
    internal: false
  }
  workloadProfiles: [
    {
      name: 'Consumption'
      workloadProfileType: 'Consumption'
    }
  ]
} : {}

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: name
  location: location
  tags: tags
  properties: union({
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: reference(logAnalyticsWorkspaceId, '2023-09-01').customerId
        sharedKey: listKeys(logAnalyticsWorkspaceId, '2023-09-01').primarySharedKey
      }
    }
  }, vnetProps)
}

output id string = containerAppsEnvironment.id
output name string = containerAppsEnvironment.name
