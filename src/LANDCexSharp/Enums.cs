using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANDCexSharp
{
    public enum USCH
    {
        BaseOn_Auto = -1,
        BaseOn_mA = 0,
        BaseOn_Ampere = 1,
        BaseOn_uA = 2,
    }

    public enum CYCLEColumns : uint
    {
        Index = 0x1000,
        CapC = 0x1001,
        CapD = 0x1002,
        SpeCapC = 0x1003,
        SpeCapD = 0x1004,
        Effi = 0x1005,
        EnergyC = 0x1006,
        EnergyD = 0x1007,
        MedVoltD = 0x1008,
        CCCCap = 0x1009,
        CCCPer = 0x100A,
        PlatCapD = 0x100B,
        PlatSpeCapD = 0x100C,
        PlatPerD = 0x100D,
        PlatTimeD = 0x100E,
        CaptnC = 0x100F,
        CaptnD = 0x1010,
        Resistance = 0x1011,
        Resistance2 = 0x1012,
        SpeEnergyC = 0x1013,
        SpeEnergyD = 0x1014,
        EndVoltD = 0x1015,
        RetentionD = 0x1016,
        AveDCIR_C = 0x1017,
        AveDCIR_D = 0x1018,
        MedVoltC = 0x1019
    }

    public enum STEPColumns : uint
    {
        Index = 0x2001,
        Mode = 0x2002,
        Period = 0x2003,
        Capacity = 0x2004,
        SpeCapacity = 0x2005,
        Energy = 0x2006,
        Capacitance = 0x2007,
        SpeEnergy = 0x2008,
        MedVolt = 0x2009,
        StartVolt = 0x200a,
        EndVolt = 0x200b,
    }

    public enum RECColumns : uint
    {
        Index = 0x4001,
        TestTime = 0x4002,
        StepTime = 0x4003,
        Current = 0x4004,
        Capacity = 0x4005,
        SpeCapacity = 0x4006,
        SocDod = 0x4007,
        Voltage = 0x4008,
        Energy = 0x4009,
        SpeEnergy = 0x400A,
        SysTime = 0x400B,
        SysTimeLong = 0x400f,
        SysTimeAsLong = 0x4010
    }

    public enum PMColumns : uint
    {
        PM_CONST_CURRENT_DISCHARGE = 0x2,
        PM_CONST_CURRENT_CHARGE = 0x3,
        PM_CONST_VOLT_DISCHARGE = 0x4,
        PM_CONST_VOLT_CHARGE = 0x5,
        PM_STEP_STOP = 0x6,
        PM_CONST_POWER_DISCHARGE = 0x13,
        PM_CONST_POWER_CHARGE = 0x14,
        PM_CONST_RESISTANCE_DISCHARGE = 0x16,
        PM_FLASH_DCIR = 0x1A,
        PM_STEP_REST = 0x70,
        PM_STEP_SYN_SUSPEND = 0x74,
        PM_STEP_C_RATE_DISCHARGE = 0x75,
        PM_STEP_C_RATE_CHARGE = 0x76,
    }

    public enum CHLSS
    {
        CHLSS_BaseOnData = 0,
        CHLSS_BaseOnConnect = 1,
        CHLSS_BaseOnDataAndConnect = 2,
    }
}
