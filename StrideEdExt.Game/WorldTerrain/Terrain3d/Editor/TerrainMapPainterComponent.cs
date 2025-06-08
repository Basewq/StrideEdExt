using SceneEditorExtensionExample.SharedData.Terrain3d;
using SceneEditorExtensionExample.StrideEditorExt;
using Stride.Core;
using Stride.Rendering;
using System;

#if GAME_EDITOR
using SceneEditorExtensionExample.UI;
using SceneEditorExtensionExample.WorldTerrain.Terrain3d.Layers;
using SceneEditorExtensionExample.SharedData;
using SceneEditorExtensionExample.StrideAssetExt.Assets;
using Stride.Core.Assets.Editor.ViewModel;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.Dirtiables;
using Stride.Games;
using Stride.Engine;
using Stride.Engine.Design;
using Stride.UI.Controls;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
#endif

namespace SceneEditorExtensionExample.WorldTerrain.Terrain3d.Editor;

[DataContract]
public enum TileCursorModeType
{
    Disabled,
    PaintTexture,
    Erase
}

[DataContract]
public enum TerrainDisplayMode
{
    Normal,
    DebugMode,
}

#if GAME_EDITOR
[DefaultEntityComponentProcessor(typeof(TerrainMapPainterProcessor), ExecutionMode = ExecutionMode.Editor)]
#endif
public class TerrainMapPainterComponent : SceneEditorExtBase
{
    private static readonly TimeSpan ThrottleTime = TimeSpan.FromMilliseconds(150);
    private bool _rebuildMapRequired = false;
    private TimeSpan _rebuildMapDelayTimeRemaining = TimeSpan.Zero;

    private TerrainMap? _terrainMap;
    [DataMember(order: 10)]
    public TerrainMap? TerrainMap
    {
        get => _terrainMap;
        set
        {
#if GAME_EDITOR
            _isLayerMetadataCheckRequired = true;
            if (_terrainMap != value)
            {
                _cachedTerrainMapAssetViewModel = null;
            }
#endif
            SetProperty(ref _terrainMap, value);
        }
    }

    [DataMember(order: 40)]
    internal TileCursorModeType SelectedCursorMode { get; set; } = TileCursorModeType.Disabled;

    /// <summary>
    /// Brush diameter in world units.
    /// </summary>
    ////[DataMember(order: 41)]
    ////[DataMemberRange(minimum: 0, maximum: 40, smallStep: 0.1, largeStep: 1, decimalPlaces: 2)]
    ////internal float BrushSize { get; set; } = 5;

    [DataMember(order: 50)]
    internal TerrainDisplayMode DisplayMode { get; set; } = TerrainDisplayMode.Normal;

    public Material? TerrainMaterial { get; set; }

#if GAME_EDITOR
    private static class UIKeys
    {
        internal static readonly UIElementKey<Button> RegenerateMapButton = new(nameof(RegenerateMapButton));

    }

    //[DataMemberIgnore]
    //internal ITerrainCursorMode? CurrentCursorMode { get; set; }

    //[DataMemberIgnore]
    //internal Dictionary<TileCellType, ITerrainCursorMode> TileCellToCursorModeMap { get; set; } = default!;

    private Button? _regenerateMapButton;

    private AssetViewModel<TerrainMapAsset>? _cachedTerrainMapAssetViewModel;
    internal AssetViewModel<TerrainMapAsset>? GetTerrainInternalAssetViewModel()
    {
        if (TerrainMap is null)
        {
            return null;
        }
        if (_cachedTerrainMapAssetViewModel is not null)
        {
            return _cachedTerrainMapAssetViewModel;
        }
        var terrainMapAssetVm = StrideEditorService.FindAssetViewModel<AssetViewModel<TerrainMapAsset>>(TerrainMap);
        if (terrainMapAssetVm is null)
        {
            if (StrideEditorService.IsActive)
            {
                throw new InvalidOperationException("TerrainMap asset view model not found.");
            }
            else
            {
                // Editor is most likely exiting.
                return null;
            }
        }
        _cachedTerrainMapAssetViewModel = terrainMapAssetVm;
        return terrainMapAssetVm;
    }

