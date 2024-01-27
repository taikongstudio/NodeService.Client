    {
        "isEnabled": true,
		"server": "172.27.241.153",
		"username": "ftpuser",
		"password": "Szch@2023",
        "localLogDirectory": "./logs",
        "remoteLogDirectoryFormat": "JobsWorkerDaemonService/{0}/logs",
        "uploadLogJobCronExpressions": [
            "35 0/20 * * * ? *"
        ],
        "SleepSeconds":3600,
        "version": "0.0.0.0",
        "configfiles": [
            "config.bat"
        ],
        "plugins": [
            {
                "name": "ftptool",
                "version": "0.0.0.0",
                "filename": "ftptool_x64_20240119.zip",
                "exePath": "ftptool.exe",
                "hash": "dbb946f9ea6eb86ab0cbc9d858e1f9c670cf1db8d53354fde5b1a423f2a92e7d",
                "launch": false,
                "platform": "X64"
            },            
            {
                "name": "ProcessStatTool",
                "version": "default",
                "filename": "ProcessStatTool_X64_20240121.zip",
                "exePath": "ProcessStatTool.exe",
                "hash": "cc442e9ff62d79a9b9a7b016329fa15c8918c85abb587be13bd399541dfd00b8",
                "launch": false,
                "platform": "X64"
            },     
            {
                "name": "JobsWorkerWebService",
                "version": "default",
                "filename": "JobsWorkerWebService_X64_20240125.zip",
                "exePath": "JobsWorkerWebService.exe",
                "hash": "cc442e9ff62d79a9b9a7b016329fa15c8918c85abb587be13bd399541dfd00b8",
                "launch": false,
                "platform": "X64"
            }
        ]
    }