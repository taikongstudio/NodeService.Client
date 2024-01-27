{
  "machineDirectoryMappingConfigPath": "/ftptoolconfig/machineDirectoryMapping.json",
  "defaultTaskName": "kpr task",
  "ftpTasks": [
    {
      "taskName": "kpr task",
      "directoryConfigName": "kpr",
      "host": "172.27.242.222",
      "port": 21,
      "username": "xwdgmuser",
      "password": "xwdgm@2023",
      "localDirectory": "",
      "isLocalDirectoryUseMapping": true,
      "remoteDirectory": "/sc_yl/kpr/{0}",
      "searchPattern": "*.xlsx",
      "includeSubDirectories": true,
      "nextTaskName": "",
      "filters": [ "" ]
    }
  ]
}