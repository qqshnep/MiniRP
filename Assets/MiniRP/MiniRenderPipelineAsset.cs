using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "MiniRP/Render Pipeline Asset", fileName = "MiniRenderPipelineAsset")]
public class MiniRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField]
    private Cubemap environmentCubemap;

 
    protected override RenderPipeline CreatePipeline()
    {
        return new MiniRenderPipeline(environmentCubemap);
    }
}