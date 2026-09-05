using System.Collections.Generic;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.UI;
using static Unity.VisualScripting.Member;

public class MiniRenderPipeline : RenderPipeline
{
    private readonly Lighting lighting = new Lighting();
    private readonly ShadowUtil shadowUtil = new ShadowUtil();

    //offline render
    private static readonly int CameraColorTextureId = Shader.PropertyToID("_CameraColorTexture");
    private static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");

    //blit
    private Material finalBlitMaterial = new Material(Shader.Find("MiniRP/FinalBlit"));
    private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");

    //postprocess

    private Material postProcessMaterial = new Material(Shader.Find("MiniRP/PostProcess"));
    private static readonly int SourceTextureId = Shader.PropertyToID("_SourceTexture");
    private static readonly int ExposureId = Shader.PropertyToID("_Exposure");

    private static readonly int BloomAId = Shader.PropertyToID("_BloomA");
    private static readonly int BloomBId = Shader.PropertyToID("_BloomB");
    private Material bloomMaterial = new Material(Shader.Find("MiniRP/Bloom"));
    private static readonly int SourceTextureSizeId = Shader.PropertyToID("_SourceTextureSize");
    private static readonly int BloomThresholdId = Shader.PropertyToID("_BloomThreshold");
    private static readonly int BloomTextureId = Shader.PropertyToID("_BloomTexture");
    private static readonly int BloomIntensityId = Shader.PropertyToID("_BloomIntensity");

    protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
    {
        foreach (Camera camera in cameras)
        {
            RenderCamera(context, camera);
        }
    }


    void RenderCamera(ScriptableRenderContext context, Camera camera)
    {
        //Debug.Log($"MiniRP Render Camera: {camera.name}");

        // 获取 Camera 对应的剔除参数
        if (!camera.TryGetCullingParameters(out ScriptableCullingParameters cullingParameters))
        {
            return;
        }

        //shadow
        cullingParameters.shadowDistance = Mathf.Min( 50.0f, camera.farClipPlane);

        // 执行剔除
        CullingResults cullingResults = context.Cull(ref cullingParameters);

        // 光照
        int mainLightIndex = lighting.Setup(context, cullingResults);

        // shadow pass
        shadowUtil.Render( context, cullingResults, mainLightIndex);

        // 设置 Camera GPU 状态
        context.SetupCameraProperties(camera);

        //创建 color + depth
        AllocateCameraTargets(context, camera);

        // depth prepass
        {
            SetupDepthPrepass(context);
            DrawDepthPrepass(context, camera, cullingResults);
        }

        //forward
        {
            SetupOpaqueTargets(context, camera);
            // Draw Opaque
            DrawOpaque(context, camera, cullingResults);

            //Skybox
            DrawSkybox(context, camera);

            //半透明
            DrawTransparent(context, camera, cullingResults);


            //FinalBlit(context, camera);
            PostProcess(context, camera);
        }

        CleanupBloomTargets(context);

        CleanupCameraTargets(context);
        //清理 shadow map buffer
        shadowUtil.Cleanup(context);

        // Submit
        context.Submit();
    }


    private void DrawOpaque(ScriptableRenderContext context, Camera camera,CullingResults cullingResults)
    {
        ShaderTagId shaderTagId = new ShaderTagId("MiniRPLit");

        RendererListDesc desc = new RendererListDesc(
                shaderTagId,
                cullingResults,
                camera
            );

        desc.renderQueueRange = RenderQueueRange.opaque;

        //排序，从前到后
        desc.sortingCriteria = SortingCriteria.CommonOpaque;

        RendererList rendererList = context.CreateRendererList(desc);

        CommandBuffer cmd = new CommandBuffer
            {
                name = "Draw Opaque"
            };

        cmd.DrawRendererList(rendererList);

        context.ExecuteCommandBuffer(cmd);

        cmd.Release();
    }

    private void DrawSkybox(ScriptableRenderContext context, Camera camera)
    {
        if (camera.clearFlags != CameraClearFlags.Skybox)
        {
            return;
        }

        if (RenderSettings.skybox == null)
        {
            return;
        }

        RendererList skyboxRendererList = context.CreateSkyboxRendererList(camera);

        CommandBuffer cmd = new CommandBuffer
            {
                name = "Draw Skybox"
            };

        cmd.DrawRendererList(skyboxRendererList);

        context.ExecuteCommandBuffer(cmd);

        cmd.Release();
    }

    private void DrawTransparent(ScriptableRenderContext context, Camera camera, CullingResults cullingResults)
    {
        ShaderTagId shaderTagId = new ShaderTagId("MiniRPUnlit");

        RendererListDesc desc = new RendererListDesc(
                shaderTagId,
                cullingResults,
                camera
            );

        desc.renderQueueRange = RenderQueueRange.transparent;

        //半透明排序
        desc.sortingCriteria = SortingCriteria.CommonTransparent;

        RendererList rendererList = context.CreateRendererList(desc);

        CommandBuffer cmd = new CommandBuffer
            {
                name = "Draw Transparent"
            };

        cmd.DrawRendererList(rendererList);

        context.ExecuteCommandBuffer(cmd);

        cmd.Release();
    }


