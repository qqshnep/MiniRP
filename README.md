# MiniRP
## Step01:接管 Unity 渲染

### 1. 创建项目
![image](https://github.com/qqshnep/MiniRP/blob/step01/readme_img/step01/01.jpg)
   
### 2. 搭建场景 Plane + Cube
![image](https://github.com/qqshnep/MiniRP/blob/step01/readme_img/step01/02.png)
   
### 3. 创建代码文件 MiniRenderPipelineAsset/MiniRenderPipeline
只使用 camera 的背景色 清除 buffer
```
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
```

![image](https://github.com/qqshnep/MiniRP/blob/step01/readme_img/step01/03.png)

### 4. 使用 MiniRenderPipelineAsset
![image](https://github.com/qqshnep/MiniRP/blob/step01/readme_img/step01/04.png)

### 5. MiniRP 生效
![image](https://github.com/qqshnep/MiniRP/blob/step01/readme_img/step01/05.png)
