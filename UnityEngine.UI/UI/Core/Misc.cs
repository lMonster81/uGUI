namespace UnityEngine.UI
{
    // Helper class containing generic functions used throughout the UI library.
    // 整个UI库使用的、包含通用方法的帮助类。（杂项工具类
    internal static class Misc
    {
        // Destroy the specified object, immediately if in edit mode.
        // 销毁指定对象，编辑器下立即销毁
        // 疑问：感觉实现和注释的意思不一致。也没有找到调用它的地方可以验证。
        // Application.isPlaying: 已构建播放器中始终返回true。在编辑器中处于播放模式也返回true（非 Prefab Mode）。
        // 参考 https://docs.unity3d.com/ScriptReference/Application.IsPlaying.html
        static public void Destroy(UnityEngine.Object obj)
        {
            if (obj != null)
            {
                if (Application.isPlaying)
                {
                    if (obj is GameObject)
                    {
                        GameObject go = obj as GameObject;
                        go.transform.parent = null;
                    }

                    Object.Destroy(obj);
                }
                else Object.DestroyImmediate(obj);
            }
        }

        // Destroy the specified object immediately, unless not in the editor, in which case the regular Destroy is used instead.
        // 编辑器下执行立即销毁，否则执行常规销毁。
        static public void DestroyImmediate(Object obj)
        {
            if (obj != null)
            {
                if (Application.isEditor) Object.DestroyImmediate(obj);
                else Object.Destroy(obj);
            }
        }
    }
}
