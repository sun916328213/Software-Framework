using FastDeepCloner;

namespace Extension.Core.DataEx
{
    public static class CloneEx
    {
        public static T DeepClone<T>(this T obj)
        {
            return (T)((object)obj).Clone();
        }
    }
}
