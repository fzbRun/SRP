using UnityEngine;
using UnityEngine.Rendering;

public partial class Pipeline : RenderPipeline
{

    bool useDynamicBatching, useGPUInstancing, useLightPerObject;
    CameraRenderer renderer;

    ShadowSetting shadowSetting;
    PostFXSetting postFXSetting;
    CameraBufferSetting cameraBuffer;
    int colorLUTResolution;

    public Pipeline(bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher,
        ShadowSetting shadowSetting, bool useLightPerObject, PostFXSetting postFXSetting, 
        CameraBufferSetting cameraBuffer, int colorLUTResolution, Shader shader)
    {
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        this.shadowSetting = shadowSetting;
        this.useLightPerObject = useLightPerObject;
        this.postFXSetting = postFXSetting;
        this.cameraBuffer = cameraBuffer;
        this.colorLUTResolution = colorLUTResolution;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        GraphicsSettings.lightsUseLinearIntensity = true;

        renderer = new CameraRenderer(shader);

        InitializeForEditor();

    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach(Camera camera in cameras)
        {
            renderer.Render(context, camera, useDynamicBatching, useGPUInstancing, shadowSetting, useLightPerObject, postFXSetting, cameraBuffer, colorLUTResolution);
        }
    }

}
