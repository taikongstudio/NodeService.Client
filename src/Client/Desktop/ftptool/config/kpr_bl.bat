{
  "machineDirectoryMappingConfigPath": "/ftptoolconfig/machineDirectoryMapping.json",
  "defaultTaskName": "kpr task",
  "ftpTasks": [
    {
      "taskName": "kpr task",
      "directoryConfigName": "kpr",
      "host": "172.27.241.153",
      "port": 21,
      "username": "ftpuser",
      "password": "Szch@2023",
      "localDirectory": "",
      "isLocalDirectoryUseMapping": true,
      "remoteDirectory": "/kprbl/{0}",
      "searchPattern": "*.xlsx",
      "includeSubDirectories": true,
      "nextTaskName": "",
      "filters": []
    }
  ]
}