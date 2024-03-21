	{
		"isEnabled": true,
		"server": "172.27.241.153",
		"username": "ftpuser",
		"password": "Szch@2023",
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
				"filename": "package_20240111_A1A7CB7B-2FEB-45FF-91FF-0B97FC3127E7.zip",
				"exePath": "JobsWorker.exe"
			}
		]
	}