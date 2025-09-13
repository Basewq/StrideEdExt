using Stride.Core;
using Stride.Core.Threading;
using Stride.Engine;
using Stride.Rendering;
using StrideEdExt.Painting;
using System.Diagnostics;

namespace StrideEdExt.Rendering.PaintRenderer;

public class PaintingRenderFeature : SubRenderFeature
{
    internal static readonly PropertyKey<HashSet<PaintTargetEntityMesh>?> PickableObjectEntityMeshSetKey
        = new(name: "PaintingPickRenderFeature.PickableObjectEntityMesh", ownerType: typeof(PaintingRenderFeature));

    private ObjectPropertyKey<PaintingPickObjectInfoData> _objectInfoPropertyKey;
    private ConstantBufferOffsetReference _objectInfoDataBuffer;

#if DEBUG
    private bool _isFirstRun = true;
#endif

    protected override void InitializeCore()
    {
        _objectInfoPropertyKey = RootRenderFeature.RenderData.CreateObjectKey<PaintingPickObjectInfoData>();
        _objectInfoDataBuffer = ((RootEffectRenderFeature)RootRenderFeature).CreateDrawCBufferOffsetSlot(PaintingPickOutputShaderKeys.PickObjectInfo.Name);
    }

    public override void Extract()
    {
        if (!Context.VisibilityGroup.Tags.TryGetValue(PickableObjectEntityMeshSetKey, out var pickableEntityMeshes)
            || pickableEntityMeshes is null)
        {
            return;
        }

        int renderObjectCount = 0;
        int validPickCount = 0;
        var objectInfoDataHolder = RootRenderFeature.RenderData.GetData(_objectInfoPropertyKey);
        foreach (var objectNodeReference in RootRenderFeature.ObjectNodeReferences)
        {
            var objectNode = RootRenderFeature.GetObjectNode(objectNodeReference);
            if (objectNode.RenderObject is not RenderMesh renderMesh)
            {
                continue;
            }

            if (renderMesh.Source is not ModelComponent modelComponent)
            {
                continue;
            }

            var key = new PaintTargetEntityMesh
            {
                EntityId = modelComponent.Entity.Id,
                Mesh = renderMesh.Mesh
            };
            var objectInfoData = new PaintingPickObjectInfoData(isPickable: pickableEntityMeshes.Contains(key));    // TODO remove, already in stage selector?
            objectInfoDataHolder[objectNodeReference] = objectInfoData;
            renderObjectCount++;
            validPickCount += objectInfoData.IsPickable > 0 ? 1 : 0;
#if DEBUG
            // This is only for debugging purposes, it can be removed.
            if (_isFirstRun)
            {
                Debug.WriteLine($"Painting Pick Entity: {modelComponent.Entity.Name} - isPickable: {objectInfoData.IsPickable}");
            }
#endif
        }
#if DEBUG
        _isFirstRun = false;
        Debug.WriteLineIf(condition: false, $"Painting Pick Count: {validPickCount} / {renderObjectCount}");
#endif
    }

    public unsafe override void Prepare(RenderDrawContext context)
    {
        // This entire method shows how we pass the PaintingPickData to the shader, and is similar to
        // how Stride does it when it needs to pass data.

        var objectInfoDataHolder = RootRenderFeature.RenderData.GetData(_objectInfoPropertyKey);
        Dispatcher.ForEach(((RootEffectRenderFeature)RootRenderFeature).RenderNodes, (ref RenderNode renderNode) =>
        {
            // PerDrawLayout means we access this data in the shader via
            // cbuffer PerDraw { ... } (see PaintingPickOutputShader.sdsl)
            var perDrawLayout = renderNode.RenderEffect.Reflection?.PerDrawLayout;
            if (perDrawLayout == null)
            {
                return;
            }

            int objectInfoDataOffset = perDrawLayout.GetConstantBufferOffset(_objectInfoDataBuffer);
            if (objectInfoDataOffset == -1)
            {
                return;
            }

            // This is similar to TransformRenderFeature.Prepare
            unsafe
            {
                ref var srcObjectInfoData = ref objectInfoDataHolder[renderNode.RenderObject.ObjectNode];
                var mappedConstBufferIntPtr = renderNode.Resources.ConstantBuffer.Data;
                var destPtr = (PaintingPickObjectInfoData*)((byte*)mappedConstBufferIntPtr + objectInfoDataOffset);
                *destPtr = srcObjectInfoData;
            }
        });
    }
}
