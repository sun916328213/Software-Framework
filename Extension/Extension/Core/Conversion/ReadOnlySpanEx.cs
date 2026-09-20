using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Core.Conversion
{
    public static class ReadOnlySpanEx
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="startIndex"></param>
        /// <param name="byteCount"></param>
        /// <param name="isLE"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static uint ToUInt(this ReadOnlySpan<byte> value, int startIndex, int byteCount, bool isLE = true)
        {
            if (byteCount < 1 || byteCount > 4)
                throw new ArgumentOutOfRangeException(nameof(byteCount));

            if (startIndex < 0 || startIndex + byteCount > value.Length)
                throw new ArgumentOutOfRangeException(nameof(startIndex));

            uint result = 0;

            if (isLE)
            {
                for (int i = 0; i < byteCount; i++)
                    result |= (uint)value[startIndex + i] << (8 * i);
            }
            else
            {
                for (int i = 0; i < byteCount; i++)
                    result = (result << 8) | value[startIndex + i];
            }

            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="isLE"></param>
        /// <returns></returns>
        public static uint ToUInt(this ReadOnlySpan<byte> value, bool isLE = true)
        {
            return value.ToUInt(0, value.Length, isLE);

        }

    }
}