    private void AllocateCameraTargets(ScriptableRenderContext context, Camera camera)
    {
        CommandBuffer cmd = new CommandBuffer
            {
                name = "Allocate Camera Targets"
            };

        cmd.GetTemporaryRT(
            CameraColorTextureId,
            camera.pixelWidth,
            camera.pixelHeight,
            0,
            FilterMode.Bilinear,
            RenderTextureFormat.DefaultHDR
        );

        cmd.GetTemporaryRT(
            CameraDepthTextureId,
            camera.pixelWidth,
            camera.pixelHeight,
            32,
            FilterMode.Point,
            RenderTextureFormat.Depth
        );

        context.ExecuteCommandBuffer(cmd);

        cmd.Release();
    }

    private void SetupDepthPrepass(ScriptableRenderContext context)
    {
        CommandBuffer cmd = new CommandBuffer
            {
                name = "Setup Depth Prepass"
            };

        cmd.SetRenderTarget(CameraDepthTextureId);

        cmd.ClearRenderTarget(
            true,       // Clear Depth
            false,      // 不需要 Clear Color
            Color.clear
        );

        context.ExecuteCommandBuffer(cmd);

        cmd.Release();
    }


    private void SetupOpaqueTargets(ScriptableRenderContext context, Camera camera)
    {
        CommandBuffer cmd = new CommandBuffer { name = "Setup Opaque Targets" };

        RenderTargetIdentifier colorRT = new RenderTargetIdentifier(CameraColorTextureId);
        RenderTargetIdentifier depthRT = new RenderTargetIdentifier(CameraDepthTextureId);
        cmd.SetRenderTarget(colorRT, depthRT);
        cmd.ClearRenderTarget(false, true, Color.black);// 不清 Depth！

        context.ExecuteCommandBuffer(cmd);
        cmd.Release();
    }

    private void FinalBlit(ScriptableRenderContext context, Camera camera)
    {
        if(finalBlitMaterial == null)
        {
            finalBlitMaterial = new Material(Shader.Find("MiniRP/FinalBlit"));
        }

        CommandBuffer cmd = new CommandBuffer
            {
                name = "MiniRP Final Blit"
            };

        cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);

        {
            bool flipY = SystemInfo.graphicsUVStartsAtTop && (camera.targetTexture == null);

            Vector4 scaleBias = new Vector4(1, 1, 0, 0);
            if (flipY)
            {
                scaleBias = new Vector4(1, -1, 0, 1);
            }

            cmd.SetGlobalVector(BlitScaleBiasId, scaleBias);
        }

        cmd.DrawProcedural(
            Matrix4x4.identity,
            finalBlitMaterial,
            0,
            MeshTopology.Triangles,
            3,
            1
        );

        context.ExecuteCommandBuffer(cmd);

