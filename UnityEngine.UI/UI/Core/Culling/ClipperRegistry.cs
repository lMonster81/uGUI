using System.Collections.Generic;
using UnityEngine.UI.Collections;

namespace UnityEngine.UI
{
    // Registry class to keep track of all IClippers that exist in the scene
    // This is used during the CanvasUpdate loop to cull clippable elements. The clipping is called after layout, but before Graphic update.
    // 一个用于跟踪场景中所有 实现了 IClipper 接口的对象的 注册表类。
    // 这是在 CanvasUpdate 循环期间用来剔除可剪切元素的。方法在 LayoutUpdate 与 GraphicUpdate 之间被调用。
    public class ClipperRegistry
    {
        static ClipperRegistry s_Instance;

        readonly IndexedSet<IClipper> m_Clippers = new IndexedSet<IClipper>();

        protected ClipperRegistry()
        {
            // This is needed for AOT platforms. Without it the compile doesn't get the definition of the Dictionarys
            // 这是 AOT 平台所需要的。没有它，编译器就得不到字典的的定义。
            // 疑问???
#pragma warning disable 168
            Dictionary<IClipper, int> emptyIClipperDic;
#pragma warning restore 168
        }

        // The singleton instance of the clipper registry.
        // ClipperRegistry 单例
        public static ClipperRegistry instance
        {
            get
            {
                if (s_Instance == null)
                    s_Instance = new ClipperRegistry();
                return s_Instance;
            }
        }

        // Perform the clipping on all registered IClipper
        // 在所有注册的 IClipper 上执行裁剪
        public void Cull()
        {
            for (var i = 0; i < m_Clippers.Count; ++i)
            {
                m_Clippers[i].PerformClipping();
            }
        }

        // Register a unique IClipper element
        // 注册一个特定 IClipper 元素
        public static void Register(IClipper c)
        {
            if (c == null)
                return;
            instance.m_Clippers.AddUnique(c);
        }

        // UnRegister a IClipper element
        // 将 IClipper 元素从注册中移除
        public static void Unregister(IClipper c)
        {
            instance.m_Clippers.Remove(c);
        }
    }
}
