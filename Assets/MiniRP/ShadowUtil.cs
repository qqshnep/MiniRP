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


    //级联阴影
    private const int CascadeCount = 4;
    private const int ShadowAtlasSize = 2048;
    private static readonly Matrix4x4[]  mainLightShadowMatrices = new Matrix4x4[CascadeCount];

    private static readonly Vector4[] cascadeCullingSpheres = new Vector4[CascadeCount];

    private static readonly Vector3 CascadeRatios = new Vector3(0.1f,0.25f,0.5f);

    private static readonly int MainLightShadowMatricesId = Shader.PropertyToID("_MainLightShadowMatrices");

    private static readonly int CascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres");

    private static readonly int CascadeCountId = Shader.PropertyToID("_CascadeCount");


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
        const int split = 2;
        int tileSize = ShadowAtlasSize / split;

        CommandBuffer cmd = new CommandBuffer { name = "Render Directional Cascades" };

        //创建整个 Atlas
        cmd.GetTemporaryRT(
            MainLightShadowmapId,
            ShadowAtlasSize,
            ShadowAtlasSize,
            32,
            FilterMode.Bilinear,
            RenderTextureFormat.Shadowmap
        );

        cmd.SetRenderTarget(MainLightShadowmapId);
        cmd.ClearRenderTarget(true, false, Color.clear);
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        // Render 4 Cascades
        for (int cascadeIdx = 0; cascadeIdx < CascadeCount; cascadeIdx++)
        {
            //计算光源视角的矩阵信息
            bool success = cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                    lightIndex,

                    // split index
                    cascadeIdx,

                    // split count
                    CascadeCount,

                    // 只有一个 cascade，因此暂时没用
                    CascadeRatios,

                    tileSize,

                    0.1f,

                    out Matrix4x4 viewMatrix,
                    out Matrix4x4 projectionMatrix,
                    out ShadowSplitData splitData
                );

            if (!success)
            {
                continue;
            }

            Vector2Int tileOffset = GetTileOffset(cascadeIdx, split);

            //设置 Atlas tile
            cmd.SetViewport( new Rect(
                    tileOffset.x * tileSize,
                    tileOffset.y * tileSize,
                    tileSize,
                    tileSize
                )
            );

            //切换矩阵
            cmd.SetViewProjectionMatrices(viewMatrix, projectionMatrix);

            // 开启 Shadow Depth Bias
            cmd.SetGlobalDepthBias(0.0f, light.shadowBias);

            // normal bias
            float worldTexelSize = 2.0f * splitData.cullingSphere.w / ShadowMapSize;
            float normalBias = light.shadowNormalBias * worldTexelSize;
            cmd.SetGlobalFloat(MainLightShadowNormalBiasId, normalBias);

            //pcf : shadowmap size
            cmd.SetGlobalVector(MainLightShadowmapSizeId, new Vector4(
                    1.0f / ShadowAtlasSize,
                    1.0f / ShadowAtlasSize,
                    ShadowAtlasSize,
                    ShadowAtlasSize));

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            // Shadow Caster List
            ShadowDrawingSettings settings = new ShadowDrawingSettings(cullingResults, lightIndex);
            settings.splitData = splitData;
            RendererList rendererList = context.CreateShadowRendererList(ref settings);

            cmd.DrawRendererList(rendererList);
            // 非常重要：恢复
            cmd.SetGlobalDepthBias(0.0f, 0.0f);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();


            // 保存 Matrix + CullingSphere
            Vector4 sphere = splitData.cullingSphere;
            sphere.w *= sphere.w; //shader 预处理
            cascadeCullingSpheres[cascadeIdx] = sphere;

            mainLightShadowMatrices[cascadeIdx] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix,tileOffset,split);


            cmd.SetGlobalMatrixArray(MainLightShadowMatricesId, mainLightShadowMatrices);

            cmd.SetGlobalVectorArray(CascadeCullingSpheresId, cascadeCullingSpheres);

            cmd.SetGlobalInt(CascadeCountId, CascadeCount);



            //注意乘法顺序
            Matrix4x4 worldToShadow = ConvertToShadowMatrix(projectionMatrix * viewMatrix);
            cmd.SetGlobalMatrix(MainLightWorldToShadowId, worldToShadow);
            cmd.SetGlobalFloat(MainLightShadowStrengthId, light.shadowStrength);
            context.ExecuteCommandBuffer(cmd);
        }
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


    /*
    index 0 → (0,0)
    index 1 → (1,0)
    index 2 → (0,1)
    index 3 → (1,1)

┌───────┬
│  0    │   1 │
├───────┼
│  2    │   3 │
└───────┴

     */
    private static Vector2Int GetTileOffset(int index, int split)
    {
        return new Vector2Int(
            index % split,
            index / split
        );
    }

    private static Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 matrix, Vector2Int offset,int split)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            matrix.m20 = -matrix.m20;
            matrix.m21 = -matrix.m21;
            matrix.m22 = -matrix.m22;
            matrix.m23 = -matrix.m23;
        }

        float scale =  1.0f / split;


        // X:
        // clip [-1,1]
        // →
        // texture [0,1]
        // →
        // atlas tile

        matrix.m00 =
            (
                0.5f *
                (matrix.m00 + matrix.m30)
                +
                offset.x * matrix.m30
            ) * scale;

        matrix.m01 =
            (
                0.5f *
                (matrix.m01 + matrix.m31)
                +
                offset.x * matrix.m31
            ) * scale;

        matrix.m02 =
            (
                0.5f *
                (matrix.m02 + matrix.m32)
                +
                offset.x * matrix.m32
            ) * scale;

        matrix.m03 =
            (
                0.5f *
                (matrix.m03 + matrix.m33)
                +
                offset.x * matrix.m33
            ) * scale;


        // Y

        matrix.m10 =
            (
                0.5f *
                (matrix.m10 + matrix.m30)
                +
                offset.y * matrix.m30
            ) * scale;

        matrix.m11 =
            (
                0.5f *
                (matrix.m11 + matrix.m31)
                +
                offset.y * matrix.m31
            ) * scale;

        matrix.m12 =
            (
                0.5f *
                (matrix.m12 + matrix.m32)
                +
                offset.y * matrix.m32
            ) * scale;

        matrix.m13 =
            (
                0.5f *
                (matrix.m13 + matrix.m33)
                +
                offset.y * matrix.m33
            ) * scale;


        // Z 从 clip space 转 shadow texture depth

        matrix.m20 =
            0.5f *
            (matrix.m20 + matrix.m30);

        matrix.m21 =
            0.5f *
            (matrix.m21 + matrix.m31);

        matrix.m22 =
            0.5f *
            (matrix.m22 + matrix.m32);

        matrix.m23 =
            0.5f *
            (matrix.m23 + matrix.m33);


        return matrix;
    }
}