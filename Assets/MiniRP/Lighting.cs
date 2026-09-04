using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{
    //主光源，平行光
    private static readonly int MainLightDirectionId = Shader.PropertyToID("_MainLightDirection");

    private static readonly int MainLightColorId = Shader.PropertyToID("_MainLightColor");


    //次光源，暂时只考虑点光源
    private const int MaxOtherLightCount = 4; //最多4个
    private static readonly int OtherLightCountId = Shader.PropertyToID("_OtherLightCount");

    private static readonly int OtherLightPositionsId = Shader.PropertyToID("_OtherLightPositions");

    private static readonly int OtherLightColorsId = Shader.PropertyToID("_OtherLightColors");

    private static readonly int OtherLightParamsId = Shader.PropertyToID("_OtherLightParams");

    private readonly Vector4[] otherLightPositions = new Vector4[MaxOtherLightCount];

    private readonly Vector4[] otherLightColors = new Vector4[MaxOtherLightCount];

    private readonly Vector4[] otherLightParams = new Vector4[MaxOtherLightCount];


    public void Setup( ScriptableRenderContext context, CullingResults cullingResults)
    {
        Vector4 lightDirection = Vector4.zero;
        Color lightColor = Color.black;

        int otherLightCount = 0;
        bool findMainLight = false; //目前只使用第一盏方向光

        var visibleLights = cullingResults.visibleLights;

        for (int i = 0; i < visibleLights.Length; i++)
        {
            VisibleLight visibleLight = visibleLights[i];

            if (visibleLight.lightType == LightType.Directional)
            {
                if (!findMainLight)
                {
                    //Unity 的 Transform Matrix4x4 前三列分别对应 X、Y、Z 轴
                    //左手坐标系，Z+轴是前方
                    Vector4 forward = visibleLight.localToWorldMatrix.GetColumn(2);

                    // Shader 中使用的是：
                    // surface → light
                    //
                    // Directional Light 的 forward 是：
                    // light → scene
                    //
                    // 所以取反
                    lightDirection = new Vector4(
                        -forward.x,
                        -forward.y,
                        -forward.z,
                        0.0f
                    );

                    lightColor = visibleLight.finalColor;

                    findMainLight = true;
                }
            }
            else if (visibleLight.lightType == LightType.Point &&
                    otherLightCount < MaxOtherLightCount)
            {
                SetupPointLight(otherLightCount, visibleLight);

                otherLightCount++;
            }
        }

        CommandBuffer cmd = new CommandBuffer
            {
                name = "Setup Lighting"
            };

        //平行光
        cmd.SetGlobalVector( MainLightDirectionId, lightDirection);
        cmd.SetGlobalColor( MainLightColorId, lightColor );

        //点光
        cmd.SetGlobalInt(OtherLightCountId, otherLightCount);
        cmd.SetGlobalVectorArray(OtherLightPositionsId, otherLightPositions);
        cmd.SetGlobalVectorArray(OtherLightColorsId, otherLightColors);
        cmd.SetGlobalVectorArray(OtherLightParamsId, otherLightParams);

        context.ExecuteCommandBuffer(cmd);

        cmd.Release();
    }


    private void SetupPointLight(int index, VisibleLight visibleLight)
    {
        //第四列，是world pos
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);

        //w=1,齐次坐标变换需要
        otherLightPositions[index] = new Vector4(
                position.x,
                position.y,
                position.z,
                1.0f
            );

        otherLightColors[index] = visibleLight.finalColor;

        float range = visibleLight.range;

        float inverseRangeSquared = 1.0f / Mathf.Max(range * range, 0.00001f);

        otherLightParams[index] = new Vector4(
                inverseRangeSquared,
                0,
                0,
                0
            );
    }
}