        cmd.Release();
    }

    private void CleanupCameraTargets(ScriptableRenderContext context)
    {
        CommandBuffer cmd = new CommandBuffer
            {
                name = "Release Camera Targets"
            };

        cmd.ReleaseTemporaryRT(CameraColorTextureId);

        cmd.ReleaseTemporaryRT(CameraDepthTextureId);

        context.ExecuteCommandBuffer(cmd);

        cmd.Release();
    }

    private void DrawDepthPrepass(ScriptableRenderContext context, Camera camera, CullingResults cullingResults)
    {
        ShaderTagId shaderTagId = new ShaderTagId("DepthOnly");

        RendererListDesc desc = new RendererListDesc(
                shaderTagId,
                cullingResults,
                camera
            );

        desc.renderQueueRange = RenderQueueRange.opaque;

        desc.sortingCriteria = SortingCriteria.CommonOpaque;

        RendererList rendererList = context.CreateRendererList(desc);

        CommandBuffer cmd = new CommandBuffer
            {
                name = "Depth Prepass"
            };

        cmd.DrawRendererList(rendererList);

        context.ExecuteCommandBuffer(cmd);

        cmd.Release();
    }


    private void PostProcess(ScriptableRenderContext context,Camera camera)
    {
        //Bloom
        {
            if (bloomMaterial == null)
            {
                bloomMaterial = new Material(Shader.Find("MiniRP/Bloom"));
            }

            AllocateBloomTargets(context, camera);
            BloomPrefilter(context, camera);
            BloomHorizontal(context, camera);
            BloomVertical(context, camera);
        }


        if (postProcessMaterial == null)
        {
            postProcessMaterial = new Material(Shader.Find("MiniRP/PostProcess"));
        }

        CommandBuffer cmd = new CommandBuffer
            {
                name = "MiniRP Post Process"
            };

        // Source
        cmd.SetGlobalTexture(SourceTextureId, CameraColorTextureId);
        cmd.SetGlobalTexture(BloomTextureId, BloomAId);
        cmd.SetGlobalFloat(BloomIntensityId, 5.0f);
        

        // Exposure
        cmd.SetGlobalFloat(ExposureId, -2.0f);

        // Final RT = BackBuffer
        cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);

        bool needYFlip = SystemInfo.graphicsUVStartsAtTop && camera.targetTexture == null;

        Vector4 scaleBias = needYFlip ? new Vector4(1, -1, 0, 1) : new Vector4(1, 1, 0, 0);

        cmd.SetGlobalVector(BlitScaleBiasId, scaleBias);

        cmd.DrawProcedural(
            Matrix4x4.identity,
            postProcessMaterial,
            0,
            MeshTopology.Triangles,
            3,
            1
        );

        context.ExecuteCommandBuffer(cmd);

        cmd.Release();
    }


    private void AllocateBloomTargets(ScriptableRenderContext context, Camera camera)
    {
        int width = Mathf.Max(1,camera.pixelWidth / 2);

        int height = Mathf.Max(1,camera.pixelHeight / 2);

        CommandBuffer cmd = new CommandBuffer
            {
                name = "Allocate Bloom Targets"
            };

        cmd.GetTemporaryRT(
            BloomAId,
            width,
            height,
            0,
            FilterMode.Bilinear,
            RenderTextureFormat.DefaultHDR
        );

        cmd.GetTemporaryRT(
            BloomBId,
            width,
            height,
            0,
            FilterMode.Bilinear,
            RenderTextureFormat.DefaultHDR
        );

        context.ExecuteCommandBuffer(cmd);

        cmd.Release();
    }

    private void BloomPrefilter( ScriptableRenderContext context, Camera camera)
    {
        CommandBuffer cmd = new CommandBuffer
            {
                name = "Bloom Prefilter"
            };

        cmd.SetGlobalTexture(SourceTextureId,CameraColorTextureId);

        cmd.SetGlobalVector(SourceTextureSizeId,new Vector4(
                1.0f / camera.pixelWidth,
                1.0f / camera.pixelHeight,
                camera.pixelWidth,
                camera.pixelHeight
            )
        );

        cmd.SetGlobalFloat(BloomThresholdId,1.0f);

        cmd.SetRenderTarget(BloomAId);

        cmd.DrawProcedural(
            Matrix4x4.identity,
            bloomMaterial,
            0,      // Pass 0
            MeshTopology.Triangles,
            3,
            1
        );

        context.ExecuteCommandBuffer(cmd);

        cmd.Release();
    }

    private void BloomHorizontal( ScriptableRenderContext context, Camera camera)
    {
        int width = Mathf.Max(1, camera.pixelWidth / 2);

        int height = Mathf.Max(1, camera.pixelHeight / 2);

        CommandBuffer cmd = new CommandBuffer
            {
                name = "Bloom Horizontal"
            };

        cmd.SetGlobalTexture( SourceTextureId,BloomAId);

        cmd.SetGlobalVector(SourceTextureSizeId,new Vector4(
                1.0f / width,
                1.0f / height,
                width,
                height
            )
        );

        cmd.SetRenderTarget(BloomBId);

        cmd.DrawProcedural(
            Matrix4x4.identity,
            bloomMaterial,
            1,      // Pass 1
            MeshTopology.Triangles,
            3,
            1
        );

        context.ExecuteCommandBuffer(cmd);

        cmd.Release();
    }

    private void BloomVertical( ScriptableRenderContext context, Camera camera)
    {
        int width = Mathf.Max(1, camera.pixelWidth / 2);

        int height = Mathf.Max(1, camera.pixelHeight / 2);

        CommandBuffer cmd = new CommandBuffer
            {
                name = "Bloom Vertical"
            };

        cmd.SetGlobalTexture( SourceTextureId,BloomBId);

        cmd.SetGlobalVector(SourceTextureSizeId,new Vector4(
                1.0f / width,
                1.0f / height,
                width,
                height
            )
        );

        cmd.SetRenderTarget(BloomAId);

        cmd.DrawProcedural(
            Matrix4x4.identity,
            bloomMaterial,
            2,      // Pass 2
            MeshTopology.Triangles,
            3,
            1
        );

        context.ExecuteCommandBuffer(cmd);

        cmd.Release();
    }

    private void CleanupBloomTargets(ScriptableRenderContext context)
    {
        CommandBuffer cmd = new CommandBuffer
            {
                name = "Release Bloom Targets"
            };

        cmd.ReleaseTemporaryRT(BloomAId);

        cmd.ReleaseTemporaryRT(BloomBId);

        context.ExecuteCommandBuffer(cmd);

        cmd.Release();
    }
}


