using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Python.Runtime.Native
{
    [StructLayout(LayoutKind.Sequential)]
    struct NativeTypeSpec : IDisposable
    {
        public readonly StrPtr Name;
        public readonly int BasicSize;
        public readonly int ItemSize;
        public readonly int Flags;
        public IntPtr Slots;

        public NativeTypeSpec(TypeSpec spec)
        {
            if (spec is null) throw new ArgumentNullException(nameof(spec));

            Name = new StrPtr(spec.Name, Encoding.UTF8);
            BasicSize = spec.BasicSize;
            ItemSize = spec.ItemSize;
            Flags = (int)spec.Flags;

            unsafe
            {
                int slotsBytes = checked((spec.Slots.Count + 1) * Marshal.SizeOf<TypeSpec.Slot>());
                var slots = (TypeSpec.Slot*)Marshal.AllocHGlobal(slotsBytes);
                for (int slotIndex = 0; slotIndex < spec.Slots.Count; slotIndex++)
                    slots[slotIndex] = spec.Slots[slotIndex];
                slots[spec.Slots.Count] = default;
                Slots = (IntPtr)slots;
            }
        }

        public void Dispose()
        {
            // we have to leak the name
            // Name.Dispose();
            Marshal.FreeHGlobal(Slots);
            Slots = IntPtr.Zero;
        }
    }
}
