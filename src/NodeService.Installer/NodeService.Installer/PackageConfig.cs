﻿namespace NodeService.Installer
{
    public class PackageConfig
    {
        public string ConfigName { get; set; }
        public string Host { get; set; }

        public int Port { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }

        public string PackagePath { get; set; }

        public string InstallPath { get; set; }

        public string ServiceName { get; set; }

        public string EntryPoint { get; set; }
    }
}
