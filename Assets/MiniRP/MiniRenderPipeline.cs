using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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

        // 1. 获取 Camera 对应的剔除参数
        if (!camera.TryGetCullingParameters(out ScriptableCullingParameters cullingParameters))
        {
            return;
        }

        // 2. 执行剔除
        CullingResults cullingResults = context.Cull(ref cullingParameters);

        // 3. 设置 Camera GPU 状态
        context.SetupCameraProperties(camera);

        // 4. Clear
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

        // 5. Submit
        context.Submit();
    }
}