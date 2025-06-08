using SceneEditorExtensionExample.SharedData;
using SceneEditorExtensionExample.SharedData.Rendering;
using SceneEditorExtensionExample.SharedData.Terrain3d;
using SceneEditorExtensionExample.StrideAssetExt.Assets;
using SceneEditorExtensionExample.StrideEditorExt;
using SceneEditorExtensionExample.WorldTerrain.Terrain3d.Editor;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;
using Stride.Engine;
using Stride.Engine.Design;
using Stride.Games;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Half = System.Half;

namespace SceneEditorExtensionExample.WorldTerrain.Terrain3d.Layers;

[ComponentCategory("Terrain")]
[DataContract(Inherited = true)]
[DefaultEntityComponentProcessor(typeof(TerrainLayerProcessor), ExecutionMode = ExecutionMode.Runtime | ExecutionMode.Editor)]
public abstract class TerrainLayerComponentBase : EntityComponent
{
#if GAME_EDITOR
    private TimeSpan _getTerrainMapRetryTimeout = TimeSpan.Zero;
#endif

    protected IServiceRegistry Services { get; private set; } = default!;
#if GAME_EDITOR
    [DataMemberIgnore]
    protected internal IStrideEditorService StrideEditorService { get; private set; } = default!;
#endif

    [DataMemberIgnore]
    public bool IsInitialized { get; private set; }

    [Display(Browsable = false)]
    public Guid? LayerId;

    public event EventHandler? LayerChanged;

    public void Initialize(IServiceRegistry services)
    {
        Services = services;
#if GAME_EDITOR
        StrideEditorService = Services.GetSafeServiceAs<IStrideEditorService>();
#endif
        EnsureIdIsSet();

        OnInitialize();
        IsInitialized = true;
    }

    protected virtual void OnInitialize() { }

    protected void EnsureIdIsSet()
    {
        if (!LayerId.HasValue)
        {
            LayerId = Guid.NewGuid();
#if GAME_EDITOR
            StrideEditorService.Invoke(() =>
            {
                using var undoRedoTransaction = StrideEditorService.CreateUndoRedoTransaction("Terrain Layer Component - EnsureIdIsSet");
                StrideEditorService.UpdateAssetComponentData(this, nameof(LayerId), LayerId);
            });
#endif
        }
    }

    public void Deinitialize()
    {
        OnDeinitialize();
    }

    protected virtual void OnDeinitialize() { }

    public void RegisterLayerMetadata(TerrainMapAsset terrainMapAsset)
    {
        OnRegisterLayerMetadata(terrainMapAsset);
    }

    protected abstract void OnRegisterLayerMetadata(TerrainMapAsset terrainMapAsset);

    public virtual void UnregisterLayerMetadata(TerrainMapAsset terrainMapAsset)
    {
        if (LayerId is { } layerId)
        {
            terrainMapAsset.UnregisterLayerMetadata(layerId);
        }
    }

    public void Update(GameTime gameTime, CameraComponent? overrideCameraComponent)
    {
#if GAME_EDITOR
        if (_getTerrainMapRetryTimeout > TimeSpan.Zero)
        {
            _getTerrainMapRetryTimeout -= gameTime.Elapsed;
        }
#endif
        OnUpdate(gameTime, overrideCameraComponent);
    }
    protected virtual void OnUpdate(GameTime gameTime, CameraComponent? overrideCameraComponent) { }

    public void UpdateForDraw(GameTime time, CameraComponent? overrideCameraComponent)
    {
        OnUpdateForDraw(time, overrideCameraComponent);
    }
    protected virtual void OnUpdateForDraw(GameTime time, CameraComponent? overrideCameraComponent) { }