    internal TerrainMapAsset? GetTerrainInternalAsset()
    {
        var terrainMapAssetVm = GetTerrainInternalAssetViewModel();
        return terrainMapAssetVm?.Asset;
    }

    protected internal override void Initialize()
    {
        SetupUI();
    }

    private void SetupUI()
    {
        if (UIComponent is not { } uiComp)
        {
            // No UI set
            return;
        }

        if (uiComp.TryGetUI(UIKeys.RegenerateMapButton, out _regenerateMapButton))
        {
            _regenerateMapButton.Click += OnRegenerateMapButtonClicked;
        }
    }

    protected internal override void Deinitialize()
    {
        if (_regenerateMapButton is not null)
        {
            _regenerateMapButton.Click -= OnRegenerateMapButtonClicked;
        }
    }

    private void OnRegenerateMapButtonClicked(object? sender, Stride.UI.Events.RoutedEventArgs e)
    {
        Debug.WriteLine("OnRegenerateMapButtonClicked called.");

        ////if (Terrain is null || Terrain?.HeightmapTexture is null)
        ////{
        ////    return;
        ////}
        ////try
        ////{
        ////    //using var stream = new MemoryStream();
        ////    using var texTool = new TextureTool();

        ////    var heightmapTextureAssetVm = StrideEditorService.FindAssetViewModel<AssetViewModel<TextureAsset>>(Terrain.HeightmapTexture);
        ////    Debug.Assert(heightmapTextureAssetVm is not null);
        ////    var heightmapTextureAsset = heightmapTextureAssetVm.Asset;
        ////    var heightmapTextureFilePath = heightmapTextureAsset.Source.FullPath;

        ////    var texImage = texTool.Load(heightmapTextureFilePath, isSRgb: false);
        ////    texTool.Decompress(texImage, texImage.Format.IsSRgb());
        ////    if (texImage.Format == PixelFormat.R16G16B16A16_UNorm)
        ////    {
        ////        texTool.Convert(texImage, PixelFormat.R8G8B8A8_UNorm);
        ////    }
        ////    var textureSize = Terrain.MapSize + Int2.One;
        ////    texTool.Resize(texImage, textureSize.X, textureSize.Y, Filter.Rescaling.Nearest);
        ////    var image = texTool.ConvertToStrideImage(texImage);

        ////    using var stream = File.OpenWrite(heightmapTextureFilePath);
        ////    image.Save(stream, ImageFileType.Png);
        ////}
        ////catch (Exception ex)
        ////{
        ////    Debug.WriteLine(ex);
        ////}
    }

    protected internal override void Update(GameTime gameTime)
    {
        DoLayerMetadataCheck();
        if (_rebuildMapRequired)
        {
            if (_rebuildMapDelayTimeRemaining > TimeSpan.Zero)
            {
                _rebuildMapDelayTimeRemaining -= gameTime.Elapsed;
                return;     // Delay the update until the user stops interacting
            }

            var terrainMapAsset = GetTerrainInternalAsset();
            if (terrainMapAsset is null)
            {
                return;
            }
            if (terrainMapAsset.HeightmapData is { } heightmapData)
            {
                heightmapData.Clear();
            }
            else
            {
                // New data
                heightmapData = new(terrainMapAsset.MapSize + Int2.One);           // Map size is quad count, so +1 to get vertices count
                terrainMapAsset.HeightmapData = heightmapData;
            }
            var heightRange = terrainMapAsset.HeightRange;
            bool hasUpdatedTerrain = UpdateLayers(heightmapData, heightRange, Entity);
            if (hasUpdatedTerrain && TerrainMap is not null)
            {
                terrainMapAsset.TerrainPropertiesCopyTo(TerrainMap);
                TerrainMap.InvalidateMeshes();

                StrideEditorService.Invoke(() =>
                {
                    var terrainMapAssetVm = StrideEditorService.FindAssetViewModelByAsset<AssetViewModel<TerrainMapAsset>>(terrainMapAsset);
                    if (terrainMapAssetVm is IDirtiable dirtiable)
                    {
                        // Note: the changes here are to do with the intermediate files rather than the asset itself.
                        // TerrainMapViewModel.OnSessionSaved is the when the intermediate files gets saved to disk.
                        dirtiable.UpdateDirtiness(true);
                    }
                });
            }

            _rebuildMapRequired = false;
        }
    }

