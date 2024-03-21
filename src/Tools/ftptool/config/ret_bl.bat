{
  "machineDirectoryMappingConfigPath": "/ftptoolconfig/machineDirectoryMapping.json",
  "defaultTaskName": "ret task",
  "ftpTasks": [
    {
      "taskName": "ret task",
      "directoryConfigName": "xjc",
      "host": "172.27.241.153",
      "port": 21,
      "username": "ftpuser",
      "password": "Szch@2023",
      "localDirectory": "",
      "isLocalDirectoryUseMapping": true,
      "remoteDirectory": "/retbl/{0}",
      "searchPattern": "*.xlsx",
      "includeSubDirectories": true,
      "nextTaskName": "",
      "filters": [ "原始数据" ]
    }
  ]
}