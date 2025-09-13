using Stride.Core.Annotations;
using Stride.Engine;
using Stride.Games;
using StrideEdExt.SharedData.StrideEditorExt.EditorRuntimeInterfacing;
using StrideEdExt.SharedData.StrideEditorExt.EditorRuntimeInterfacing.EditorToRuntimeMessages;
using StrideEdExt.SharedData.Terrain3d;
using StrideEdExt.SharedData.Terrain3d.EditorToRuntimeMessages;
using StrideEdExt.StrideEditorExt;
using StrideEdExt.WorldTerrain.Terrain3d.Layers;
using StrideEdExt.WorldTerrain.Terrain3d.Layers.Heightmaps;
using StrideEdExt.WorldTerrain.Terrain3d.Layers.MaterialMaps;
using StrideEdExt.WorldTerrain.Terrain3d.Layers.MaterialWeightMaps;
using System.Diagnostics.CodeAnalysis;
using NotNull = Stride.Core.Annotations.NotNullAttribute;

namespace StrideEdExt.WorldTerrain.Terrain3d.Editor;

class TerrainMapEditorProcessor : EntityProcessor<TerrainMapEditorComponent, TerrainMapEditorProcessor.AssociatedData>
{
    private IRuntimeToEditorMessagingService? _runtimeToEditorMessagingService;
    private List<IDisposable> _editorMessageSubscriptions = [];

    private DateTime _processorStartTime = DateTime.MaxValue;

    public TerrainMapEditorProcessor()
    {
        Order = 100100;     // Make this processor's update call after any camera position changes and after SceneEditorExtProcessor
    }

    protected override void OnSystemAdd()
    {
        _runtimeToEditorMessagingService = Services.GetService<IRuntimeToEditorMessagingService>();
        if (_runtimeToEditorMessagingService is not null)
        {
            RegisterMessageHandler<TestEditorToRuntimeMessage>(msg =>
            {
                System.Diagnostics.Debug.WriteLine($"Message was: {msg.Message}");
            });

            // Heightmap messages
            RegisterMessageHandler<SetTerrainMapHeightmapDataMessage>(msg =>
            {
                var (editorComp, data) = ComponentDatas.FirstOrDefault(x => x.Key.TerrainMap?.TerrainMapAssetId == msg.TerrainMapAssetId);
                if (data?.TerrainComponent is TerrainComponent terrainComp)
                {
                    terrainComp.UpdateHeightmap(msg.HeightmapTextureSize, msg.HeightmapData);
                }
            });
            RegisterMessageHandler<SetModelHeightmapDataMessage>(msg =>
            {
                if (TryGetComponentByLayerId<ModelHeightmapLayerComponent>(msg.LayerId, out var layerComp))
                {
                    layerComp.UpdateData(msg.LayerHeightmapData, msg.HeightmapTexturePixelStartPosition);
                }
            });
            RegisterMessageHandler<SetPainterHeightmapDataMessage>(msg =>
            {
                if (TryGetComponentByLayerId<PainterHeightmapLayerComponent>(msg.LayerId, out var layerComp))
                {
                    layerComp.UpdateData(msg.LayerHeightmapData, msg.HeightmapTexturePixelStartPosition);
                }
            });
            RegisterMessageHandler<SetTextureHeightmapDataMessage>(msg =>
            {
                if (TryGetComponentByLayerId<TextureHeightmapLayerComponent>(msg.LayerId, out var layerComp))
                {
                    layerComp.UpdateData(msg.LayerHeightmapData, msg.HeightmapTexturePixelStartPosition);
                }
            });

            // Material Map messages
            RegisterMessageHandler<SetTerrainMapMaterialLayerIndexListMessage>(msg =>
            {
                var (editorComp, data) = ComponentDatas.FirstOrDefault(x => x.Key.TerrainMap?.TerrainMapAssetId == msg.TerrainMapAssetId);
                if (editorComp is not null)
                {
                    var materialLayers = msg.MaterialLayers.Select(x => new TerrainMapMaterialLayerData(x.MaterialName, x.MaterialIndex));
                    editorComp.UpdateMaterialLayerList(materialLayers);
                }
            });
            RegisterMessageHandler<SetTerrainMapMaterialIndexMapDataMessage>(msg =>
            {
                var (editorComp, data) = ComponentDatas.FirstOrDefault(x => x.Key.TerrainMap?.TerrainMapAssetId == msg.TerrainMapAssetId);
                if (data?.TerrainComponent is TerrainComponent terrainComp)
                {
                    terrainComp.UpdateMaterialIndexMap(msg.MaterialIndexMapData, msg.MaterialWeightMapData);
                }
            });
            RegisterMessageHandler<SetPainterMaterialWeightMapDataMessage>(msg =>
            {
                if (TryGetComponentByLayerId<PainterMaterialWeightMapLayerComponent>(msg.LayerId, out var layerComp))
                {
                    layerComp.UpdateData(msg.MaterialWeightMapData, msg.MaterialWeightMapTexturePixelStartPosition);
                }
            });
            RegisterMessageHandler<SetTextureMaterialWeightMapDataMessage>(msg =>
            {
                if (TryGetComponentByLayerId<TextureMaterialWeightMapLayerComponent>(msg.LayerId, out var layerComp))
                {
                    layerComp.UpdateData(msg.MaterialWeightMapData, msg.MaterialWeightMapTexturePixelStartPosition);
                }
            });

            void RegisterMessageHandler<TMessage>(Action<TMessage> messageHandler)
                where TMessage : IEditorToRuntimeMessage
            {
                var sub = _runtimeToEditorMessagingService.Subscribe<TMessage>(this, messageHandler);
                _editorMessageSubscriptions.Add(sub);
            }
        }

        _processorStartTime = DateTime.Now.AddSeconds(3);
    }

