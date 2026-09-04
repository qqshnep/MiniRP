# MiniRP
## Step02:自己做 Culling

### 1. MiniRenderPipeline
```
// 1. 获取 Camera 对应的剔除参数
if (!camera.TryGetCullingParameters(out ScriptableCullingParameters cullingParameters))
{
    return;
}

// 2. 执行剔除
CullingResults cullingResults = context.Cull(ref cullingParameters);
```

### 2. 没有Drawcall FrameDebugger暂时看不到
![image](https://github.com/qqshnep/MiniRP/blob/step02/readme_img/step02/01.png)
   
