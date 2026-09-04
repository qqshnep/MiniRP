using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{
    private static readonly int MainLightDirectionId = Shader.PropertyToID("_MainLightDirection");

    private static readonly int MainLightColorId = Shader.PropertyToID("_MainLightColor");

    public void Setup( ScriptableRenderContext context, CullingResults cullingResults)
    {
        Vector4 lightDirection = Vector4.zero;
        Color lightColor = Color.black;

        var visibleLights = cullingResults.visibleLights;

        for (int i = 0; i < visibleLights.Length; i++)
        {
            VisibleLight visibleLight = visibleLights[i];

            if (visibleLight.lightType == LightType.Directional)
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

                break;
            }
        }

        CommandBuffer cmd = new CommandBuffer
            {
                name = "Setup Lighting"
            };

        cmd.SetGlobalVector( MainLightDirectionId, lightDirection);

        cmd.SetGlobalColor( MainLightColorId, lightColor );

        context.ExecuteCommandBuffer(cmd);

        cmd.Release();
    }
}