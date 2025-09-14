﻿using StrideEdExt.Rendering;
using Stride.Core.Annotations;
using Stride.Engine;
using Stride.Rendering;
using System.Collections.Generic;

namespace StrideEdExt.WorldTerrain.Foliage;

// Adapted from Stride.Engine.Processors.InstancingProcessor
class FoliageChunkInstancingProcessor : EntityProcessor<FoliageChunkInstancingComponent, FoliageChunkInstancingProcessor.AssociatedData>, IEntityComponentRenderProcessor
{
    private readonly Dictionary<RenderModel, FoliageChunkInstancingComponent> _modelInstancingMap = new();
    private ModelRenderProcessor _modelRenderProcessor = default!;

    public VisibilityGroup VisibilityGroup { get; set; } = default!;

    public FoliageChunkInstancingProcessor()
    {
        Order = 100001;  // Make sure this occurs after FoliageInstancingManagerProcessor
    }

    protected override void OnSystemAdd()
    {
        VisibilityGroup.Tags.Set(FoliageInstancingRenderFeature.ModelToInstancingMapKey, _modelInstancingMap);

        _modelRenderProcessor = EntityManager.GetProcessor<ModelRenderProcessor>();
        if (_modelRenderProcessor is null)
        {
            _modelRenderProcessor = new ModelRenderProcessor();
            EntityManager.Processors.Add(_modelRenderProcessor);
        }
    }

    protected override void OnSystemRemove()
    {
        VisibilityGroup.Tags.Remove(FoliageInstancingRenderFeature.ModelToInstancingMapKey);
    }

    protected override AssociatedData GenerateComponentData([NotNull] Entity entity, [NotNull] FoliageChunkInstancingComponent component)
    {
        return new AssociatedData
        {
        };
    }

    protected override bool IsAssociatedDataValid([NotNull] Entity entity, [NotNull] FoliageChunkInstancingComponent component, [NotNull] AssociatedData data)
    {
        return true;
    }

    protected override void OnEntityComponentAdding(Entity entity, [NotNull] FoliageChunkInstancingComponent component, [NotNull] AssociatedData data)
    {
        data.ModelComponent = entity.Get<ModelComponent>();

        if (data.ModelComponent is not null
            && _modelRenderProcessor.RenderModels.TryGetValue(data.ModelComponent, out var renderModel))
        {
            _modelInstancingMap[renderModel] = component;
            data.RenderModel = renderModel;
        }
    }

    protected override void OnEntityComponentRemoved(Entity entity, [NotNull] FoliageChunkInstancingComponent component, [NotNull] AssociatedData data)
    {
        if (data.RenderModel is not null)
        {
            _modelInstancingMap.Remove(data.RenderModel);
        }

        component.Dispose();
    }

    public class AssociatedData
    {
        public ModelComponent? ModelComponent;
        public RenderModel? RenderModel;
    }
}
