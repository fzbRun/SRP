using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{

    public const float renderScaleMin = 0.1f, renderScaleMax = 2.0f;

    static CameraSetting defaultCameraSetting = new CameraSetting();
    static bool copyTextureSupported = SystemInfo.copyTextureSupport > CopyTextureSupport.None;

    ScriptableRenderContext context;    //�൱��Opengl�е�֡����
    CullingResults cull;
    Camera camera;
    CommandBuffer buffer = new CommandBuffer()  //����棬��Ų�ͬ�������Ⱦ�����֡����submitʱ������buffer�е�������������Ӧ��������ݡ�
    {
        name = "Render Camera"
    };
    Lighting light = new Lighting();
    PostFXSetting postFXSetting = new PostFXSetting();
    PostFXStack postFXStack = new PostFXStack();
    bool allowHDR, useRenderScale;
    int colorLUTResolution;
    Material material;


    static ShaderTagId
        unlitShaderTagID = new ShaderTagId("SRPDefaultUnlit"),
        litShaderTagID = new ShaderTagId("CustomLit");

    //static int frameBufferID = Shader.PropertyToID("_CameraFrameBuffer");   //��ɫ�����
    static int colorAttachmentID = Shader.PropertyToID("_CameraColorAttachment");   //��ɫ������
    static int depthAttachmentID = Shader.PropertyToID("_CameraDepthAttachment");   //��Ȼ�����
    static int colorTextureID = Shader.PropertyToID("_CameraColorTexture");
    static int depthTextureID = Shader.PropertyToID("_CameraDepthTexture");
    bool useColorTexture, useDepthTexture, useIntermediateBuffer;
    static int sourceTextureID = Shader.PropertyToID("_SourceTexture");
    static int srcBlendID = Shader.PropertyToID("_CameraSrcBlend");
	static int dstBlendID = Shader.PropertyToID("_CameraDstBlend");
    static int bufferSizeID = Shader.PropertyToID("_CameraBufferSize");

    Texture2D missingTexture;

    Vector2Int bufferSize;

    public CameraRenderer(Shader shader)
    {
        material = CoreUtils.CreateEngineMaterial(shader);
        missingTexture = new Texture2D(1, 1)
        {
            hideFlags = HideFlags.HideAndDontSave,
            name = "Missing"
        };
        missingTexture.SetPixel(0, 0, Color.white * 0.5f);
        missingTexture.Apply(true, true);
    }

    public void Dispose()
    {
        CoreUtils.Destroy(material);
        CoreUtils.Destroy(missingTexture);
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, bool useDepth)
    {
        buffer.SetGlobalTexture(sourceTextureID, from);
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, material, useDepth ? 1 : 0, MeshTopology.Triangles, 3);
    }

    void DrawFinal(CameraSetting.FinalBlendMode finalBlendMode)
    {
        buffer.SetGlobalFloat(srcBlendID, (float)finalBlendMode.source);
        buffer.SetGlobalFloat(dstBlendID, (float)finalBlendMode.destination);
        buffer.SetGlobalTexture(sourceTextureID, colorAttachmentID);
        buffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget,
            finalBlendMode.destination == BlendMode.Zero ? RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load,
            RenderBufferStoreAction.Store);
        buffer.SetViewport(camera.pixelRect);   //SetRenderTargetz֮��Ὣ�ӿڱ任����Ļ��С��������Ҫ�޸�
        buffer.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
        buffer.SetGlobalFloat(srcBlendID, 1f);
        buffer.SetGlobalFloat(dstBlendID, 0f);
    }

    //��Ⱦǰ��׼��
    public void setUp()
    {

        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags; //flags˳��Ϊ��պУ���ɫ����Ⱥ�nothing��ǰ��İ�������ģ�����Ϊ��պ�ʱ��Ҳ�������ɫ�����

        //�Ƿ�ʹ���м�����
        useIntermediateBuffer = useColorTexture || useDepthTexture || postFXStack.isActive || useRenderScale;
        if (useIntermediateBuffer)
        {

            if(flags > CameraClearFlags.Color)
            {
                camera.clearFlags = CameraClearFlags.Color; //������
            }

            buffer.GetTemporaryRT(colorAttachmentID, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear,
                allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            buffer.GetTemporaryRT(depthAttachmentID, bufferSize.x, bufferSize.y, 32, FilterMode.Point, RenderTextureFormat.Depth);
            buffer.SetRenderTarget(colorAttachmentID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                                    depthAttachmentID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        }

        //���뽫�����clear�����w����͸������Ϊ0��������պн����ᱻ����Ļ��渲��
        buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth, flags == CameraClearFlags.Color, new Color(0, 0, 0, 0));
        buffer.BeginSample(SampleName);
        buffer.SetGlobalTexture(colorTextureID, missingTexture);
        buffer.SetGlobalTexture(depthTextureID, missingTexture);
        ExecuteBuffer();

    }

    void ExecuteBuffer()
    {

        context.ExecuteCommandBuffer(buffer);   //��buffer�е������õ�context��
        buffer.Clear();

    }

    void submit()
    {

        buffer.EndSample(SampleName);
        ExecuteBuffer();
        context.Submit();

    }

    void copyAttachment()
    {
        if (useColorTexture)
        {
            buffer.GetTemporaryRT(colorTextureID, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, 
                allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            if (copyTextureSupported)
            {
                buffer.CopyTexture(colorAttachmentID, colorTextureID);
            }
            else
            {
                Draw(colorAttachmentID, colorTextureID, false);
            }
        }

        if (useDepthTexture)
        {
            buffer.GetTemporaryRT(depthTextureID, bufferSize.x, bufferSize.y, 32, FilterMode.Point, RenderTextureFormat.Depth);
            if (copyTextureSupported)
            {
                buffer.CopyTexture(depthAttachmentID, depthTextureID);
            }
            else
            {
                Draw(depthAttachmentID, depthTextureID, true);
            }
        }

        if (!copyTextureSupported)
        {
            //���ڽ���ȾĿ����Ϊ����ʱ�������Ǵ���ģ�͸�������岻�ᱻ��Ⱦ�������������Ҫ��֮��Ϊ��ɫ���������
            buffer.SetRenderTarget(colorAttachmentID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                                    depthAttachmentID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        }

        ExecuteBuffer();
    }

    void cleanUp()
    {
        light.CleanUp();
        if (useIntermediateBuffer)
        {
            buffer.ReleaseTemporaryRT(colorAttachmentID);
            buffer.ReleaseTemporaryRT(depthAttachmentID);
            if (useColorTexture)
            {
                buffer.ReleaseTemporaryRT(colorTextureID);
            }
            if (useDepthTexture)
            {
                buffer.ReleaseTemporaryRT(depthTextureID);
            }
        }
    }

    bool Cull(float maxDistance)
    {
        //�������޳�����
        if(camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {

            p.shadowDistance = Mathf.Min(maxDistance, camera.farClipPlane);
            cull = context.Cull(ref p);
            return true;

        }

        return false;

    }

    void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing, ShadowSetting shadowSetting, bool useLightsPerObject, int renderinLayerMask)
    {

        PerObjectData lightPerObjectFlags = useLightsPerObject ? PerObjectData.LightData | PerObjectData.LightIndices : PerObjectData.None;

        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };
        var drawingSettings = new DrawingSettings(unlitShaderTagID, sortingSettings)
        {

            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing,
            perObjectData = PerObjectData.Lightmaps | PerObjectData.ShadowMask | PerObjectData.LightProbe | PerObjectData.OcclusionProbe | PerObjectData.LightProbeProxyVolume |
                PerObjectData.OcclusionProbeProxyVolume | PerObjectData.ReflectionProbes | lightPerObjectFlags

        };
        drawingSettings.SetShaderPassName(1, litShaderTagID);
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque, renderingLayerMask: (uint)renderinLayerMask);
        context.DrawRenderers(cull, ref drawingSettings, ref filteringSettings);

        context.DrawSkybox(camera);

        if(useColorTexture || useDepthTexture)
        {
            copyAttachment();
        }

        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cull, ref drawingSettings, ref filteringSettings);

    }

    public Vector3 calcCenters(Camera camera, ShadowSetting shadowSetting)
    {
        float num = shadowSetting.directional.cascadeCount;
        float k = 0.5f;
        float n = camera.nearClipPlane;
        //float f = camera.farClipPlane;
        float f = Mathf.Min(shadowSetting.maxDistance, camera.farClipPlane);
        Vector3 centers;
        centers.x = k * n * Mathf.Pow(f / n, 1 / num) + (1.0f - k) * (n + (f - n) * (1 / num));
        centers.y = k * n * Mathf.Pow(f / n, 2 / num) + (1.0f - k) * (n + (f - n) * (2 / num));
        centers.z = k * n * Mathf.Pow(f / n, 3 / num) + (1.0f - k) * (n + (f - n) * (3 / num));
        //Debug.Log("n: " + n + "   " + "f: " + f);
        return centers / f * 1.5f;
        //return new Vector3(0.3f, 0.4f, 0.5f);
    }

    public void changeCenters(Camera camera, ref ShadowSetting shadowSetting)
    {
        Vector3 centers = calcCenters(camera, shadowSetting);
        shadowSetting.directional.cascadeRatio1 = centers.x;
        shadowSetting.directional.cascadeRatio2 = centers.y;
        shadowSetting.directional.cascadeRatio3 = centers.z;
    }

    public void Render(ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useGPUInstancing, 
        ShadowSetting shadowSetting, bool useLightPerObject, PostFXSetting postFXSetting, 
        CameraBufferSetting cameraBuffer, int colorLUTResolution)
    {

        this.context = context;
        this.camera = camera;

        var crpCamera = camera.GetComponent<CustomRenderPipelineCamera>();
        CameraSetting cameraSetting = crpCamera ? crpCamera.Settings : defaultCameraSetting;

        if(camera.cameraType == CameraType.Reflection)
        {
            useColorTexture = cameraBuffer.copyColorReflection;
            useDepthTexture = cameraBuffer.copyDepthReflection;
        }
        else
        {
            useColorTexture = cameraBuffer.copyColor && cameraSetting.copyColor;
            useDepthTexture = cameraBuffer.copyDepth && cameraSetting.copyDepth;
        }

        float renderScale = cameraBuffer.renderScale;
        renderScale = cameraSetting.getRenderScale(renderScale);
        useRenderScale = renderScale < 0.99f || renderScale > 1.01f;

        PrepareBuffer();
        PrepareForSceneWindow();

        if (!Cull(shadowSetting.maxDistance))   //��׶���޳�
        {
            return;
        }

        this.allowHDR = cameraBuffer.allowHDR && camera.allowHDR;

        if (useRenderScale)
        {
            renderScale = Mathf.Clamp(renderScale, renderScaleMin, renderScaleMax);
            bufferSize.x = (int)(camera.pixelWidth * renderScale);
            bufferSize.y = (int)(camera.pixelHeight * renderScale);
        }
        else
        {
            bufferSize.x = camera.pixelWidth;
            bufferSize.y = camera.pixelHeight;
        }

        cameraBuffer.fxaa.enabled &= cameraSetting.allowFXAA;

        this.colorLUTResolution = colorLUTResolution;

        buffer.BeginSample(SampleName);

        buffer.SetGlobalVector(bufferSizeID, new Vector4(
            1.0f / bufferSize.x, 1.0f / bufferSize.y,
            bufferSize.x, bufferSize.y));

        ExecuteBuffer();
        changeCenters(camera, ref shadowSetting);   //�õ�����
        light.setUp(context, cull, shadowSetting, useLightPerObject,
            cameraSetting.maskLights ? cameraSetting.renderingLayerMask : -1);   //���������ݴ���GPU��������Ⱦ��Ӱ
        if (cameraSetting.overridePostFX)
        {
            postFXStack.setUp(context, camera, cameraSetting.postFXSetting, allowHDR, colorLUTResolution, 
                cameraSetting.finalBlendMode, bufferSize, cameraBuffer.bicubicRescaling, cameraBuffer.fxaa, cameraSetting.keepAlpha);  //���ú��ڴ���
        }
        else
        {
            postFXStack.setUp(context, camera, postFXSetting, allowHDR, colorLUTResolution, 
                cameraSetting.finalBlendMode, bufferSize, cameraBuffer.bicubicRescaling, cameraBuffer.fxaa, cameraSetting.keepAlpha);  //���ú��ڴ���
        }
        buffer.EndSample(SampleName);
        setUp();    //�жϺ��ڴ������������

        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing, shadowSetting, useLightPerObject, cameraSetting.renderingLayerMask);
        DrawUnSupportedShaders();

        DrawGizmosBeforeFX();   //Gizmos����Ļ����Ⱦ������ʱ��Ļ��û�л��棬����������Gizmos���ᱻ�ڵ��������渳����Ҳ�����赲Gizoms����֪��Ϊɶ����������ǰ�档
        if (postFXStack.isActive)
        {
            postFXStack.Render(colorAttachmentID);
        }
        else if(useIntermediateBuffer)
        {
            //��������Ⱦ����ʱ�������޸�����������Ĵ�С���������ߴ�С��ͬ������ʹ��buffer.CopyTexture����
            if (camera.targetTexture && bufferSize == camera.targetTexture.texelSize)
            {
                buffer.CopyTexture(colorAttachmentID, camera.targetTexture);
            }
            else
            {
                //Draw(colorAttachmentID, BuiltinRenderTextureType.CameraTarget, false);
                DrawFinal(cameraSetting.finalBlendMode);    //�����Ҫ�ӿڱ任����Ȼ��ʵ���˲������ӿڱ任����Ҳû��
            }
            ExecuteBuffer();
        }
        DrawGizmosAfterFX();

        cleanUp();

        submit();

    }

}