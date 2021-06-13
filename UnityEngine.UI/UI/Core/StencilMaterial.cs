using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.UI
{
    // Dynamic material class makes it possible to create custom materials on the fly on a per-Graphic basis, and still have them get cleaned up correctly.
    // 模板测试材质管理类，可为每个 Graphic 动态地创建其自定义的模板测试材质，并使它们能够得到正确清理。
    public static class StencilMaterial
    {
        private class MatEntry
        {
            public Material baseMat;
            public Material customMat;
            public int count;

            public int stencilId;
            public StencilOp operation = StencilOp.Keep;
            public CompareFunction compareFunction = CompareFunction.Always;
            public int readMask;
            public int writeMask;
            public bool useAlphaClip;
            public ColorWriteMask colorMask;
        }

        private static List<MatEntry> m_List = new List<MatEntry>();    //保存自定义模板测试材质项的列表

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [Obsolete("Use Material.Add instead.", true)]
        public static Material Add(Material baseMat, int stencilID) { return null; }

        // Add a new material using the specified base and stencil ID.
        // 用指定的 baseMat 和 stencilID 生成一个新的材质。
        // 重载方法。
        public static Material Add(Material baseMat, int stencilID, StencilOp operation, CompareFunction compareFunction, ColorWriteMask colorWriteMask)
        {
            return Add(baseMat, stencilID, operation, compareFunction, colorWriteMask, 255, 255);
        }

        // Add a new material using the specified base and stencil ID.
        // 用指定的 baseMat 和 stencilID 生成一个新的材质。
        // 重载方法。
        public static Material Add(Material baseMat, int stencilID, StencilOp operation, CompareFunction compareFunction, ColorWriteMask colorWriteMask, int readMask, int writeMask)
        {
            if ((stencilID <= 0 && colorWriteMask == ColorWriteMask.All) || baseMat == null)
                return baseMat;     //参数无效，返回 baseMat

            if (!baseMat.HasProperty("_Stencil"))
            {
                Debug.LogWarning("Material " + baseMat.name + " doesn't have _Stencil property", baseMat);
                return baseMat;     //baseMat 不包含属性 "_Stencil"，返回 baseMat。
            }
            if (!baseMat.HasProperty("_StencilOp"))
            {
                Debug.LogWarning("Material " + baseMat.name + " doesn't have _StencilOp property", baseMat);
                return baseMat;      //baseMat 不包含属性 "_StencilOp"，返回 baseMat。
            }
            if (!baseMat.HasProperty("_StencilComp"))
            {
                Debug.LogWarning("Material " + baseMat.name + " doesn't have _StencilComp property", baseMat);
                return baseMat;      //baseMat 不包含属性 "_StencilComp"，返回 baseMat。
            }
            if (!baseMat.HasProperty("_StencilReadMask"))
            {
                Debug.LogWarning("Material " + baseMat.name + " doesn't have _StencilReadMask property", baseMat);
                return baseMat;      //baseMat 不包含属性 "_StencilReadMask"，返回 baseMat。
            }
            if (!baseMat.HasProperty("_StencilWriteMask"))
            {
                Debug.LogWarning("Material " + baseMat.name + " doesn't have _StencilWriteMask property", baseMat);
                return baseMat;     //baseMat 不包含属性 "_StencilWriteMask"，返回 baseMat。
            }
            if (!baseMat.HasProperty("_ColorMask"))
            {
                Debug.LogWarning("Material " + baseMat.name + " doesn't have _ColorMask property", baseMat);
                return baseMat;     //baseMat 不包含属性 "_ColorMask"，返回 baseMat。
            }

            for (int i = 0; i < m_List.Count; ++i)
            {
                MatEntry ent = m_List[i];

                if (ent.baseMat == baseMat
                    && ent.stencilId == stencilID
                    && ent.operation == operation
                    && ent.compareFunction == compareFunction
                    && ent.readMask == readMask
                    && ent.writeMask == writeMask
                    && ent.colorMask == colorWriteMask)
                {
                    ++ent.count;                //引用计数+1
                    return ent.customMat;       //若已存在相同的，则返回已有的。
                }
            }

            //创建新的
            var newEnt = new MatEntry();
            newEnt.count = 1;                   //引用计数=1
            newEnt.baseMat = baseMat;
            newEnt.customMat = new Material(baseMat);
            newEnt.customMat.hideFlags = HideFlags.HideAndDontSave; //不在层级面板上显示并不能保存到场景（通常用于物体由某脚本创建并纯粹在它的控制之下，不随场景销毁）。
            newEnt.stencilId = stencilID;
            newEnt.operation = operation;
            newEnt.compareFunction = compareFunction;
            newEnt.readMask = readMask;
            newEnt.writeMask = writeMask;
            newEnt.colorMask = colorWriteMask;
            newEnt.useAlphaClip = operation != StencilOp.Keep && writeMask > 0; //只要模板测试操作不是Keep 且 writeMask > 0，则启用透明度裁剪。

            //以具体参数命名
            newEnt.customMat.name = string.Format("Stencil Id:{0}, Op:{1}, Comp:{2}, WriteMask:{3}, ReadMask:{4}, ColorMask:{5} AlphaClip:{6} ({7})", stencilID, operation, compareFunction, writeMask, readMask, colorWriteMask, newEnt.useAlphaClip, baseMat.name);

            //设置参数到新的材质上
            //具体要查看 UI-Default.shader。官网下载 Built-in-Shaders：https://unity.cn/releases/lts
            newEnt.customMat.SetInt("_Stencil", stencilID);
            newEnt.customMat.SetInt("_StencilOp", (int)operation);
            newEnt.customMat.SetInt("_StencilComp", (int)compareFunction);
            newEnt.customMat.SetInt("_StencilReadMask", readMask);
            newEnt.customMat.SetInt("_StencilWriteMask", writeMask);
            newEnt.customMat.SetInt("_ColorMask", (int)colorWriteMask);
            newEnt.customMat.SetInt("_UseUIAlphaClip", newEnt.useAlphaClip ? 1 : 0);

            //设置材质的宏 UNITY_UI_ALPHACLIP，以控制是否启用透明度裁剪。 若启用将执行 clip (color.a - 0.001);
            if (newEnt.useAlphaClip)
                newEnt.customMat.EnableKeyword("UNITY_UI_ALPHACLIP");
            else
                newEnt.customMat.DisableKeyword("UNITY_UI_ALPHACLIP");

            //加入列表保存
            m_List.Add(newEnt);

            //返回新材质
            return newEnt.customMat;
        }

        // Remove an existing material, automatically cleaning it up if it's no longer in use.
        // 移除一个已存在的材质，如果不再使用，自然清理。
        // 注意！：自然清理指应在不使用的时候调用移除，不是说StencilMaterial会自动清理不再使用的。
        public static void Remove(Material customMat)
        {
            if (customMat == null)
                return;

            for (int i = 0; i < m_List.Count; ++i)  //遍历列表查找
            {
                MatEntry ent = m_List[i];

                if (ent.customMat != customMat)
                    continue;

                if (--ent.count == 0)       //引用计数-1， 若已无任何引用则销毁移除
                {
                    Misc.DestroyImmediate(ent.customMat);   //将引用的材质立即销毁
                    ent.baseMat = null;
                    m_List.RemoveAt(i);     //从列表中移除
                }
                return;
            }
        }

        // 清理全部
        public static void ClearAll()
        {
            for (int i = 0; i < m_List.Count; ++i)  //遍历列表依次处理
            {
                MatEntry ent = m_List[i];

                Misc.DestroyImmediate(ent.customMat); //将引用的材质立即销毁
                ent.baseMat = null;
            }
            m_List.Clear(); //清空列表
        }
    }
}
