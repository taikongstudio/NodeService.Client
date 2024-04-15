﻿using System;
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

        public RentedArray(T[] array)
        {
            this._array = array;
        }

        public bool HasValue {  get { return _array != null; } }

        public T[] Value { get { return _array; } }

        public void Dispose()
        {
            ArrayPool<T>.Shared.Return(this._array, true);
        }
    }
}
