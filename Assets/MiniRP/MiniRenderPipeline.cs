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
        context.SetupCameraProperties(camera);

        CommandBuffer cmd = new CommandBuffer();

        cmd.ClearRenderTarget(
            true,
            true,
            camera.backgroundColor
        );

        context.ExecuteCommandBuffer(cmd);

        cmd.Release();

        context.Submit();
    }
}