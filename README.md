# MiniRP
## Step05:平行光

### 1. Lambert公式
```
float4 Frag(Varyings input) : SV_Target
{
    float3 N = normalize(input.normalWS);

    float3 L = normalize(_MainLightDirection.xyz);

    //Lambert
    float NdotL = saturate(dot(N, L));

    float3 color = _BaseColor.rgb * _MainLightColor.rgb * NdotL;

    return float4( color, _BaseColor.a);
}
```

### 2. FrameDebuger
光照是在 Draw Mesh 的时候直接计算的，所以FrameDebugger里不会出现新的Draw Event
![image](https://github.com/qqshnep/MiniRP/blob/step05/readme_img/step05/01.png)

   
