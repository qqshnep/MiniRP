using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

public class MiniRenderPipeline : RenderPipeline
{
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

        // 执行剔除
        CullingResults cullingResults = context.Cull(ref cullingParameters);

        // 设置 Camera GPU 状态
        context.SetupCameraProperties(camera);

        // Clear
        CommandBuffer cmd = new CommandBuffer
        {
            name = "MiniRP Camera"
        };

        cmd.ClearRenderTarget(
            true,
            true,
            camera.backgroundColor
        );

        context.ExecuteCommandBuffer(cmd);

        cmd.Release();

        // Draw Opaque
        DrawVisibleGeometry(context, camera, cullingResults);



        // Submit
        context.Submit();
    }


    private void DrawVisibleGeometry(ScriptableRenderContext context, Camera camera,CullingResults cullingResults)
    {
        ShaderTagId shaderTagId = new ShaderTagId("MiniRPUnlit");

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
}