    private bool TryGetComponentByLayerId<TComponent>(Guid layerId, [NotNullWhen(true)] out TComponent? layerComponent)
        where TComponent : TerrainLayerComponentBase
    {
        foreach (var (editorComp, compData) in ComponentDatas)
        {
            if (editorComp.Entity.TryFindComponentOnDescendant(x => x.LayerId == layerId, out layerComponent))
            {
                return true;
            }
        }
        layerComponent = null;
        return false;
    }

    protected override void OnSystemRemove()
    {
        foreach (var sub in _editorMessageSubscriptions)
        {
            sub.Dispose();
        }
        _editorMessageSubscriptions.Clear();
    }

    protected override AssociatedData GenerateComponentData([NotNull] Entity entity, [NotNull] TerrainMapEditorComponent component)
    {
        return new AssociatedData
        {
        };
    }

    protected override void OnEntityComponentAdding(Entity entity, [NotNull] TerrainMapEditorComponent component, [NotNull] AssociatedData data)
    {
        component.EditorProcessor = this;
    }

    protected override void OnEntityComponentRemoved(Entity entity, [NotNull] TerrainMapEditorComponent component, [NotNull] AssociatedData data)
    {
        if (data.TerrainPreviewEntity is not null)
        {
            data.TerrainPreviewEntity.Scene = null;
        }
    }

    public override void Update(GameTime time)
    {
        if (DateTime.Now < _processorStartTime)
        {
            // HACK: Delay further execution because code like editorComp.GetTerrainInternalAsset() calls code on UI thread
            // which can crash the editor when called while the scene is still being loaded in the editor
            return;
        }

        foreach (var kv in ComponentDatas)
        {
            var editorComp = kv.Key;
            var data = kv.Value;

            if (!editorComp.IsInitialized)
            {
                continue;
            }
            if (editorComp.TerrainMap is null || !EditorExtensions.IsRuntimeAssetLoaded(editorComp))
            {
                continue;
            }

            if (data.LoadedTerrainMap != editorComp.TerrainMap)
            {
                // Must wait until array has loaded with correct size (eg. due to resizing the terrain map size)
                if (editorComp.TerrainMap.HeightmapData?.Length2d == editorComp.TerrainMap.HeightmapTextureSize)
                {
                    if (!editorComp.TerrainMap.IsInitialized)
                    {
                        editorComp.TerrainMap.Initialize();
                    }
                    data.LoadedTerrainMap = editorComp.TerrainMap;
                    if (data.TerrainComponent is not null)
                    {
                        data.TerrainComponent.TerrainMap = editorComp.TerrainMap;
                        data.TerrainComponent.BuildPhysicsColliders();
                    }
                }
            }
            if (data.LoadedTerrainMap is not null && data.TerrainPreviewEntity is null)
            {
                // Warning: this bypasses the editor and does not appear in the scene/entity tree.
                // An issue is if the user adds any new entities to the scene (at the top level)
                // then the editor will crash because the quantum node tree detects differences
                // in the scene's entity hierarchy, so avoid manually adding more entities and painting
                // at the same time, just save/reload after you add any additional entities.
                data.TerrainComponent = new()
                {
                    IsEditorEnabled = true,
                    TerrainMap = data.LoadedTerrainMap,
                    //TerrainMaterial = data.LoadedTerrainMaterial,
                    MaxChunkRenderDistance = 1000,     // Increase viewing distance for the editor
                };
                data.TerrainPreviewEntity = new()
                {
                    Name = "TerrainPreviewEntity"
                };
                data.TerrainPreviewEntity.Add(data.TerrainComponent);
                var editorScene = editorComp.Entity.Scene;
                data.TerrainPreviewEntity.Scene = editorScene;
                //data.TerrainPreviewEntity.SetParent(editorComp.Entity);    // Only use this if trying to see the model wireframe - the editor cannot handle entity hierarchy modifications

                if (data.LoadedTerrainMap.IsInitialized)
                {
                    data.TerrainComponent.BuildPhysicsColliders();
                }
            }

            break;     // Only deal with one editorComp...
        }
    }

    internal bool TryGetTerrainComponent(TerrainMapEditorComponent terrainMapEditorComponent, [NotNullWhen(true)] out TerrainComponent? terrainComponent)
    {
        if (ComponentDatas.TryGetValue(terrainMapEditorComponent, out var data))
        {
            terrainComponent = data.TerrainComponent;
            return terrainComponent is not null;
        }

        terrainComponent = null;
        return false;
    }

    internal class AssociatedData
    {
        public TerrainMap? LoadedTerrainMap;

        public Entity? TerrainPreviewEntity;
        public TerrainComponent? TerrainComponent;
    }
}
