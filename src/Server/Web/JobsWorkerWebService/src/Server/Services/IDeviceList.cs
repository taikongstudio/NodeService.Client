using JobsWorkerWebService.Server.Models;

namespace JobsWorkerWebService.Server.Services
{
    public interface IDeviceList
    {
        public bool Contains(string deviceName);

        public MachineInfo[] GetMachines();
    }
}
