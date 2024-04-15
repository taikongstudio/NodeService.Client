using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LANDCexSharp
{
    public class LANDCexDataReader : IDisposable
    {
        private nint _handle;

        private LANDCexDataReader(nint handle)
        {
            _handle = handle;
        }

        public static bool CreateReader(string path, USCH usch, out LANDCexDataReader dataReader)
        {
            dataReader = null;
            if (!NativeMethods.CheckDllVersion(0x00030102))
            {
                return false;
            }
            nint handle = NativeMethods.LoadData(path, (int)usch);
            dataReader = new LANDCexDataReader(handle);
            return true;
        }

        public void Dispose()
        {
            NativeMethods.FreeData(this._handle);
        }



        public IEnumerable<Cycle> EnumCycles()
        {

            var rowCount = NativeMethods.GetRows(this._handle, (uint)CYCLEColumns.Index);
            for (int i = 0; i < rowCount; i++)
            {
                Cycle cycle = new Cycle();
                ParseCycle(i, cycle);
                yield return cycle;
            }
            yield break;
        }

        public IEnumerable<Rec> EnumRecs(int cycleIndex)
        {
            int firstIndex = NativeMethods.GetFirstRecOfCycle(this._handle, cycleIndex);
            int lastIndex = NativeMethods.GetLastRecOfCycle(this._handle, cycleIndex);
            for (int index = firstIndex; index < lastIndex; index++)
            {
                Rec rec = new Rec();
                ParseRec(index, rec);
                yield return rec;
            }
        }

        public IEnumerable<Rec> EnumChargeRecs(int cycleIndex)
        {
            int firstIndex = NativeMethods.GetFirstChargeRecOfCycle(this._handle, cycleIndex);
            int lastIndex = NativeMethods.GetLastChargeRecOfCycle(this._handle, cycleIndex);
            for (int index = firstIndex; index < lastIndex; index++)
            {
                Rec rec = new Rec();
                ParseRec(index, rec);
                yield return rec;
            }
        }

        public IEnumerable<Rec> EnumDischRecs(int cycleIndex)
        {
            int firstIndex = NativeMethods.GetFirstDischRecOfCycle(this._handle, cycleIndex);
            int lastIndex = NativeMethods.GetLastDischRecOfCycle(this._handle, cycleIndex);
            for (int index = firstIndex; index < lastIndex; index++)
            {
                Rec rec = new Rec();
                ParseRec(index, rec);

                yield return rec;
            }
        }

        public IEnumerable<Step> EnumChargeSteps(int cycleIndex)
        {
            int firstIndex = NativeMethods.GetFirstChargeStepOfCycle(this._handle, cycleIndex);
            int lastIndex = NativeMethods.GetLastChargeStepOfCycle(this._handle, cycleIndex);
            for (int index = firstIndex; index < lastIndex; index++)
            {
                Step step = ParseStep(index);

                yield return step;
            }
        }

        public IEnumerable<Step> EnumDischSteps(int cycleIndex)
        {
            int firstIndex = NativeMethods.GetFirstDischStepOfCycle(this._handle, cycleIndex);
            int lastIndex = NativeMethods.GetLastDischStepOfCycle(this._handle, cycleIndex);
            for (int index = firstIndex; index < lastIndex; index++)
            {
                Step step = ParseStep(index);

                yield return step;
            }

        }

        public IEnumerable<Step> EnumSteps(int cycleIndex)
        {
            int firstIndex = NativeMethods.GetFirstStepOfCycle(this._handle, cycleIndex);
            int lastIndex = NativeMethods.GetLastStepOfCycle(this._handle, cycleIndex);
            for (int index = firstIndex; index < lastIndex; index++)
            {
                Step step = ParseStep(index);

                yield return step;
            }
        }

        private unsafe Step ParseStep(int index)
        {
            Step step = new Step();
            Variant variant = default;
            NativeMethods.GetDataEx(this._handle, (uint)STEPColumns.Index, index, &variant);
            step.Index = variant.Int32Value;
            NativeMethods.GetDataEx(this._handle, (uint)STEPColumns.Mode, index, &variant);
            step.Mode = variant.UInt8Value;
            NativeMethods.GetDataEx(this._handle, (uint)STEPColumns.Period, index, &variant);
            step.Period = variant.SingleValue;
            NativeMethods.GetDataEx(this._handle, (uint)STEPColumns.Capacity, index, &variant);
            step.Capacity = variant.SingleValue;
            NativeMethods.GetDataEx(this._handle, (uint)STEPColumns.SpeCapacity, index, &variant);
            step.SpeCapacity = variant.SingleValue;
            NativeMethods.GetDataEx(this._handle, (uint)STEPColumns.Energy, index, &variant);
            step.Energy = variant.SingleValue;
            NativeMethods.GetDataEx(this._handle, (uint)STEPColumns.Capacitance, index, &variant);
            step.Capacitance = variant.SingleValue;
            NativeMethods.GetDataEx(this._handle, (uint)STEPColumns.SpeEnergy, index, &variant);
            step.SpeEnergy = variant.SingleValue;
            NativeMethods.GetDataEx(this._handle, (uint)STEPColumns.MedVolt, index, &variant);
            step.MedVolt = variant.SingleValue;
            NativeMethods.GetDataEx(this._handle, (uint)STEPColumns.StartVolt, index, &variant);
            step.StartVolt = variant.SingleValue;
            NativeMethods.GetDataEx(this._handle, (uint)STEPColumns.EndVolt, index, &variant);
            step.EndVolt = variant.SingleValue;
            return step;
        }

        private unsafe Rec ParseRec(int index, Rec rec)
        {
            Variant variant = default;
            NativeMethods.GetDataEx(this._handle, (uint)RECColumns.Index, index, &variant);
            rec.Index = variant.Int32Value;
            NativeMethods.GetDataEx(this._handle, (uint)RECColumns.TestTime, index, &variant);
            rec.TestTime = variant.DoubleValue;
            NativeMethods.GetDataEx(this._handle, (uint)RECColumns.StepTime, index, &variant);
            rec.StepTime = variant.DoubleValue;
            NativeMethods.GetDataEx(this._handle, (uint)RECColumns.Current, index, &variant);
            rec.Current = variant.SingleValue;
            NativeMethods.GetDataEx(this._handle, (uint)RECColumns.Capacity, index, &variant);
            rec.Capacity = variant.SingleValue;
            NativeMethods.GetDataEx(this._handle, (uint)RECColumns.SpeCapacity, index, &variant);
            rec.SpeCapacity = variant.SingleValue;
            NativeMethods.GetDataEx(this._handle, (uint)RECColumns.SocDod, index, &variant);
            rec.SocDod = variant.SingleValue;
            NativeMethods.GetDataEx(this._handle, (uint)RECColumns.Voltage, index, &variant);
            rec.Voltage = variant.SingleValue;
            NativeMethods.GetDataEx(this._handle, (uint)RECColumns.Energy, index, &variant);
            rec.Energy = variant.SingleValue;
            NativeMethods.GetDataEx(this._handle, (uint)RECColumns.SpeEnergy, index, &variant);
            rec.SpeEnergy = variant.SingleValue;
            NativeMethods.GetDataEx(this._handle, (uint)RECColumns.SysTime, index, &variant);
            rec.SysTime = new string(Marshal.PtrToStringBSTR(variant.PtrValue));
            NativeMethods.GetDataEx(this._handle, (uint)RECColumns.SysTimeLong, index, &variant);
            rec.SysTimeLong = DateTimeOffset.FromUnixTimeSeconds(variant.Int32Value).DateTime;
            //NativeMethods.GetDataEx(this._handle, (uint)RECColumns.SysTimeAsLong, recIndex, &variant);
            //rec.SysTimeAsLong = variant.Int64Value;
            return rec;
        }

        private unsafe Cycle ParseCycle(int index, Cycle cycle)
        {
            Variant variant = default;
            bool result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.Index, index, &variant);
            cycle.Index = variant.Int32Value;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.CapC, index, &variant);
            cycle.CapC = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.CapD, index, &variant);
            cycle.CapD = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.SpeCapC, index, &variant);
            cycle.SpeCapC = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.SpeCapD, index, &variant);
            cycle.SpeCapD = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.Effi, index, &variant);
            cycle.Effi = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.EnergyC, index, &variant);
            cycle.EnergyC = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.EnergyD, index, &variant);
            cycle.EnergyD = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.MedVoltD, index, &variant);
            cycle.MedVoltD = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.CCCCap, index, &variant);
            cycle.CCCCap = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.CCCPer, index, &variant);
            cycle.CCCPer = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.PlatCapD, index, &variant);
            cycle.PlatCapD = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.PlatSpeCapD, index, &variant);
            cycle.PlatSpeCapD = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.PlatPerD, index, &variant);
            cycle.PlatPerD = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.PlatTimeD, index, &variant);
            cycle.PlatTimeD = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.CaptnC, index, &variant);
            cycle.CaptnC = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.CaptnD, index, &variant);
            cycle.CaptnD = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.MedVoltD, index, &variant);
            cycle.MedVoltD = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.Resistance, index, &variant);
            cycle.Resistance = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.Resistance2, index, &variant);
            cycle.Resistance2 = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.SpeEnergyC, index, &variant);
            cycle.SpeEnergyC = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.SpeEnergyD, index, &variant);
            cycle.SpeEnergyD = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.EndVoltD, index, &variant);
            cycle.EndVoltD = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.RetentionD, index, &variant);
            cycle.RetentionD = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.AveDCIR_C, index, &variant);
            cycle.AveDCIR_C = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.AveDCIR_D, index, &variant);
            cycle.AveDCIR_D = variant.SingleValue;
            result = NativeMethods.GetDataEx(_handle, (uint)CYCLEColumns.MedVoltC, index, &variant);
            cycle.MedVoltC = variant.SingleValue;

            int numOfProcedures = NativeMethods.GetNumOfProcedure(_handle, index);
            cycle.Procedures = new string[numOfProcedures];
            for (int i = 0; i < numOfProcedures; i++)
            {
                NativeMethods.GetProcedureName(_handle, &variant, i);
                cycle.Procedures[i] = Marshal.PtrToStringBSTR(variant.PtrValue);
            }
            return cycle;
        }
    }
}
