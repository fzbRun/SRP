using UnityEngine.Rendering;
using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public partial class PipelineAsset : RenderPipelineAsset
{

    [SerializeField]
    bool useDynamicBatching = true, useGPUInstancing = true, useSRPBatching = true, useLightPerObject = true;

    [SerializeField]
    private ShadowSetting shadowSetting = default;

    [SerializeField]
    PostFXSetting postFXSetting = default;

    [SerializeField]
    CameraBufferSetting cameraBuffer = new CameraBufferSetting
    {
        allowHDR = true,
        renderScale = 1.0f,
        fxaa = new CameraBufferSetting.FXAA
        {
            fixedThreshold = 0.0833f,
            relativeThreshold = 0.166f,
            subpixelBlending = 0.75f
        }
    };

    [SerializeField]
    Shader cameraRendererShader = default;

    public enum ColorLUTResolution { _16 = 16, _32 = 32, _64 = 64 }

    [SerializeField]
    ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;

    protected override RenderPipeline CreatePipeline()
    {
        return new Pipeline(useDynamicBatching, useGPUInstancing, useSRPBatching, 
            shadowSetting, useLightPerObject, postFXSetting, cameraBuffer, (int)colorLUTResolution, cameraRendererShader);
    }

}
