using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.UI;

public class MiniRenderPipeline : RenderPipeline
{
    private readonly Lighting lighting = new Lighting();
    private readonly ShadowUtil shadowUtil = new ShadowUtil();

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

        // Clear
        CommandBuffer cmd = new CommandBuffer
        {
            name = "MiniRP Camera"
        };

        // 恢复RT，避免覆盖 shadowmap
        cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);

        cmd.ClearRenderTarget(true, true, camera.backgroundColor);

        context.ExecuteCommandBuffer(cmd);

        cmd.Release();

        // Draw Opaque
        DrawOpaque(context, camera, cullingResults);

        //Skybox
        DrawSkybox(context, camera);

        //半透明
        DrawTransparent(context, camera, cullingResults);

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
}