    protected void RaiseLayerChangedEvent()
    {
        LayerChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateHeightmap(Array2d<float> terrainMapHeightmapData, Vector2 heightRange)
    {
        OnUpdateHeightmap(terrainMapHeightmapData, heightRange);
    }

    protected abstract void OnUpdateHeightmap(Array2d<float> terrainMapHeightmapData, Vector2 heightRange);

    protected bool TryGetTerrainMap([NotNullWhen(true)] out TerrainMap? terrainMap, [NotNullWhen(true)] out Entity? terrainEntity)
    {
        terrainMap = null;
        terrainEntity = null;

#if GAME_EDITOR
        if (_getTerrainMapRetryTimeout > TimeSpan.Zero)
        {
            return false;
        }
#endif
        if (Entity.TryFindComponentOnAncestor<TerrainComponent>(out var terrainComp))
        {
            terrainEntity = terrainComp.Entity;
            terrainMap = terrainComp.TerrainMap;
        }
#if GAME_EDITOR
        if (terrainMap is null && Entity.TryFindComponentOnAncestor<TerrainMapPainterComponent>(out var painterComp))
        {
            terrainEntity = painterComp.Entity;
            terrainMap = painterComp.TerrainMap;
        }

        if (terrainMap is not null && !EnsureLoadedTerrainMap(ref terrainMap))
        {
            Debug.WriteLine("TerrainLayerComponentBase: Editor Content Manager Terrain Map not ready. Scheduling retry.");
            _getTerrainMapRetryTimeout = TimeSpan.FromSeconds(3);
            return false;
        }
#endif

        return terrainMap is not null && terrainEntity is not null;
    }

#if GAME_EDITOR
    private bool EnsureLoadedTerrainMap(ref TerrainMap? terrainMap)
    {
        if (terrainMap is not null)
        {
            var terrainMapAttachedRef = AttachedReferenceManager.GetAttachedReference(terrainMap);
            if (terrainMapAttachedRef?.IsProxy ?? false)
            {
                Debug.WriteLine("ModelHeightmapLayerComponent: Editor Content Manager Terrain Map not ready. Scheduling retry.");
                _getTerrainMapRetryTimeout = TimeSpan.FromSeconds(3);
                return false;
            }
        }
        return terrainMap is not null;
    }
#endif

    protected static void TryUnloadAsset<T>(IContentManager contentManager, ref T? backingField) where T : class
    {
        if (backingField is not null)
        {
            contentManager.Unload(backingField);
            backingField = null;
        }
    }

    /// <summary>
    /// Returns the actual region on <paramref name="terrainMapSize"/> that can be written into.
    /// </summary>
    protected static Rectangle CalculateWritableRegion(
        Rectangle subRegion, Int2 terrainMapSize)
    {
        var terrainMapEndIndex = terrainMapSize - Int2.One;

        int startIndexX = MathUtil.Clamp(subRegion.X, min: 0, max: terrainMapEndIndex.X);
        int startIndexY = MathUtil.Clamp(subRegion.Y, min: 0, max: terrainMapEndIndex.Y);

        int endIndexXExcl = MathUtil.Clamp(subRegion.X + subRegion.Width, min: 0, max: terrainMapSize.X);
        int endIndexYExcel = MathUtil.Clamp(subRegion.Y + subRegion.Height, min: 0, max: terrainMapSize.Y);

        int width = endIndexXExcl - startIndexX;
        int height = endIndexYExcel - startIndexY;
        var writableRegion = new Rectangle(startIndexX, startIndexY, width, height);
        return writableRegion;
    }

    protected static void UpdateHeightmapRegion(
        Array2d<float> localRegionData, Int2 startingIndex,
        Array2d<float> terrainMapData, Vector2 heightRange,
        LayerBlendType blendType,
        bool isLocalRegionDataNormalized)
    {
        Func<float, float> toNormalizedHeightmapValue;
        if (isLocalRegionDataNormalized)
        {
            toNormalizedHeightmapValue = x => x;
        }
        else
        {
            toNormalizedHeightmapValue = x => Math.Clamp(MathUtil.InverseLerp(heightRange.X, heightRange.Y, x), 0, 1);
        }
        UpdateHeightmapRegion(
            localRegionData, startingIndex,
            terrainMapData,
            blendType,
            localRegionDataValueToNormalizedFloatFunc: toNormalizedHeightmapValue,
            isValidLocalRegionDataValuePredicate: x => true);
    }

    protected static void UpdateHeightmapRegion(
        Array2d<float?> localRegionData, Int2 startingIndex,
        Array2d<float> terrainMapData, Vector2 heightRange,
        LayerBlendType blendType,
        bool isLocalRegionDataNormalized)
    {
        Func<float?, float> toNormalizedHeightmapValue;
        if (isLocalRegionDataNormalized)
        {
            toNormalizedHeightmapValue = x => x ?? 0;
        }
        else
        {
            toNormalizedHeightmapValue = x => Math.Clamp(MathUtil.InverseLerp(heightRange.X, heightRange.Y, x ?? 0), 0, 1);
        }
        UpdateHeightmapRegion(
            localRegionData, startingIndex,
            terrainMapData,
            blendType,
            localRegionDataValueToNormalizedFloatFunc: toNormalizedHeightmapValue,
            isValidLocalRegionDataValuePredicate: x => x.HasValue);
    }

    protected static void UpdateHeightmapRegion(
        Array2d<Half> localRegionData, Int2 startingIndex,
        Array2d<float> terrainMapData, Vector2 heightRange,
        LayerBlendType blendType,
        bool isLocalRegionDataNormalized)
    {
        Func<Half, float> toNormalizedHeightmapValue;
        if (isLocalRegionDataNormalized)
        {
            toNormalizedHeightmapValue = x => (float)x;
        }
        else
        {
            toNormalizedHeightmapValue = x => Math.Clamp(MathUtil.InverseLerp(heightRange.X, heightRange.Y, (float)x), 0, 1);
        }
        UpdateHeightmapRegion(
            localRegionData, startingIndex,
            terrainMapData,
            blendType,
            localRegionDataValueToNormalizedFloatFunc: toNormalizedHeightmapValue,
            isValidLocalRegionDataValuePredicate: x => true);
    }

    protected static void UpdateHeightmapRegion(
        Array2d<Half?> localRegionData, Int2 startingIndex,
        Array2d<float> terrainMapData, Vector2 heightRange,
        LayerBlendType blendType,
        bool isLocalRegionDataNormalized)
    {
        Func<Half?, float> toNormalizedHeightmapValue;
        if (isLocalRegionDataNormalized)
        {
            toNormalizedHeightmapValue = x => (float)(x ?? Half.Zero);
        }
        else
        {
            toNormalizedHeightmapValue = x => Math.Clamp(MathUtil.InverseLerp(heightRange.X, heightRange.Y, (float)(x ?? Half.Zero)), 0, 1);
        }
        UpdateHeightmapRegion(
            localRegionData, startingIndex,
            terrainMapData,
            blendType,
            localRegionDataValueToNormalizedFloatFunc: toNormalizedHeightmapValue,
            isValidLocalRegionDataValuePredicate: x => x.HasValue);
    }

    protected static void UpdateHeightmapRegion(
        Array2d<ushort> localRegionData, Int2 startingIndex,
        Array2d<float> terrainMapData,
        LayerBlendType blendType)
    {
        UpdateHeightmapRegion(
            localRegionData, startingIndex,
            terrainMapData,
            blendType,
            localRegionDataValueToNormalizedFloatFunc: HeightmapTextureHelper.Int16ToNormalizedFloat,
            isValidLocalRegionDataValuePredicate: x => true);
    }

    protected static void UpdateHeightmapRegion(
        Array2d<ushort?> localRegionData, Int2 startingIndex,
        Array2d<float> terrainMapData,
        LayerBlendType blendType)
    {
        UpdateHeightmapRegion(
            localRegionData, startingIndex,
            terrainMapData,
            blendType,
            localRegionDataValueToNormalizedFloatFunc: x => HeightmapTextureHelper.Int16ToNormalizedFloat(x ?? 0),
            isValidLocalRegionDataValuePredicate: x => x.HasValue);
    }

    protected static void UpdateHeightmapRegion(
        Array2d<byte> localRegionData, Int2 startingIndex,
        Array2d<float> terrainMapData,
        LayerBlendType blendType)
    {
        UpdateHeightmapRegion(
            localRegionData, startingIndex,
            terrainMapData,
            blendType,
            localRegionDataValueToNormalizedFloatFunc: HeightmapTextureHelper.ByteToNormalizedFloat,
            isValidLocalRegionDataValuePredicate: x => true);
    }

    protected static void UpdateHeightmapRegion(
        Array2d<byte?> localRegionData, Int2 startingIndex,
        Array2d<float> terrainMapData,
        LayerBlendType blendType)
    {
        UpdateHeightmapRegion(
            localRegionData, startingIndex,
            terrainMapData,
            blendType,
            localRegionDataValueToNormalizedFloatFunc: x => HeightmapTextureHelper.ByteToNormalizedFloat(x ?? 0),
            isValidLocalRegionDataValuePredicate: x => x.HasValue);
    }

    private static void UpdateHeightmapRegion<TLocalData>(
        Array2d<TLocalData> localRegionData, Int2 startingIndex,
        Array2d<float> terrainMapData,
        LayerBlendType blendType,
        Func<TLocalData, float> localRegionDataValueToNormalizedFloatFunc,
        Predicate<TLocalData> isValidLocalRegionDataValuePredicate)
    {
        var blendFunc = GetBlendFunction(blendType);
        var localRegionRect = new Rectangle(startingIndex.X, startingIndex.Y, localRegionData.LengthX, localRegionData.LengthY);
        var writableRegion = CalculateWritableRegion(localRegionRect, terrainMapData.Length2d);
        if (writableRegion.Height == 0 || writableRegion.Width == 0)
        {
            return;
        }
        for (int y = 0; y < writableRegion.Height; y++)
        {
            for (int x = 0; x < writableRegion.Width; x++)
            {
                int terrainMapIndexX = x + writableRegion.X;
                int terrainMapIndexY = y + writableRegion.Y;
                int localHeightmapIndexX = terrainMapIndexX - startingIndex.X;
                int localHeightmapIndexY = terrainMapIndexY - startingIndex.Y;
                var dataValue = localRegionData[localHeightmapIndexX, localHeightmapIndexY];
                if (!isValidLocalRegionDataValuePredicate(dataValue))
                {
                    continue;
                }
                float localHeightmapValue = localRegionDataValueToNormalizedFloatFunc(dataValue);
                var currentTerrainHeightmapValue = terrainMapData[terrainMapIndexX, terrainMapIndexY];
                terrainMapData[terrainMapIndexX, terrainMapIndexY] = blendFunc(localHeightmapValue, currentTerrainHeightmapValue);
            }
        }
    }

    private static readonly Func<float, float, float> BlendFuncAverage = (val1, val2) => Math.Clamp(MathF.Round((val1 + val2) * 0.5f), 0, 1);
    private static readonly Func<float, float, float> BlendFuncMinimum = (val1, val2) => (val1 <= val2) ? val1 : val2;
    private static readonly Func<float, float, float> BlendFuncMaximum = (val1, val2) => (val1 >= val2) ? val1 : val2;
    private static Func<float, float, float> GetBlendFunction(LayerBlendType blendType)
    {
        return blendType switch
        {
            LayerBlendType.Minimum => BlendFuncMinimum,
            LayerBlendType.Maximum => BlendFuncMaximum,
            _ => BlendFuncAverage,
        };
    }
}
