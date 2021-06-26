using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.UI
{
    //用 List<T> 作为 ObjectPool 的 T。
    internal static class ListPool<T>
    {
        // Object pool to avoid allocations.
        private static readonly ObjectPool<List<T>> s_ListPool = new ObjectPool<List<T>>(null, Clear);
        static void Clear(List<T> l) { l.Clear(); }     //设置放回回调：清理 List

        public static List<T> Get()
        {
            return s_ListPool.Get();
        }

        public static void Release(List<T> toRelease)
        {
            s_ListPool.Release(toRelease);
        }
    }
}
