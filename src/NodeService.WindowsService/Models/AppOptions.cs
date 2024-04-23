using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.WindowsService.Models
{
    public class App
    {
        public string Name { get; set; }
    }

    public class AppOptions
    {
        public App[] Apps { get; set; } = [];
    }
}
