	{
		"isEnabled": true,
		"server": "172.27.242.224",
		"username": "xwdgmuser",
		"password": "xwdgm@2023",
		"localLogDirectory": "./logs",
		"remoteLogDirectoryFormat": "JobsWorkerDaemonService/{0}/logs",
		"uploadLogJobCronExpressions": [
			"47 0/10 * * * ? *"
		],
		"version": "0.0.0.10",
		"SleepSeconds":150,
		"configfiles": [
			"config.bat",
			"create_service.bat",
			"delete_service.bat",
			"start_service.bat",
			"stop_service.bat"
		],
		"plugins": [
			{
				"name": "JobsWorker",
				"version": "0.0.0.10",
				"filename": "test_gm_20240124_669B72DA-C17A-44BD-8326-C515714FCAC4.zip",
				"exePath": "JobsWorker.exe"
			}
		]
	}