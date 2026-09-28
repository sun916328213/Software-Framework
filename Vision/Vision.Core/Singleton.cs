using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vision.Core
{
    public abstract class Singleton<T> where T : class
    {
        private static readonly Lazy<T> mLazy =
            new Lazy<T>(() => (T)Activator.CreateInstance(typeof(T), nonPublic: true));

        public static T Instance => mLazy.Value;
    }
}
