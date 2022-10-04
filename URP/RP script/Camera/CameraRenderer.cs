using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{

    public const float renderScaleMin = 0.1f, renderScaleMax = 2.0f;

    static CameraSetting defaultCameraSetting = new CameraSetting();
    static bool copyTextureSupported = SystemInfo.copyTextureSupport > CopyTextureSupport.None;

    ScriptableRenderContext context;    //相当于Opengl中的帧缓冲
    CullingResults cull;
    Camera camera;
    CommandBuffer buffer = new CommandBuffer()  //命令缓存，存放不同种类的渲染命令，当帧缓冲submit时，利用buffer中的索引，调用相应命令的数据。
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

    //static int frameBufferID = Shader.PropertyToID("_CameraFrameBuffer");   //颜色和深度
    static int colorAttachmentID = Shader.PropertyToID("_CameraColorAttachment");   //颜色缓冲区
    static int depthAttachmentID = Shader.PropertyToID("_CameraDepthAttachment");   //深度缓冲区
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
        buffer.SetViewport(camera.pixelRect);   //SetRenderTargetz之后会将视口变换到屏幕大小，所以需要修改
        buffer.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
        buffer.SetGlobalFloat(srcBlendID, 1f);
        buffer.SetGlobalFloat(dstBlendID, 0f);
    }

    //渲染前的准备
    public void setUp()
    {

        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags; //flags顺序为天空盒，颜色，深度和nothing，前面的包含后面的，比如为天空盒时，也会清除颜色和深度

        //是否使用中间纹理
        useIntermediateBuffer = useColorTexture || useDepthTexture || postFXStack.isActive || useRenderScale;
        if (useIntermediateBuffer)
        {

            if(flags > CameraClearFlags.Color)
            {
                camera.clearFlags = CameraClearFlags.Color; //清除深度
            }

            buffer.GetTemporaryRT(colorAttachmentID, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear,
                allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            buffer.GetTemporaryRT(depthAttachmentID, bufferSize.x, bufferSize.y, 32, FilterMode.Point, RenderTextureFormat.Depth);
            buffer.SetRenderTarget(colorAttachmentID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                                    depthAttachmentID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        }

        //必须将这里的clear数组的w分量透明度设为0，否则天空盒将不会被后面的画面覆盖
        buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth, flags == CameraClearFlags.Color, new Color(0, 0, 0, 0));
        buffer.BeginSample(SampleName);
        buffer.SetGlobalTexture(colorTextureID, missingTexture);
        buffer.SetGlobalTexture(depthTextureID, missingTexture);
        ExecuteBuffer();

    }

    void ExecuteBuffer()
    {

        context.ExecuteCommandBuffer(buffer);   //将buffer中的命令拿到context中
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
            //现在将渲染目标设为了临时纹理，这是错误的，透明的物体不会被渲染到相机，我们需要将之改为颜色和深度纹理
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
        //获得相机剔除参数
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

        if (!Cull(shadowSetting.maxDistance))   //视锥体剔除
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
        changeCenters(camera, ref shadowSetting);   //得到联级
        light.setUp(context, cull, shadowSetting, useLightPerObject,
            cameraSetting.maskLights ? cameraSetting.renderingLayerMask : -1);   //将光照数据传入GPU，并且渲染阴影
        if (cameraSetting.overridePostFX)
        {
            postFXStack.setUp(context, camera, cameraSetting.postFXSetting, allowHDR, colorLUTResolution, 
                cameraSetting.finalBlendMode, bufferSize, cameraBuffer.bicubicRescaling, cameraBuffer.fxaa, cameraSetting.keepAlpha);  //设置后期处理
        }
        else
        {
            postFXStack.setUp(context, camera, postFXSetting, allowHDR, colorLUTResolution, 
                cameraSetting.finalBlendMode, bufferSize, cameraBuffer.bicubicRescaling, cameraBuffer.fxaa, cameraSetting.keepAlpha);  //设置后期处理
        }
        buffer.EndSample(SampleName);
        setUp();    //判断后期处理，清楚缓冲区

        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing, shadowSetting, useLightPerObject, cameraSetting.renderingLayerMask);
        DrawUnSupportedShaders();

        DrawGizmosBeforeFX();   //Gizmos在屏幕中渲染，而此时屏幕上没有画面，所以深度最大，Gizmos不会被遮挡，而画面赋过来也不会阻挡Gizoms（不知道为啥），所以在前面。
        if (postFXStack.isActive)
        {
            postFXStack.Render(colorAttachmentID);
        }
        else if(useIntermediateBuffer)
        {
            //当设置渲染缩放时，不能修改相机缓冲区的大小，所以两者大小不同，不能使用buffer.CopyTexture函数
            if (camera.targetTexture && bufferSize == camera.targetTexture.texelSize)
            {
                buffer.CopyTexture(colorAttachmentID, camera.targetTexture);
            }
            else
            {
                //Draw(colorAttachmentID, BuiltinRenderTextureType.CameraTarget, false);
                DrawFinal(cameraSetting.finalBlendMode);    //多相机要视口变换，虽然我实验了不进行视口变换好像也没事
            }
            ExecuteBuffer();
        }
        DrawGizmosAfterFX();

        cleanUp();

        submit();

    }

}