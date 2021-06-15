using System;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace UnityEngine.UI
{
    // A Graphic that is capable of being masked out.
    // 可被遮罩图形类
    public abstract class MaskableGraphic : Graphic, IClippable, IMaskable, IMaterialModifier
    {
        [NonSerialized]
        protected bool m_ShouldRecalculateStencil = true;   //是否重新计算模板测试深度（脏标记）

        [NonSerialized]
        protected Material m_MaskMaterial;  //Mask 材质

        [NonSerialized]
        private RectMask2D m_ParentMask;    //父 RectMask2D， 由 RectMask2D 在 RecalculateClipping 时为其所有“实现了接口 IClippable”的子物体设置 

        // m_Maskable is whether this graphic is allowed to be masked or not. It has the matching public property maskable.
        // The default for m_Maskable is true, so graphics under a mask are masked out of the box.
        // The maskable property can be turned off from script by the user if masking is not desired.
        // m_Maskable 表示这个图形是否允许被 Mask。有与之对应的 public属性 maskable。
        // m_Maskable 的默认值是true，所以 Mask 下的图形是默认生效的。
        // 如果不想被 Mask，可以用脚本关闭 maskable 属性。
        [NonSerialized]
        private bool m_Maskable = true;     //遮罩启用开关

        // m_IncludeForMasking 已废弃。
        [NonSerialized]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [Obsolete("Not used anymore.", true)]
        protected bool m_IncludeForMasking = false;

        [Serializable]
        public class CullStateChangedEvent : UnityEvent<bool> {}
        
        [SerializeField]
        private CullStateChangedEvent m_OnCullStateChanged = new CullStateChangedEvent();

        // Callback issued when culling changes.
        // Called when the culling state of this MaskableGraphic either becomes culled or visible. You can use this to control other elements of your UI as culling happens.
        // 剔除改变时的回调。（供外部设置）
        // 当 MaskableGraphic 的剔除状态变成 被剔除（culled）或 可见时（visible）调用。
        // 当 剔除发生时，你可以使用它来控制UI的其他元素。
        public CullStateChangedEvent onCullStateChanged
        {
            get { return m_OnCullStateChanged; }
            set { m_OnCullStateChanged = value; }
        }

        // Does this graphic allow masking.
        // 这个图形是否允许被 Mask
        public bool maskable
        {
            get { return m_Maskable; }
            set
            {
                if (value == m_Maskable)
                    return;
                m_Maskable = value;
                m_ShouldRecalculateStencil = true;
                SetMaterialDirty();
            }
        }

        [NonSerialized]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [Obsolete("Not used anymore", true)]
        protected bool m_ShouldRecalculate = true;

        [NonSerialized]
        protected int m_StencilValue;   //模板测试深度

        // See IMaterialModifier.GetModifiedMaterial
        // 实现 IMaterialModifier 的接口
        // 1、若需要重新计算模板测试深度，则用根 Canvas 和 本transfrom 重新计算模板测试深度（若不启用遮罩开关，则为0）。
        // 2、若模板测试深度>0，且Mask存在且激活，则更新当前模板测试材质。
        public virtual Material GetModifiedMaterial(Material baseMaterial)
        {
            var toUse = baseMaterial;   //默认使用基础材质

            if (m_ShouldRecalculateStencil)
            {
                var rootCanvas = MaskUtilities.FindRootSortOverrideCanvas(transform);  //获取根Canvas
                m_StencilValue = maskable ? MaskUtilities.GetStencilDepth(transform, rootCanvas) : 0;  //计算模板测试深度
                m_ShouldRecalculateStencil = false;     //脏标记置回false。
            }

            // if we have a enabled Mask component then it will
            // generate the mask material. This is an optimisation
            // it adds some coupling between components though :(
            // 如果我们有一个启用的 Mask 组件，那么它将生成 Mask 材质。
            // 这是一个优化，但它增加了一些组件间的耦合:(
            // 这里的优化指：引入 StencilMaterial 类，对模板测试材质进行缓存管理。
            Mask maskComponent = GetComponent<Mask>();
            if (m_StencilValue > 0 && (maskComponent == null || !maskComponent.IsActive()))
            {
                //创建或获取新的模板测试材质
                var maskMat = StencilMaterial.Add(toUse, (1 << m_StencilValue) - 1, StencilOp.Keep, CompareFunction.Equal, ColorWriteMask.All, (1 << m_StencilValue) - 1, 0);   
                StencilMaterial.Remove(m_MaskMaterial); //移除原模板测试材质
                m_MaskMaterial = maskMat;   //更新当前
                toUse = m_MaskMaterial;     //新材质作为返回值
            }

            //返回修改后的材质
            return toUse;
        }

        // See IClippable.Cull
        // 实现 IClippable 的接口
        // 执行剔除
        // 1、计算是否需要剔除。
        // 2、更新剔除状态。
        public virtual void Cull(Rect clipRect, bool validRect)
        {
            // 可完全剔除?：rect无效 或 clipRect 与 rootCanvasRect 不重叠（包括正反）
            var cull = !validRect || !clipRect.Overlaps(rootCanvasRect, true);
            // 更新剔除状态
            UpdateCull(cull);
        }

        // 更新剔除状态（是否剔除
        // 1、设置 canvasRenderer.cull 是否剔除。
        // 2、触发回调 m_OnCullStateChanged。
        // 3、调用父类定义的生命周期方法 OnCullingChanged，处理父类中的事务。
        private void UpdateCull(bool cull)
        {
            if (canvasRenderer.cull != cull)
            {
                canvasRenderer.cull = cull;
                UISystemProfilerApi.AddMarker("MaskableGraphic.cullingChanged", this);
                m_OnCullStateChanged.Invoke(cull);
                OnCullingChanged();
            }
        }

        // See IClippable.SetClipRect
        // 实现 IClippable 的接口
        // 设置裁剪矩形。
        // 若矩形有效，开启 canvasRenderer 的裁剪并设置裁剪矩形。 否则关闭 canvasRenderer 的裁剪。
        public virtual void SetClipRect(Rect clipRect, bool validRect)
        {
            if (validRect)
                canvasRenderer.EnableRectClipping(clipRect);    //（重要！裁剪生效的本质原因）
            else
                canvasRenderer.DisableRectClipping();
        }

        // 1、执行父类 OnEnable
        // ---（由 RectMask2D 影响）---
        // 2、m_ShouldRecalculateStencil 设为true（需要重新计算模板测试深度）。
        // 3、更新 m_ParentMask。
        // 4、标记 材质脏标记 为脏。
        //---------------------------------
        // ---（由 Mask 影响）---
        // 5、如果存在 Mask 组件，触发通知 StencilStateChanged。
        protected override void OnEnable()
        {
            base.OnEnable();
            m_ShouldRecalculateStencil = true;
            UpdateClipParent();
            SetMaterialDirty();

            //疑问??? 这个通知感觉应该写在 Graphic 类中。
            //因为 Mask 关联的是 Graphic，而不是 MaskableGraphic。
            //如果有一个类继承自 Graphic 而不是 MaskableGraphic，那么它还要调用这句。
            if (GetComponent<Mask>() != null)
            {
                MaskUtilities.NotifyStencilStateChanged(this);
            }
        }

        // 1、执行父类 OnDisable
        // ---（由 RectMask2D 影响）---
        // 2、m_ShouldRecalculateStencil 设为true（需要重新计算模板测试深度）。
        // 3、标记 材质脏标记 为脏。
        // 4、更新 m_ParentMask。
        //---------------------------------
        // ---（由 Mask 影响）---
        // 5、从 StencilMaterial 中移除当前使用的模板测试材质。
        // 6、m_MaskMaterial 设为 null。
        // 7、如果存在 Mask 组件，触发通知 StencilStateChanged。
        protected override void OnDisable()
        {
            base.OnDisable();
            m_ShouldRecalculateStencil = true;
            SetMaterialDirty();
            UpdateClipParent();
            StencilMaterial.Remove(m_MaskMaterial);
            m_MaskMaterial = null;

            //疑问??? 这个通知感觉应该写在 Graphic 类中。
            //因为 Mask 关联的是 Graphic，而不是 MaskableGraphic。
            //如果有一个类继承自 Graphic 而不是 MaskableGraphic，那么它还要调用这句。
            if (GetComponent<Mask>() != null)
            {
                MaskUtilities.NotifyStencilStateChanged(this);
            }
        }

#if UNITY_EDITOR
        // 重写 Graphic 方法。
        // 编辑器下，脚本被加载、或 Inspector 中的任何值被修改时，方法被调用
        // 1、调用父类 OnValidate
        // 2、m_ShouldRecalculateStencil 设为true（需要重新计算模板测试深度）。
        // 3、更新 m_ParentMask。
        // 4、标记 材质脏标记 为脏。
        protected override void OnValidate()
        {
            base.OnValidate();
            m_ShouldRecalculateStencil = true;
            UpdateClipParent();
            SetMaterialDirty();
        }

#endif
        // 重写 Graphic 方法。
        // 父物体改变后（具体看UIBehaviour里的注释），
        // 1、调用父类 OnTransformParentChanged。
        // 2、若物体激活且组件启用：
        //   ⑴、m_ShouldRecalculateStencil 设为true（需要重新计算模板测试深度）。
        //   ⑵、更新 m_ParentMask。
        //   ⑶、标记 材质脏标记 为脏。
        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();

            if (!isActiveAndEnabled)
                return;

            m_ShouldRecalculateStencil = true;
            UpdateClipParent();
            SetMaterialDirty();
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [Obsolete("Not used anymore.", true)]
        public virtual void ParentMaskStateChanged() {}

        // 重写 Graphic 方法。
        // 当关联的 Canvas 在 Hierarchy 上变化时（具体看UIBehaviour里的注释）
        // 1、调用父类 OnTransformParentChanged。
        // 2、若物体激活且组件启用：
        //   ⑴、m_ShouldRecalculateStencil 设为true（需要重新计算模板测试深度）。
        //   ⑵、更新 m_ParentMask。
        //   ⑶、标记 材质脏标记 为脏。
        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();

            if (!isActiveAndEnabled)
                return;

            m_ShouldRecalculateStencil = true;
            UpdateClipParent();
            SetMaterialDirty();
        }

        readonly Vector3[] m_Corners = new Vector3[4];
        // rectTransform 在其 Root Canvas 上的矩形
        private Rect rootCanvasRect
        {
            get
            {
                // 获取 rectTransform 四个转角的世界坐标
                // 4 个顶点的 返回数组是顺时针的。它从左下开始，然后到左上， 然后到右上，最后到右下。
                // GetWorldCorners: https://docs.unity3d.com/cn/2020.1/ScriptReference/RectTransform.GetWorldCorners.html
                rectTransform.GetWorldCorners(m_Corners);   
                
                if (canvas)  // Graphic 当前所属的 Canvas
                {
                    Matrix4x4 mat = canvas.rootCanvas.transform.worldToLocalMatrix;     //通过当前所属Canvas找到根Canvas，再取到世界坐标到根Canvas本地坐标的变换矩阵。
                    for (int i = 0; i < 4; ++i)
                        m_Corners[i] = mat.MultiplyPoint(m_Corners[i]);     //将 rectTransform 的四个顶点，变换到 根Canvas 的坐标系下。
                }

                // bounding box is now based on the min and max of all corners (case 1013182)
                // 边框盒子 现在是基于 四个转角的最大和最小的XY坐标。

                // 用转角 1、2、3 与 转角 0 比较，即可求出四个转角的最小XY 和 最大 XY。
                Vector2 min = m_Corners[0];
                Vector2 max = m_Corners[0];
                for (int i = 1; i < 4; i++)
                {
                    min.x = Mathf.Min(m_Corners[i].x, min.x);
                    min.y = Mathf.Min(m_Corners[i].y, min.y);
                    max.x = Mathf.Max(m_Corners[i].x, max.x);
                    max.y = Mathf.Max(m_Corners[i].y, max.y);
                }

                //返回 Rect
                return new Rect(min, max - min);
            }
        }

        // 更新 m_ParentMask
        private void UpdateClipParent()
        {
            // 仅当需要被 Mask 且 Active 时，取当前可令自身 IClippable 生效的父 RectMask2D 为 m_ParentMask。 否则为null。
            var newParent = (maskable && IsActive()) ? MaskUtilities.GetRectMaskForClippable(this) : null;

            // if the new parent is different OR is now inactive
            // 若新的父节点存在 且（与之前不同 或 当前未激活）
            if (m_ParentMask != null && (newParent != m_ParentMask || !newParent.IsActive())) //这里不会未激活吧！。MaskUtilities.GetRectMaskForClippable 中取到的都是激活的。
            {
                m_ParentMask.RemoveClippable(this); //将当前物体从原 RectMask2D 的引用中移除
                UpdateCull(false);  // 更新剔除状态为不要剔除
            }

            // don't re-add it if the newparent is inactive
            // 仅 newparent 是激活时才建立父子引用关系
            if (newParent != null && newParent.IsActive())
                newParent.AddClippable(this);   // 将当前物体加入新 RectMask2D 的引用中

            m_ParentMask = newParent;   //更新 ParentMask 为新的。
        }

        // See IClippable.RecalculateClipping
        // 实现 IClippable 的接口
        // 1、更新 m_ParentMask
        public virtual void RecalculateClipping()
        {
            UpdateClipParent();
        }

        // See IMaskable.RecalculateMasking
        // 实现 IMaskable 的接口
        // 1、移除材质引用
        // 2、m_MaskMaterial 设为 null。 
        // 3、m_ShouldRecalculateStencil 设为true（需要重新计算模板测试深度）。
        // 4、标记 材质脏标记 为脏。
        public virtual void RecalculateMasking()
        {
            // Remove the material reference as either the graphic of the mask has been enable/ disabled.
            // This will cause the material to be repopulated from the original if need be. (case 994413)
            // 移除材质引用，
            // 父 Mask 组件启用/禁用时；或与父 Mask 组件关联的 MaskableGraphic 被启用/禁用时（实际上，Mask 组件关联的是 Graphic）。 
            // 这将导致材质从原材质重新填充，如果需要的话。(例 994413) （即标记 材质脏标记 为脏，然后GetModifiedMaterial被重新调用）
            StencilMaterial.Remove(m_MaskMaterial);
            m_MaskMaterial = null;
            m_ShouldRecalculateStencil = true;
            SetMaterialDirty();
        }
    }
}
