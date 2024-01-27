{
  "machineDirectoryMappingConfigPath": "/ftptoolconfig/machineDirectoryMapping.json",
  "defaultTaskName": "xjc task",
  "ftpTasks": [
    {
      "taskName": "xjc task",
      "directoryConfigName": "xjc",
      "host": "172.27.241.153",
      "port": 21,
      "username": "ftpuser",
      "password": "Szch@2023",
      "localDirectory": "",
      "isLocalDirectoryUseMapping": true,
      "remoteDirectory": "/xjcbl/{0}",
      "searchPattern": "*.xlsx",
      "includeSubDirectories": true,
      "nextTaskName": "",
      "filters": [ "原始数据" ]
    }
  ]
}