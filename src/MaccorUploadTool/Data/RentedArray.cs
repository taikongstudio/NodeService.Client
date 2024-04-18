using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaccorUploadTool.Data
{
    public readonly struct RentedArray<T> : IDisposable
    {
        private readonly T[] _array;

        public static readonly RentedArray<T> Empty = new(0);

        public RentedArray(int size)
        {
            this._array = size == 0 ? [] : ArrayPool<T>.Shared.Rent(size);
        }

        public bool HasValue { get { return _array != null && _array.Length > 0; } }

        public T[] Value { get { return _array; } }

        public void Dispose()
        {
            ArrayPool<T>.Shared.Return(this._array, true);
        }
    }
}