    private bool _isLayerMetadataCheckRequired = true;
    private void DoLayerMetadataCheck()
    {
        if (!_isLayerMetadataCheckRequired)
        {
            return;
        }
        var terrainMapAsset = GetTerrainInternalAsset();
        if (terrainMapAsset is null)
        {
            return;
        }

        var previousLayerMetadataList = terrainMapAsset.GetAllLayerMetadata().ToList();
        CheckRegisteredLayerMetadata(previousLayerMetadataList, terrainMapAsset, Entity);
        foreach (var prevLayerMetadata in previousLayerMetadataList)
        {
            terrainMapAsset.UnregisterLayerMetadata(prevLayerMetadata.LayerId);
        }
        if (terrainMapAsset.HasLayerMetadataListChanged)
        {
            StrideEditorService.Invoke(() =>
            {
                StrideEditorService.RefreshAssetCollection(terrainMapAsset, TerrainMapAsset.LayerMetadataListName);
            });
            terrainMapAsset.HasLayerMetadataListChanged = false;
        }

        _isLayerMetadataCheckRequired = false;
    }

    private void CheckRegisteredLayerMetadata(List<TerrainMapLayerMetadata> layerMetadataList, TerrainMapAsset terrainMapAsset, Entity parentEntity)
    {
        for (int i = 0; i < parentEntity.Transform.Children.Count; i++)
        {
            var childEntity = parentEntity.Transform.Children[i].Entity;
            for (int compIdx = 0; compIdx < childEntity.Components.Count; compIdx++)
            {
                if (childEntity.Components[compIdx] is TerrainLayerComponentBase layerComp)
                {
                    layerComp.RegisterLayerMetadata(terrainMapAsset);
                    var layerId = layerComp.LayerId!.Value;
                    layerMetadataList.RemoveAll(x => x.LayerId == layerId);
                }
            }

            CheckRegisteredLayerMetadata(layerMetadataList, terrainMapAsset, childEntity);
        }
    }

    private bool UpdateLayers(Array2d<float> heightmapData, Vector2 heightRange, Entity parentEntity)
    {
        // Note that layers are built in reverse order of the entity tree.
        // This is so the layer system is similar to most paint software.

        bool hasUpdatedTerrain = false;
        for (int i = parentEntity.Transform.Children.Count - 1; i >= 0; i--)
        {
            // Update with depth-first search order
            var childEntity = parentEntity.Transform.Children[i].Entity;
            hasUpdatedTerrain = hasUpdatedTerrain || UpdateLayers(heightmapData, heightRange, childEntity);

            for (int compIdx = 0; compIdx < childEntity.Components.Count; compIdx++)
            {
                if (childEntity.Components[compIdx] is TerrainLayerComponentBase layerComp)
                {
                    layerComp.UpdateHeightmap(heightmapData, heightRange);
                    hasUpdatedTerrain = true;
                }
            }
        }
        return hasUpdatedTerrain;
    }
#endif

    public void RebuildMap()
    {
        _rebuildMapRequired = true;     // Don't actually rebuild until the next Update call
        if (_rebuildMapDelayTimeRemaining <= TimeSpan.Zero)
        {
            _rebuildMapDelayTimeRemaining = ThrottleTime;
        }
    }
}
