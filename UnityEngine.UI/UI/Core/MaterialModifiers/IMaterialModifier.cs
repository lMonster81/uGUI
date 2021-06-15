namespace UnityEngine.UI
{
    // Interface which allows for the modification of the Material used to render a Graphic before they are passed to the CanvasRenderer.
    // When a Graphic sets a material is is passed (in order) to any components on the GameObject that implement IMaterialModifier.
    // This component can modify the material to be used for rendering.
    // 这个接口允许渲染一个图形的材质在传递到 CanvasRenderer 之前被修改。
    public interface IMaterialModifier
    {
        // Perform material modification in this function.
        // 在此方法中执行材质的修改。
        // "baseMaterial"：The material that is to be modified.  //将被修改的材质
        // 返回值：The modified material. //被修改后的材质
        Material GetModifiedMaterial(Material baseMaterial);
    }
}
