using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Core.Comparison
{
    /// <summary>
    /// 比对速度模式
    /// </summary>
    public enum ECompareMode
    {
        /// <summary>
        /// 8位
        /// </summary>
        Eight = 0,
        /// <summary>
        /// 16位
        /// </summary>
        Sixteen = 1,
        /// <summary>
        /// 32位
        /// </summary>
        Thirty_Two = 2,
        /// <summary>
        /// 64位
        /// </summary>
        Sixty_Four = 3,
    }
    /// <summary>
    /// 比对极性
    /// </summary>
    public enum EComparePolarity
    {
        /// <summary>
        /// 上升沿
        /// </summary>
        RisingEdge,
        /// <summary>
        /// 下降沿
        /// </summary>
        FallingEdge,
        // <summary>
        /// 上下沿
        /// </summary>
        BothEdges,
        /// <summary>
        /// 无极性 不进行对比
        /// </summary>
        None
    }

    /// <summary>
    /// 比较值
    /// </summary>
    public struct  CompareValue
    {
        /// <summary>
        /// 索引
        /// </summary>
        public int Index { get; set; }
        // <summary>
        /// 当前值
        /// </summary>
        public object Value_Current { get; set; }
        /// <summary>
        /// 上次值
        /// </summary>
        public object Value_Last { get; set; }
    }
    public static class BitOperationEx
    {
        /// <summary>
        /// 安全的指针方式对比两个字节数组，并返回对比结果
        /// </summary>
        /// <param name="b1">待对比的字节数组1</param>
        /// <param name="b2">待对比的字节数组2</param>
        /// <param name="pCompareMode">对比极性模式：上升沿/下降沿/双边沿</param>
        /// <param name="pCompareSpeed">对比速度模式（按每次处理8/16/32/64位字节）</param>
        /// <returns>包含对比结果的SResult对象，Value为差异点列表</returns>
        /// <exception cref="Exception">输入数组为空或长度不一致时抛出</exception>
        public static unsafe List<CompareValue> Compare_Ptr(byte[] b1, byte[] b2,EComparePolarity pCompareMode = EComparePolarity.BothEdges,ECompareMode pCompareSpeed = 0)
        {
            List<CompareValue> result = new List<CompareValue>();
            List<CompareValue> pCompareResult = new List<CompareValue>();

            // 输入校验
            if (b1 == null || b2 == null)
            {
                throw new Exception("输入的两个数组不能为空。");
            }
            if (b1.Length != b2.Length)
            {
                throw new Exception("输入的两个数组个数不相等。");
            }

            // 固定指针并调用底层对比逻辑
            fixed (byte* b1_Ptr = b1, b2_Ptr = b2)
                ArrayCompare(out pCompareResult, b1_Ptr, b2_Ptr, pCompareMode, b1.Length, 0, pCompareSpeed);

            result = pCompareResult;
            return result;
        }

        /// <summary>
        /// 安全的指针方式对比两个短整型数组，并返回对比结果
        /// </summary>
        /// <param name="b1">待对比的短整型数组1</param>
        /// <param name="b2">待对比的短整型数组2</param>
        /// <param name="pCompareMode">对比极性模式：上升沿/下降沿/双边沿</param>
        /// <param name="pCompareSpeed">对比速度模式（按每次处理8/16/32/64位字节）</param>
        /// <returns>包含对比结果的SResult对象，Value为差异点列表</returns>
        /// <exception cref="Exception">输入数组为空或长度不一致时抛出</exception>
        public static unsafe List<CompareValue> Compare_Ptr(ushort[] b1, ushort[] b2, EComparePolarity pCompareMode = EComparePolarity.BothEdges, ECompareMode pCompareSpeed = 0)
        {
            List<CompareValue> result = new List<CompareValue>();
            List<CompareValue> pCompareResult = new List<CompareValue>();

            // 输入校验
            if (b1 == null || b2 == null)
            {
                throw new Exception("输入的两个数组不能为空。");
            }
            if (b1.Length != b2.Length)
            {
                throw new Exception("输入的两个数组个数不相等。");
            }

            // 固定指针并调用底层对比逻辑
            fixed (ushort* b1_Ptr = b1, b2_Ptr = b2)
                ArrayCompare(out pCompareResult, b1_Ptr, b2_Ptr, pCompareMode, b1.Length, 0, pCompareSpeed);

            result = pCompareResult;
            return result;
        }

        /// <summary>
        /// 安全的指针方式对比两个整型数组，并返回对比结果
        /// </summary>
        /// <param name="b1">待对比的整型数组1</param>
        /// <param name="b2">待对比的整型数组2</param>
        /// <param name="pCompareMode">对比极性模式：上升沿/下降沿/双边沿</param>
        /// <param name="pCompareSpeed">对比速度模式（按每次处理8/16/32/64位字节）</param>
        /// <returns>包含对比结果的SResult对象，Value为差异点列表</returns>
        /// <exception cref="Exception">输入数组为空或长度不一致时抛出</exception>
        public static unsafe List<CompareValue> Compare_Ptr(short[] b1, short[] b2,EComparePolarity pCompareMode = EComparePolarity.BothEdges,ECompareMode pCompareSpeed = 0)
        {
            List<CompareValue> result = new List<CompareValue>();
            List<CompareValue> pCompareResult = new List<CompareValue>();

            // 输入校验
            if (b1 == null || b2 == null)
            {
                throw new Exception("输入的两个数组不能为空。");
            }
            if (b1.Length != b2.Length)
            {
                throw new Exception("输入的两个数组个数不相等。");
            }

            // 固定指针并调用底层对比逻辑
            fixed (short* b1_Ptr = b1, b2_Ptr = b2)
                ArrayCompare(out pCompareResult, b1_Ptr, b2_Ptr, pCompareMode, b1.Length, 0, pCompareSpeed);

            result = pCompareResult;
            return result;
        }

        /// <summary>
        /// 安全的指针方式对比两个整型数组，并返回对比结果
        /// </summary>
        /// <param name="b1">待对比的整型数组1</param>
        /// <param name="b2">待对比的整型数组2</param>
        /// <param name="pCompareMode">对比极性模式：上升沿/下降沿/双边沿</param>
        /// <param name="pCompareSpeed">对比速度模式（按每次处理8/16/32/64位字节）</param>
        /// <returns>包含对比结果的SResult对象，Value为差异点列表</returns>
        /// <exception cref="Exception">输入数组为空或长度不一致时抛出</exception>
        public static unsafe List<CompareValue> Compare_Ptr(bool[] b1, bool[] b2, EComparePolarity pCompareMode = EComparePolarity.BothEdges, ECompareMode pCompareSpeed = 0)
        {
            List<CompareValue> result = new List<CompareValue>();
            List<CompareValue> pCompareResult = new List<CompareValue>();

            // 输入校验
            if (b1 == null || b2 == null)
            {
                throw new Exception("输入的两个数组不能为空。");
            }
            if (b1.Length != b2.Length)
            {
                throw new Exception("输入的两个数组个数不相等。");
            }

            // 固定指针并调用底层对比逻辑
            fixed (bool* b1_Ptr = b1, b2_Ptr = b2)
                ArrayCompare(out pCompareResult, b1_Ptr, b2_Ptr, pCompareMode, b1.Length, 0, pCompareSpeed);

            result = pCompareResult;
            return result;
        }


        /// <summary>
        /// 底层数组对比逻辑（字节数组版本）
        /// </summary>
        /// <param name="pCompareResult">输出：差异点列表</param>
        /// <param name="pValue1">输入：数组1的指针（字节类型）</param>
        /// <param name="pValue2">输入：数组2的指针（字节类型）</param>
        /// <param name="pCompareMode">对比极性模式</param>
        /// <param name="pLength">数组长度</param>
        /// <param name="pIndexOffset">起始索引偏移量</param>
        /// <param name="pCompareSpeed">对比速度模式（按每次处理8/16/32/64位字节）</param>
        /// <typeparam name="T">元素类型（必须为非托管类型；调用方传的都是 byte*/ushort*/short*/bool*）</typeparam>
        /// <remarks>
        /// ⛔ 序 3b（2026-09-18 修）：原方法**没有** <c>where T : unmanaged</c>，而形参是 <c>T*</c>
        /// ⇒ 编译器报 CS8500（"采用指向托管类型的指针"）。该约束只**收紧**调用面，
        /// 现有 4 个调用点（byte*/ushort*/short*/bool*）与内部递归调用全部满足，**运行时行为零变化**。
        /// </remarks>
        unsafe static void ArrayCompare<T>(out List<CompareValue> pCompareResult, T* pValue1, T* pValue2,
            EComparePolarity pCompareMode, int pLength, int pIndexOffset = 0,
            ECompareMode pCompareSpeed = ECompareMode.Sixteen) where T : unmanaged
        {
            pCompareResult = new List<CompareValue>();

            // 计算每次处理的字节数（2^pCompareSpeed）
            int L = (int)Math.Pow(2, (int)pCompareSpeed);
            T* EndAddress = pValue1 + pLength;          // 数组末尾指针
            T* LastAddress = EndAddress - L;            // 最后一个可完整处理块的起始指针

            int currentIndex = pIndexOffset;               // 当前处理的起始索引

            // 按块处理：每次处理L字节（如16位模式下每次处理2字节）
            while (pValue1 <= LastAddress)
            {
                switch (pCompareSpeed)
                {
                    case ECompareMode.Eight:       // 单字节处理（8位）
                        Handle(ref pCompareResult, currentIndex, 0, pValue1, pValue2, pCompareMode);
                        break;
                    case ECompareMode.Sixteen:     // 双字节处理（16位）
                                                   // 按16位对比，异或找出差异字节
                        ushort v1 = *(ushort*)pValue1;
                        ushort v2 = *(ushort*)pValue2;
                        if (v1 != v2)
                        {
                            ushort xor = (ushort)(v1 ^ v2);
                            // 逐字节检查差异
                            for (int i = 0; i < L; i++)
                            {
                                if (((xor >> (L * i)) & 0xff) > 0)
                                    Handle(ref pCompareResult, currentIndex, i, pValue1, pValue2, pCompareMode);
                            }
                        }
                        break;
                    // 类似处理32位和64位模式（逻辑与16位一致）
                    case ECompareMode.Thirty_Two:
                        uint v32 = *(uint*)pValue1;
                        uint v32_2 = *(uint*)pValue2;
                        if (v32 != v32_2)
                        {
                            uint xor32 = v32 ^ v32_2;
                            for (int i = 0; i < L; i++)
                            {
                                if (((xor32 >> (L * i)) & 0xff) > 0)
                                    Handle(ref pCompareResult, currentIndex, i, pValue1, pValue2, pCompareMode);
                            }
                        }
                        break;
                    case ECompareMode.Sixty_Four:
                        ulong v64 = *(ulong*)pValue1;
                        ulong v64_2 = *(ulong*)pValue2;
                        if (v64 != v64_2)
                        {
                            ulong xor64 = v64 ^ v64_2;
                            for (int i = 0; i < L; i++)
                            {
                                if (((xor64 >> (L * i)) & 0xff) > 0)
                                    Handle(ref pCompareResult, currentIndex, i, pValue1, pValue2, pCompareMode);
                            }
                        }
                        break;
                }

                // 指针后移L字节，索引增加L
                currentIndex += L;
                pValue1 += L;
                pValue2 += L;
            }

            // 递归处理剩余不完整块（降速处理）
            pCompareSpeed = (ECompareMode)((int)pCompareSpeed - 1);
            if (pCompareSpeed >= 0)
            {
                List<CompareValue> remainingDifferences;
                int remainingLength = (int)(EndAddress - pValue1);
                ArrayCompare(out remainingDifferences, pValue1, pValue2, pCompareMode, remainingLength, currentIndex, pCompareSpeed);
                pCompareResult.AddRange(remainingDifferences);
            }
        }



        /// <summary>
        /// 处理单个差异点，根据对比模式判断是否记录
        /// </summary>
        /// <typeparam name="T">值类型（必须为非托管类型且可比较；⛔ 序 3b 起由 <c>where T : unmanaged</c> 强制）</typeparam>
        /// <param name="pCompareResult">差异点列表</param>
        /// <param name="pStartIndex">当前块起始索引</param>
        /// <param name="i">块内偏移量</param>
        /// <param name="pValue1">数组1指针</param>
        /// <param name="pValue2">数组2指针</param>
        /// <param name="pCompareMode">对比极性模式</param>
        /// <remarks>
        /// ⛔ 序 3b（2026-09-18 修）：补 <c>where T : unmanaged</c> 消掉 CS8500。
        /// ⚠️ 该方法只由 <see cref="ArrayCompare{T}"/> 调用，两者约束必须**同时**加 —— 只加一个只消掉一半警告。
        /// </remarks>
        private unsafe static void Handle<T>(ref List<CompareValue> pCompareResult, int pStartIndex, int i,
         T* pValue1, T* pValue2, EComparePolarity pCompareMode) where T : unmanaged
        {
            // 计算当前元素索引  
            int index = pStartIndex + i;
            // 读取当前元素值  
            T current = *(pValue1 + i);
            T last = *(pValue2 + i);

            // 根据对比模式判断是否记录差异  
            switch (pCompareMode)
            {
                case EComparePolarity.RisingEdge:   // 上升沿（当前值 > 前值）  
                    if (Comparer<T>.Default.Compare(current, last) > 0)
                    {
                        pCompareResult.Add(new CompareValue
                        {
                            Index = index,
                            Value_Current = current,
                            Value_Last = last
                        });
                    }
                    break;
                case EComparePolarity.FallingEdge:  // 下降沿（当前值 < 前值）  
                    if (Comparer<T>.Default.Compare(current, last) < 0)
                    {
                        pCompareResult.Add(new CompareValue
                        {
                            Index = index,
                            Value_Current = current,
                            Value_Last = last
                        });
                    }
                    break;
                case EComparePolarity.BothEdges:    // 双边沿（只要不相等就记录）  
                    if (Comparer<T>.Default.Compare(current, last) != 0)
                    {
                        pCompareResult.Add(new CompareValue
                        {
                            Index = index,
                            Value_Current = current,
                            Value_Last = last
                        });
                    }
                    break;
            }
        }
    }
}

