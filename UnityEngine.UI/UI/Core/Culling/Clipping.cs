using System.Collections.Generic;

namespace UnityEngine.UI
{
    // Utility class to help when clipping using IClipper.
    // 使用 IClipper 进行裁剪时的工具类。
    public static class Clipping
    {
        // Find the Rect to use for clipping.
        // Given the input RectMask2ds find a rectangle that is the overlap of all the inputs.
        // 找到用于剪切的矩形。
        // 输入一个 RectMask2d 列表，找到与所有输入矩形都重叠的矩形。（所有输入矩形的总交集）
        // 参数"rectMaskParents"：RectMasks to build the overlap rect from. //
        // 参数"validRect"：Was there a valid Rect found.
        // 返回值：The final compounded overlapping rect.
        //---------------------------------------------------------------
        // 这个方法决定了，有多个 RectMask2d 嵌套时，是怎么处理的！
        //---------------------------------------------------------------
        public static Rect FindCullAndClipWorldRect(List<RectMask2D> rectMaskParents, out bool validRect)
        {
            //列表为空，返回无效和默认Rect
            if (rectMaskParents.Count == 0)
            {
                validRect = false;  
                return new Rect();
            }

            Rect current = rectMaskParents[0].canvasRect; //取第一个 RectMask2D 在 Canvas空间下的 Rect。
            float xMin = current.xMin;
            float xMax = current.xMax;
            float yMin = current.yMin;
            float yMax = current.yMax;
            for (var i = 1; i < rectMaskParents.Count; ++i)   //遍历取交集
            {
                current = rectMaskParents[i].canvasRect;     //取其他 RectMask2D 在 Canvas空间下的 Rect。

                //取交集：取所有RectMask2D 的 xMin 和 yMin 的最大值 和 xMax 和 yMax 的最小值。
                if (xMin < current.xMin)
                    xMin = current.xMin;
                if (yMin < current.yMin)
                    yMin = current.yMin;
                if (xMax > current.xMax)
                    xMax = current.xMax;
                if (yMax > current.yMax)
                    yMax = current.yMax;
            }

            validRect = xMax > xMin && yMax > yMin; //总交集是否有效
            if (validRect)
                return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);  //返回总交集
            else
                return new Rect();
        }
    }
}
