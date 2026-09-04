using UnityEngine;
using UnityEngine.Rendering;

public class ShadowUtil
{
    private const int ShadowMapSize = 2048;

    private static readonly int MainLightShadowmapId = Shader.PropertyToID("_MainLightShadowmap");

    private static readonly int MainLightWorldToShadowId = Shader.PropertyToID("_MainLightWorldToShadow");

    private static readonly int MainLightShadowStrengthId = Shader.PropertyToID("_MainLightShadowStrength");

    private bool shadowMapAllocated;

    private static readonly int MainLightShadowNormalBiasId = Shader.PropertyToID("_MainLightShadowNormalBias");
    private static readonly int MainLightShadowmapSizeId = Shader.PropertyToID("_MainLightShadowmapSize");

    public void Render(ScriptableRenderContext context, CullingResults cullingResults, int mainLightIndex)
    {
        shadowMapAllocated = false;

        if (mainLightIndex < 0)
        {
            SetShadowStrength(context, 0.0f);

            return;
        }

        VisibleLight visibleLight = cullingResults.visibleLights[mainLightIndex];

        Light light = visibleLight.light;

        if (light == null ||
            light.shadows == LightShadows.None ||
            light.shadowStrength <= 0.0f ||
            !cullingResults.GetShadowCasterBounds(mainLightIndex, out _))
        {
            SetShadowStrength(context, 0.0f);

            return;
        }

        RenderDirectionalShadow(context, cullingResults, mainLightIndex, light);
    }

    private void RenderDirectionalShadow(ScriptableRenderContext context, CullingResults cullingResults, int lightIndex, Light light)
    {
        //计算光源视角的矩阵信息
        bool success = cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                lightIndex,

                // split index
                0,

                // split count
                1,

                // 只有一个 cascade，因此暂时没用
                Vector3.zero,

                ShadowMapSize,

                0.1f,

                out Matrix4x4 viewMatrix,
                out Matrix4x4 projectionMatrix,
                out ShadowSplitData splitData
            );

        if (!success)
        {
            SetShadowStrength(context, 0.0f);

            return;
        }

        CommandBuffer cmd = new CommandBuffer
            {
                name = "Render Main Light Shadow"
            };

        cmd.GetTemporaryRT(
            MainLightShadowmapId,

            ShadowMapSize,
            ShadowMapSize,

            32,

            FilterMode.Bilinear,

            RenderTextureFormat.Shadowmap
        );

        //切换RT
        cmd.SetRenderTarget(MainLightShadowmapId);
        cmd.ClearRenderTarget(true, false, Color.clear);

        //切换矩阵
        cmd.SetViewProjectionMatrices(viewMatrix,projectionMatrix);
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        //rendererlist
        ShadowDrawingSettings shadowSettings = new ShadowDrawingSettings(cullingResults, lightIndex);
        shadowSettings.splitData = splitData;
        RendererList shadowRendererList = context.CreateShadowRendererList(ref shadowSettings);

        // 开启 Shadow Depth Bias
        cmd.SetGlobalDepthBias(0.0f, light.shadowBias);

        // normal bias
        float worldTexelSize = 2.0f * splitData.cullingSphere.w / ShadowMapSize;
        float normalBias = light.shadowNormalBias * worldTexelSize;
        cmd.SetGlobalFloat(MainLightShadowNormalBiasId, normalBias);

        //pcf : shadowmap size
        cmd.SetGlobalVector(MainLightShadowmapSizeId, new Vector4(
                1.0f / ShadowMapSize,
                1.0f / ShadowMapSize,
                ShadowMapSize,
                ShadowMapSize));

        cmd.DrawRendererList(shadowRendererList);

        // 非常重要：恢复
        cmd.SetGlobalDepthBias(0.0f, 0.0f);

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        //注意乘法顺序
        Matrix4x4 worldToShadow = ConvertToShadowMatrix(projectionMatrix * viewMatrix);
        cmd.SetGlobalMatrix(MainLightWorldToShadowId, worldToShadow);
        cmd.SetGlobalFloat(MainLightShadowStrengthId, light.shadowStrength);
        context.ExecuteCommandBuffer(cmd);
        cmd.Release();

        shadowMapAllocated = true;
    }


    private Matrix4x4 ConvertToShadowMatrix(Matrix4x4 matrix)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            matrix.m20 = -matrix.m20;
            matrix.m21 = -matrix.m21;
            matrix.m22 = -matrix.m22;
            matrix.m23 = -matrix.m23;
        }

        Matrix4x4 scaleOffset = Matrix4x4.identity;

        //x * 0.5 + 0.5, 坐标映射 [-1, 1] -> [0, 1]
        scaleOffset.m00 = 0.5f;
        scaleOffset.m11 = 0.5f;
        scaleOffset.m22 = 0.5f;

        scaleOffset.m03 = 0.5f;
        scaleOffset.m13 = 0.5f;
        scaleOffset.m23 = 0.5f;

        return scaleOffset * matrix;
    }

    private void SetShadowStrength(ScriptableRenderContext context, float strength)
    {
        CommandBuffer cmd = new CommandBuffer
            {
                name = "Disable Main Shadow"
            };

        cmd.SetGlobalFloat(MainLightShadowStrengthId, strength);

        context.ExecuteCommandBuffer(cmd);

        cmd.Release();
    }

    public void Cleanup(ScriptableRenderContext context)
    {
        if (!shadowMapAllocated)
        {
            return;
        }

        CommandBuffer cmd = new CommandBuffer
            {
                name = "Release Shadow Map"
            };

        cmd.ReleaseTemporaryRT(MainLightShadowmapId);

        context.ExecuteCommandBuffer(cmd);

        cmd.Release();

        shadowMapAllocated = false;
    }
}