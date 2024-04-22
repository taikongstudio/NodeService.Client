﻿using CommandLine.Text;
using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace NodeService.WindowsService.Models
{
    public class ServiceOptions
    {


        [Option("mode", HelpText = "mode")]
        public string mode { get; set; }

        [Option("env", Default = nameof(Environments.Production), HelpText = "env")]
        public string env { get; set; }

        [Option("doctor", Default =false, HelpText = "doctor")]
        public bool doctor { get; set; }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

    }
}