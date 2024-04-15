using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANDCexSharp
{
    public class Cycle
    {
        public int Index;
        public float CapC;
        public float CapD;
        public float SpeCapC;
        public float SpeCapD;
        public float Effi;
        public float EnergyC;
        public float EnergyD;
        public float MedVoltD;
        public float CCCCap;
        public float CCCPer;
        public float PlatCapD;
        public float PlatSpeCapD;
        public float PlatPerD;
        public float PlatTimeD;
        public float CaptnC;
        public float CaptnD;
        public float Resistance;
        public float Resistance2;
        public float SpeEnergyC;
        public float SpeEnergyD;
        public float EndVoltD;
        public float RetentionD;
        public float AveDCIR_C;
        public float AveDCIR_D;
        public float MedVoltC;
        public string[] Procedures = [];

        public Cycle()
        {
        }
    }

    public struct Step
    {
        public int Index;
        public byte Mode;
        public float Period;
        public float Capacity;
        public float SpeCapacity;
        public float Energy;
        public float Capacitance;
        public float SpeEnergy;
        public float MedVolt;
        public float StartVolt;
        public float EndVolt;
    }

    public class Rec
    {
        public int Index;
        public double TestTime;
        public double StepTime;
        public float Current;
        public float Capacity;
        public float SpeCapacity;
        public float SocDod;
        public float Voltage;
        public float Energy;
        public float SpeEnergy;
        public string SysTime;
        public float AuxTemp;
        public float AuxVolt;
        public float AuxVolume;
        public DateTime SysTimeLong;
        public long SysTimeAsLong;
    }

    public struct PM
    {
        public int CONST_CURRENT_DISCHARGE;
        public int CONST_CURRENT_CHARGE;
        public int CONST_VOLT_DISCHARGE;
        public int CONST_VOLT_CHARGE;
        public int STOP;
        public int CONST_POWER_DISCHARGE;
        public int CONST_POWER_CHARGE;
        public int CONST_RESISTANCE_DISCHARGE;
        public int FLASH_DCIR;
        public int REST;
        public int SYN_SUSPEND;
        public int C_RATE_DISCHARGE;
        public int C_RATE_CHARGE;
    }


}
