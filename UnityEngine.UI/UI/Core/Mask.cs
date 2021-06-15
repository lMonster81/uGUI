using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace UnityEngine.UI
{
    [AddComponentMenu("UI/Mask", 13)]
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    // A component for masking children elements.
    // By using this element any children elements that have masking enabled will mask where a sibling Graphic would write 0 to the stencil buffer.
    // 用于遮罩子元素的组件。
    // 通过使用这个元素，任何启用了 Mask（MaskableGraphic 的 maskable） 的子元素都会被遮罩。与本组件同级关联的 Graphic 将在模板测试缓冲区上写入0。
    public class Mask : UIBehaviour, ICanvasRaycastFilter, IMaterialModifier
    {
        [NonSerialized]
        private RectTransform m_RectTransform;  //与本组件关联的 RectTransform。
        public RectTransform rectTransform
        {
            get { return m_RectTransform ?? (m_RectTransform = GetComponent<RectTransform>()); }
        }

        [SerializeField]
        private bool m_ShowMaskGraphic = true;

        // Show the graphic that is associated with the Mask render area.
        // 是否显示与 Mask 关联的图形的渲染区域。
        // 设置时，若关联的 Graphic 不为 null，则标记 Graphic 的材质脏标记 为脏。
        public bool showMaskGraphic
        {
            get { return m_ShowMaskGraphic; }
            set
            {
                if (m_ShowMaskGraphic == value)
                    return;

                m_ShowMaskGraphic = value;
                if (graphic != null)
                    graphic.SetMaterialDirty();
            }
        }

        [NonSerialized]
        private Graphic m_Graphic;

        // The graphic associated with the Mask.
        // 与 Mask 关联的 graphic。
        public Graphic graphic
        {
            get { return m_Graphic ?? (m_Graphic = GetComponent<Graphic>()); }
        }
          
        [NonSerialized]
        private Material m_MaskMaterial;    //Mask材质

        [NonSerialized]
        private Material m_UnmaskMaterial;

        protected Mask()
        {}

        //Mask是否启用（生效）：激活且关联的 Graphic 不为 null。
        public virtual bool MaskEnabled() { return IsActive() && graphic != null; }

        [Obsolete("Not used anymore.")]
        public virtual void OnSiblingGraphicEnabledDisabled() {}

        // 1、调用父类 OnEnable。
        // 2、若关联的 Graphic 不为null
        //    ⑴、启用与 Graphic 关联的 CanvasRenderer 组件的 hasPopInstruction。
        //    ⑵、标记 Graphic 的材质脏标记 为脏。
        // 3、通知 StencilStateChanged。（通知所有实现 IMaskable 接口的子物体重新计算遮罩。
        protected override void OnEnable()
        {
            base.OnEnable();
            if (graphic != null)
            {
                // hasPopInstruction：
                //    Enable“render stack”pop draw call。
                //    当使用 hierarchy 渲染时，canvasRenderer 可以插入一个"pop"指令。
                //    这个"pop"指令将在所有子元素被渲染后执行。
                //    CanvasRenderer 组件将使用配置的 pop 材质渲染。
                graphic.canvasRenderer.hasPopInstruction = true;    
                graphic.SetMaterialDirty();
            }

            MaskUtilities.NotifyStencilStateChanged(this);
        }

        // 1、调用父类 OnDisable。
        // 2、若关联的 Graphic 不为null
        //    ⑴、标记 Graphic 的材质脏标记 为脏。
        //    ⑵、关闭与 Graphic 关联的 CanvasRenderer 组件的 hasPopInstruction。
        //    ⑶、设置与 Graphic 关联的 CanvasRenderer 组件的 popMaterialCount 为 0。
        // 3、将 m_MaskMaterial 从 StencilMaterial 中移除，并设置 m_MaskMaterial 为 null。
        // 4、将 m_UnmaskMaterial 从 StencilMaterial 中移除，并设置 m_UnmaskMaterial 为 null。
        // 5、通知 StencilStateChanged。（通知所有实现 IMaskable 接口的子物体重新计算遮罩。
        protected override void OnDisable()
        {
            // we call base OnDisable first here as we need to have the IsActive return the correct value when we notify the children that the mask state has changed.
            // 我们首先在这里调用 base.OnDisable，因为我们需要 在通知子物体Mask状态改变时，让 IsActive 返回正确的值。
            // 疑问 ??? 未理解，为什么调 base.OnDisable 会影响到 IsActive。
            // 实际测试，某 UIBehaviour 的子类的 OnDisable 中，调用 base.OnDisable 前后，其 activeInHierarchy 和 activeSelf 均为 false。
            base.OnDisable();
            if (graphic != null)
            {
                graphic.SetMaterialDirty();
                graphic.canvasRenderer.hasPopInstruction = false;
                graphic.canvasRenderer.popMaterialCount = 0;    // popMaterialCount：CanvasRenderer组件可用的材质数量，用于内部遮罩。
            }

            StencilMaterial.Remove(m_MaskMaterial);
            m_MaskMaterial = null;
            StencilMaterial.Remove(m_UnmaskMaterial);
            m_UnmaskMaterial = null;

            MaskUtilities.NotifyStencilStateChanged(this);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (!IsActive())
                return;

            if (graphic != null)
                graphic.SetMaterialDirty();

            MaskUtilities.NotifyStencilStateChanged(this);
        }

#endif
        // 实现 ICanvasRaycastFilter 的接口
        // 射线投射位置是否有效
        public virtual bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
        {
            if (!isActiveAndEnabled)     //若未激活或未启用，则有效（不过滤）
                return true;

            // 若激活且启用，则检查投射点是否在本 rectTransform 的矩形内。 在则有效。
            return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, sp, eventCamera);
        }

        // Stencil calculation time!
        // 实际的模板测试在这里进行！
        // 实现 IMaterialModifier 的接口。
        // 1、检查 Mask 是否启用，若未启用，直接返回 baseMaterial。
        public virtual Material GetModifiedMaterial(Material baseMaterial)
        {
            if (!MaskEnabled())
                return baseMaterial;

            var rootSortCanvas = MaskUtilities.FindRootSortOverrideCanvas(transform);   // 获取最深根的 Canvas，或第一个“使用独立绘制顺序”的 Canvas。
            var stencilDepth = MaskUtilities.GetStencilDepth(transform, rootSortCanvas); // 计算模板测试深度。
            if (stencilDepth >= 8)
            {
                Debug.LogWarning("Attempting to use a stencil mask with depth > 8", gameObject);
                return baseMaterial;    //如果深度>=8，抛出警告，直接返回 baseMaterial。 //疑问??? 为什么？
            }

            int desiredStencilBit = 1 << stencilDepth;  //预期的模板测试深度Bit。

            // if we are at the first level... we want to destroy what is there
            // 如果是嵌套 Mask 的第一层（最上面的一层） 
            if (desiredStencilBit == 1)  // （ 即 stencilDepth == 0）。
            {
                //创建或获取新的模板测试材质
                var maskMaterial = StencilMaterial.Add(baseMaterial, 1, StencilOp.Replace, CompareFunction.Always, m_ShowMaskGraphic ? ColorWriteMask.All : 0);
                StencilMaterial.Remove(m_MaskMaterial); //移除旧的
                m_MaskMaterial = maskMaterial;  //更新当前引用

                //创建或获取新的模板测试材质 unmaskMaterial
                var unmaskMaterial = StencilMaterial.Add(baseMaterial, 1, StencilOp.Zero, CompareFunction.Always, 0);
                StencilMaterial.Remove(m_UnmaskMaterial); //移除旧的
                m_UnmaskMaterial = unmaskMaterial;  //更新当前引用
                graphic.canvasRenderer.popMaterialCount = 1;     // popMaterialCount：CanvasRenderer 组件可用的材质数量，用于内部遮罩。
                graphic.canvasRenderer.SetPopMaterial(m_UnmaskMaterial, 0);  //SetPopMaterial：设置 canvasRenderer 的材质，用于内部遮罩。

                return m_MaskMaterial;  //返回修改后的材质
            }

            //otherwise we need to be a bit smarter and set some read / write masks
            var maskMaterial2 = StencilMaterial.Add(baseMaterial, desiredStencilBit | (desiredStencilBit - 1), StencilOp.Replace, CompareFunction.Equal, m_ShowMaskGraphic ? ColorWriteMask.All : 0, desiredStencilBit - 1, desiredStencilBit | (desiredStencilBit - 1));
            StencilMaterial.Remove(m_MaskMaterial);
            m_MaskMaterial = maskMaterial2;

            graphic.canvasRenderer.hasPopInstruction = true;
            var unmaskMaterial2 = StencilMaterial.Add(baseMaterial, desiredStencilBit - 1, StencilOp.Replace, CompareFunction.Equal, 0, desiredStencilBit - 1, desiredStencilBit | (desiredStencilBit - 1));
            StencilMaterial.Remove(m_UnmaskMaterial);
            m_UnmaskMaterial = unmaskMaterial2;
            graphic.canvasRenderer.popMaterialCount = 1;     // popMaterialCount：CanvasRenderer组件可用的材质数量，用于内部遮罩。
            graphic.canvasRenderer.SetPopMaterial(m_UnmaskMaterial, 0);

            return m_MaskMaterial;  //返回修改后的材质
        }
    }
}
