using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    [AddComponentMenu("UI/Rect Mask 2D", 13)]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    // A 2D rectangular mask that allows for clipping / masking of areas outside the mask.
    // The RectMask2D behaves in a similar way to a standard Mask component. It differs though in some of the restrictions that it has.
    // A RectMask2D:
    // *Only works in the 2D plane
    // *Requires elements on the mask to be coplanar.
    // *Does not require stencil buffer / extra draw calls
    // *Requires fewer draw calls
    // *Culls elements that are outside the mask area.
    // 一个允许裁剪 Mask 之外区域的2D矩形Mask。
    // * Only works in the 2D plane. 只在2D平面中工作。
    // * Requires elements on the mask to be coplanar. 要求元素与Mask在同一平面。
    // * Does not require stencil buffer / extra draw calls. 不需要模板缓冲区 / 额外的 draw calls。
    // * Requires fewer draw calls. 需要较少的绘制调用。
    // * 删除 Mask 区域之外的元素。
    public class RectMask2D : UIBehaviour, IClipper, ICanvasRaycastFilter
    {
        [NonSerialized]
        private readonly RectangularVertexClipper m_VertexClipper = new RectangularVertexClipper(); //矩形顶点剔除器

        [NonSerialized]
        private RectTransform m_RectTransform;  // 与本 RectMask2D 关联的 RectTransform。

        [NonSerialized]
        private HashSet<MaskableGraphic> m_MaskableTargets = new HashSet<MaskableGraphic>(); //所有受本 RectMask2D 裁剪的 MaskableGraphic 的集合。

        [NonSerialized]
        private HashSet<IClippable> m_ClipTargets = new HashSet<IClippable>(); //所有受本 RectMask2D 裁剪的 IClippable 的集合 （MaskableGraphic已除外。实际可能为空， 因为 目前实现了 IClippable 接口的只有 MaskableGraphic）。

        [NonSerialized]
        private bool m_ShouldRecalculateClipRects;  //是否需要重新计算 m_Clippers（脏标记）。

        [NonSerialized]
        private List<RectMask2D> m_Clippers = new List<RectMask2D>();   //对于当前节点，自身及实际生效的所有父 RectMask2D 的列表（中间未穿插“使用独立绘制顺序”的Canvas）。

        [NonSerialized]
        private Rect m_LastClipRectCanvasSpace; //保存上次生效的、Canvas空间下的裁剪矩形
        [NonSerialized]
        private bool m_ForceClip;

        // Returns a non-destroyed instance or a null reference.
        // 返回一个未销毁的 Canvas 或 null。
        // 当前所属的 Canvas（激活状态的）。
        [NonSerialized] private Canvas m_Canvas;
        private Canvas Canvas
        {
            get
            {
                if (m_Canvas == null)
                {
                    var list = ListPool<Canvas>.Get();
                    gameObject.GetComponentsInParent(false, list);
                    if (list.Count > 0)
                        m_Canvas = list[list.Count - 1];
                    else
                        m_Canvas = null;
                    ListPool<Canvas>.Release(list);
                }

                return m_Canvas;
            }
        }

        // Get the Rect for the mask in canvas space.
        // 获取 Canvas空间下的 RectMask2D 的 Rect。
        public Rect canvasRect
        {
            get
            {
                return m_VertexClipper.GetCanvasRect(rectTransform, Canvas);
            }
        }

        // Helper function to get the RectTransform for the mask.
        // 获取 与本 RectMask2D 关联的 RectTransform。
        public RectTransform rectTransform
        {
            get { return m_RectTransform ?? (m_RectTransform = GetComponent<RectTransform>()); }
        }

        protected RectMask2D()
        {}

        // 1、调用父类 OnEnable。
        // 2、m_ShouldRecalculateClipRects 设为 true（需要重新计算 m_Clippers）。
        // 3、在 ClipperRegistry 注册。
        // 4、通知 2DMaskStateChanged。
        protected override void OnEnable()
        {
            base.OnEnable();
            m_ShouldRecalculateClipRects = true;
            ClipperRegistry.Register(this);
            MaskUtilities.Notify2DMaskStateChanged(this);
        }

        // 1、调用父类 OnDisable。
        // 2、清空 m_MaskableTargets。
        // 3、清空 m_Clippers。
        // 4、移除 ClipperRegistry 中的注册。
        // 5、通知 2DMaskStateChanged。
        protected override void OnDisable()
        {
            // we call base OnDisable first here as we need to have the IsActive return the correct value when we notify the children that the mask state has changed.
            // 我们首先在这里调用 base.OnDisable，因为我们需要 在通知子物体Mask状态改变时，让 IsActive 返回正确的值。
            // 疑问 ??? 未理解，为什么调 base.OnDisable 会影响到 IsActive。
            // 实际测试，某 UIBehaviour 的子类的 OnDisable 中，调用 base.OnDisable 前后，其 activeInHierarchy 和 activeSelf 均为 false。
            base.OnDisable();
            m_ClipTargets.Clear();
            m_MaskableTargets.Clear();
            m_Clippers.Clear();
            ClipperRegistry.Unregister(this);
            MaskUtilities.Notify2DMaskStateChanged(this);   //会调用 GetComponentsInChildren，判断自身及子物体是否激活。
        }

#if UNITY_EDITOR
        // 1、调用父类 OnValidate。
        // 2、m_ShouldRecalculateClipRects 设为 true（需要重新 m_Clippers）。
        // 4、若当前处于激活状态，通知 2DMaskStateChanged。
        protected override void OnValidate()
        {
            base.OnValidate();
            m_ShouldRecalculateClipRects = true;

            if (!IsActive())
                return;

            MaskUtilities.Notify2DMaskStateChanged(this);
        }

#endif
        // 实现 ICanvasRaycastFilter 的接口
        // 射线投射位置是否有效
        public virtual bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
        {
            if (!isActiveAndEnabled)   //若未激活或未启用，则有效（不过滤）
                return true;

            // 若激活且启用，则检查投射点是否在本 rectTransform 的矩形内。
            // 在则有效
            return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, sp, eventCamera);
        }

        private Vector3[] m_Corners = new Vector3[4];
        // rectTransform 在其 Root Canvas 上的矩形
        private Rect rootCanvasRect
        {
            get
            {
                // 获取 rectTransform 四个转角的世界坐标
                // 4 个顶点的 返回数组是顺时针的。它从左下开始，然后到左上， 然后到右上，最后到右下。
                // GetWorldCorners: https://docs.unity3d.com/cn/2020.1/ScriptReference/RectTransform.GetWorldCorners.html
                rectTransform.GetWorldCorners(m_Corners);

                if (!ReferenceEquals(Canvas, null)) //Canvas不为null
                {
                    Canvas rootCanvas = Canvas.rootCanvas;  //取根Canvas
                    for (int i = 0; i < 4; ++i)
                        m_Corners[i] = rootCanvas.transform.InverseTransformPoint(m_Corners[i]); //将 rectTransform 的四个顶点，变换到 根Canvas 的坐标系下。
                }

                // 返回 rectTransform 在其 Root Canvas 上的矩形
                return new Rect(m_Corners[0].x, m_Corners[0].y, m_Corners[2].x - m_Corners[0].x, m_Corners[2].y - m_Corners[0].y);
            }
        }

        // 执行裁剪
        // 1、Canvas 为 null 时不执行裁剪。
        // 2、计算 m_Clippers（仅需要重新计算时）。
        // 3、判断是否应该剔除。
        // 4、为所有子 IClippable 和 所有子 MaskableGraphic 执行裁剪和剔除。
        public virtual void PerformClipping()
        {
            if (ReferenceEquals(Canvas, null))
            {
                return;
            }

            //TODO See if an IsActive() test would work well here or whether it might cause unexpected side effects (re case 776771)
            //TODO 看看 IsActive() 在这里是否能正常工作，或者它是否会导致意外的副作用

            // if the parents are changed or something similar we do a recalculate here
            // 如果父物体发生了变化 或 类似的、导致裁剪矩形变化的情况，重新计算 m_Clippers。
            if (m_ShouldRecalculateClipRects)
            {
                MaskUtilities.GetRectMasksForClip(this, m_Clippers);    //计算 m_Clippers。
                m_ShouldRecalculateClipRects = false;   //置回
            }

            // get the compound rects from the clippers that are valid
            // 用有效的 clippers 获取叠加/复合的矩形。
            bool validRect = true;
            Rect clipRect = Clipping.FindCullAndClipWorldRect(m_Clippers, out validRect);

            // If the mask is in ScreenSpaceOverlay/Camera render mode, its content is only rendered when its rect overlaps that of the root canvas.
            // 如果 Canvas 的渲染模式为 ScreenSpaceOverlay 或 ScreenSpaceCamera，则它的内容只有在它的rect与根Canvas重叠时才会被渲染。
            // 即： ScreenSpaceOverlay 或 ScreenSpaceCamera 模式下，超过根Canvas的部分会直接被剔除。
            RenderMode renderMode = Canvas.rootCanvas.renderMode;
            bool maskIsCulled = (renderMode == RenderMode.ScreenSpaceCamera || renderMode == RenderMode.ScreenSpaceOverlay) && !clipRect.Overlaps(rootCanvasRect, true);

            //被剔除
            if (maskIsCulled)
            {
                // Children are only displayed when inside the mask.
                // If the mask is culled, then the children inside the mask are also culled.
                // In that situation, we pass an invalid rect to allow callees to avoid some processing.
                // 只有在 Mask 内部才显示子元素。
                // 如果 Mask 被剔除，那么 Mask 内的子元素也会被筛选。
                // 在这种情况下，可以传递一个无效的 rect 来让被调用方避开一些处理。
                clipRect = Rect.zero;
                validRect = false;
            }

            //裁剪矩形变化了：
            if (clipRect != m_LastClipRectCanvasSpace)
            {
                // 为所有子 IClippable 执行裁剪（启用裁剪并设置裁剪矩形/关闭裁剪）。
                foreach (IClippable clipTarget in m_ClipTargets)
                {
                    clipTarget.SetClipRect(clipRect, validRect);
                }

                // 为所有子 MaskableGraphic 执行裁剪（启用裁剪并设置裁剪矩形/关闭裁剪）。
                // 为所有子 MaskableGraphic 执行剔除（设置是否剔除）。
                foreach (MaskableGraphic maskableTarget in m_MaskableTargets)
                {
                    maskableTarget.SetClipRect(clipRect, validRect);
                    maskableTarget.Cull(clipRect, validRect);
                }
            }
            //强制裁剪：
            else if (m_ForceClip)
            {
                // 为所有子 IClippable 执行裁剪（启用裁剪并设置裁剪矩形/关闭裁剪）。
                foreach (IClippable clipTarget in m_ClipTargets)
                {
                    clipTarget.SetClipRect(clipRect, validRect);
                }
                // 为所有子 MaskableGraphic 执行裁剪（启用裁剪并设置裁剪矩形/关闭裁剪）。
                // 为所有子 MaskableGraphic 执行剔除（设置是否剔除）（仅 canvasRenderer.hasMoved 时）。
                foreach (MaskableGraphic maskableTarget in m_MaskableTargets)
                {
                    maskableTarget.SetClipRect(clipRect, validRect);

                    if (maskableTarget.canvasRenderer.hasMoved)     //如果发生的任何更改会使生成的几何形状的位置无效，则为 true。
                        maskableTarget.Cull(clipRect, validRect);
                }
            }
            //裁剪矩形未变化且未强制裁剪：
            else
            {
                // 为所有子 MaskableGraphic 执行剔除（设置是否剔除）（仅 canvasRenderer.hasMoved 时）。
                foreach (MaskableGraphic maskableTarget in m_MaskableTargets)
                {
                    if (maskableTarget.canvasRenderer.hasMoved)
                        maskableTarget.Cull(clipRect, validRect);
                }
            }

            m_LastClipRectCanvasSpace = clipRect;   //保存裁剪矩形
            m_ForceClip = false;    //强制裁剪置回
        }

        // Add a IClippable to be tracked by the mask.
        // 添加一个被 RectMask2D 追踪的 IClippable。
        public void AddClippable(IClippable clippable)
        {
            if (clippable == null)
                return;
            m_ShouldRecalculateClipRects = true;
            MaskableGraphic maskable = clippable as MaskableGraphic;

            if (maskable == null)
                m_ClipTargets.Add(clippable);   //若 IClippable 不是 MaskableGraphic，则加入 m_ClipTargets。
            else
                m_MaskableTargets.Add(maskable); //若 IClippable 是 MaskableGraphic，则加入 m_MaskableTargets。

            m_ForceClip = true;
        }

        // Remove an IClippable from being tracked by the mask.
        // 移除一个被 RectMask2D 追踪的 IClippable。
        public void RemoveClippable(IClippable clippable)
        {
            if (clippable == null)
                return;

            m_ShouldRecalculateClipRects = true;
            clippable.SetClipRect(new Rect(), false);

            MaskableGraphic maskable = clippable as MaskableGraphic;

            if (maskable == null)
                m_ClipTargets.Remove(clippable);    //若 IClippable 不是 MaskableGraphic，则从 m_ClipTargets 中移除。
            else
                m_MaskableTargets.Remove(maskable);  //若 IClippable 是 MaskableGraphic，则从 m_MaskableTargets 中移除。

            m_ForceClip = true;
        }

        //重写 UIBehaviour 的方法
        //父物体改变后（具体看UIBehaviour里的注释），
        // 1、调用父类 OnTransformParentChanged。
        // 2、m_ShouldRecalculateClipRects 设为 true（需要重新计算 m_Clippers）。
        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            m_ShouldRecalculateClipRects = true;
        }

        //重写 UIBehaviour 的方法
        //当关联的 Canvas 在 Hierarchy 上变化时（具体看UIBehaviour里的注释），
        // 1、清除对当前所属的 Canvas（激活状态的）的引用。
        // 2、调用父类 OnCanvasHierarchyChanged。
        // 3、m_ShouldRecalculateClipRects 设为 true（需要重新计算 m_Clippers）。
        protected override void OnCanvasHierarchyChanged()
        {
            m_Canvas = null;
            base.OnCanvasHierarchyChanged();
            m_ShouldRecalculateClipRects = true;
        }
    }
}
