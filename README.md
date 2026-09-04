# MiniRP
## Step03:第一次真正画出 Mesh

### 1. MiniRenderPipeline
```
private void DrawVisibleGeometry(ScriptableRenderContext context, Camera camera,CullingResults cullingResults)
{
    ShaderTagId shaderTagId = new ShaderTagId("MiniRPUnlit");

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
            name = "Draw Opaque"
        };

    cmd.DrawRendererList(rendererList);

    context.ExecuteCommandBuffer(cmd);

    cmd.Release();
}
```

### 2. 创建 MiniUnlit 材质和Shader
将 Plane 和 Cube 的材质，修改为 MiniUnlit

### 3. FrameDebugger 可以看到 Plane + Cube
![image](https://github.com/qqshnep/MiniRP/blob/step03/readme_img/step03/01.png)
   
