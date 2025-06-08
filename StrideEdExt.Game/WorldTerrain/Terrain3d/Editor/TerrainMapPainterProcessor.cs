#if GAME_EDITOR
using SceneEditorExtensionExample.SharedData.Terrain3d;
using SceneEditorExtensionExample.StrideEditorExt;
using Stride.Assets.Presentation.AssetEditors.EntityHierarchyEditor.Game;
using Stride.Assets.Presentation.AssetEditors.GameEditor.Game;
using Stride.Assets.Presentation.AssetEditors.SceneEditor.Game;
using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Diagnostics;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;
using Stride.Editor.EditorGame.Game;
using Stride.Engine;
using Stride.Games;
using Stride.Input;
using Stride.Rendering;
using Stride.Rendering.Lights;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace SceneEditorExtensionExample.WorldTerrain.Terrain3d.Editor;

class TerrainMapPainterProcessor : EntityProcessor<TerrainMapPainterComponent, TerrainMapPainterProcessor.AssociatedData>
{
    private Random _random = new(Seed: 1000);

    private ILogger? _logger;
    private ContentManager _contentManager = default!;
    private InputManager _inputManager = default!;
    private SceneEditorGame _sceneEditorGame = default!;
    private IStrideEditorService _strideEditorService = default!;
    private TerrainMapPainterEditorMouseService _painterMouseService = default!;

    private bool _isInstancingRenderFeatureCheckRequired = true;

    private DateTime _processorStartTime = DateTime.MaxValue;

    public TerrainMapPainterProcessor()
    {
        Order = 100100;     // Make this processor's update call after any camera position changes and after SceneEditorExtProcessor
    }

    private ILogger GetLogger()
    {
        if (_logger is not null)
        {
            return _logger;
        }

        _logger = GlobalLogger.GetLogger(GetType().FullName!);
        return _logger;
    }

    protected override void OnSystemAdd()
    {
        _logger = Services.GetService<ILogger>();
        _contentManager = Services.GetSafeServiceAs<ContentManager>();
        _inputManager = Services.GetSafeServiceAs<InputManager>();
        _sceneEditorGame = (SceneEditorGame)Services.GetSafeServiceAs<IGame>();
        _strideEditorService = Services.GetSafeServiceAs<IStrideEditorService>();

        _painterMouseService = _sceneEditorGame.EditorServices.Get<TerrainMapPainterEditorMouseService>();
        if (_painterMouseService is null)
        {
            Debug.WriteLine($"{nameof(TerrainMapPainterEditorMouseService)} added.");

            _painterMouseService = new();

            // HACK: Every EditorGameMouseServiceBase derived classes hold a LOCAL copy of
            // every other mouse service instead of reading from some common registry...
            // This code manually goes through every other mouse service and add our one in.
            var mouseServiceType = typeof(EditorGameMouseServiceBase);
            var mouseSvceListFieldInfo = mouseServiceType.GetField("mouseServices", BindingFlags.Instance | BindingFlags.NonPublic);
            Debug.Assert(mouseSvceListFieldInfo is not null);

            foreach (var editorService in _sceneEditorGame.EditorServices.Services)
            {
                if (editorService is not EditorGameMouseServiceBase mouseSvce)
                {
                    continue;
                }
                var mouseSvceList = mouseSvceListFieldInfo.GetValue(mouseSvce) as List<IEditorGameMouseService>;
                if (mouseSvceList is not null)
                {
                    Debug.WriteLine($"Found mouse services: {mouseSvceList.Count}");
                    mouseSvceList.Add(_painterMouseService);
                }
            }

            _painterMouseService.InitializeService(_sceneEditorGame);

            var editorServiceType = typeof(EditorGameServiceBase);
            var editorSvceRegisterMouseServicesMethodInfo = mouseServiceType.GetMethod("RegisterMouseServices", BindingFlags.Instance | BindingFlags.NonPublic);
            EditorGameServiceRegistry serviceRegistry = _sceneEditorGame.EditorServices;
            editorSvceRegisterMouseServicesMethodInfo?.Invoke(_painterMouseService, [serviceRegistry]);
            _sceneEditorGame.EditorServices.Add(_painterMouseService);
        }
        else
        {
            Debug.WriteLine($"{nameof(TerrainMapPainterEditorMouseService)} already registered.");
        }

        var selectionService = _sceneEditorGame.EditorServices.Get<IEditorGameEntitySelectionService>();
        if (selectionService is not null)
        {
            selectionService.SelectionUpdated += EntitySelectionService_OnSelectionUpdated;
        }

        _processorStartTime = DateTime.Now.AddSeconds(3);
    }

    private void EntitySelectionService_OnSelectionUpdated(object? sender, EntitySelectionEventArgs e)
    {
        // Stop painting if the user changed entity selection
        _strideEditorService.Invoke(() =>
        {
            var deselectedPainters = ComponentDatas
                                        .Select(x => x.Key)
                                        .Where(painterComp => !e.NewSelection.Any(selectedEntity => selectedEntity.Id == painterComp.Entity.Id))    // All painters that are not selected
                                        .Where(painterComp => painterComp.SelectedCursorMode != TileCursorModeType.Disabled)
                                        .ToList();
            if (deselectedPainters.Count > 0)
            {
                using var undoRedoTransaction = _strideEditorService.CreateUndoRedoTransaction("Painter Entity Deselected - Disable Painters");
                foreach (var painterComp in deselectedPainters)
                {
                    _strideEditorService.UpdateAssetComponentData(painterComp, propertyName: nameof(TerrainMapPainterComponent.SelectedCursorMode), TileCursorModeType.Disabled);
                }
            }
        });
    }

    protected override void OnSystemRemove()
    {
        var selectionService = _sceneEditorGame.EditorServices.Get<IEditorGameEntitySelectionService>();
        if (selectionService is not null)
        {
            selectionService.SelectionUpdated -= EntitySelectionService_OnSelectionUpdated;
        }
    }

    protected override AssociatedData GenerateComponentData([NotNull] Entity entity, [NotNull] TerrainMapPainterComponent component)
    {
        return new AssociatedData
        {
        };
    }

    protected override void OnEntityComponentAdding(Entity entity, [NotNull] TerrainMapPainterComponent component, [NotNull] AssociatedData data)
    {
        component.PropertyChanged += OnPainterComponentPropertyChanged;
        component.SelectedCursorMode = TileCursorModeType.Disabled;
    }

    private void OnPainterComponentPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs ev)
    {
        if (sender is not TerrainMapPainterComponent painterComp)
        {
            return;
        }
        //switch (ev.PropertyName)
        //{
        //    case nameof(TerrainMapPainterComponent.TerrainMap):
        //        if (painterComp.IsInitialized)
        //        {
        //            var terrainMapAsset = painterComp.GetTerrainInternalAsset();
        //            if (terrainMapAsset is not null
        //                && ComponentDatas.TryGetValue(painterComp, out var data)
        //                && data.IsInitialInstancingDisplayed)   // If it's still being initialized we ignore property changes
        //            {
        //            }
        //        }
        //        break;
        //}
    }

    protected override void OnEntityComponentRemoved(Entity entity, [NotNull] TerrainMapPainterComponent component, [NotNull] AssociatedData data)
    {
        component.PropertyChanged -= OnPainterComponentPropertyChanged;

        if (data.TerrainComponent is not null)
        {
            entity.Remove(data.TerrainComponent);
            data.TerrainComponent = null;
        }
        if (data.PaintPreviewEntity is not null)
        {
            data.PaintPreviewEntity.Scene = null;
        }
    }

    public override void Update(GameTime time)
    {
        if (_sceneEditorGame is null || _sceneEditorGame.IsExiting)
        {
            return;
        }
        if (DateTime.Now < _processorStartTime)
        {
            // HACK: Delay further execution because code like painterComp.GetTerrainInternalAsset() calls code on UI thread
            // which can crash the editor when called while the scene is still being loaded in the editor
            return;
        }
        if (_isInstancingRenderFeatureCheckRequired)
        {
            var meshRenderFeature = _sceneEditorGame.SceneSystem.GraphicsCompositor.RenderFeatures.OfType<MeshRenderFeature>().FirstOrDefault();
            if (meshRenderFeature is not null)
            {
                if (!meshRenderFeature.RenderFeatures.Any(x => x is InstancingRenderFeature))
                {
                    var instRendFeature = new InstancingRenderFeature();
                    if (meshRenderFeature.RenderFeatures.TryFindIndex(x => x is ForwardLightingRenderFeature, out int insertIndex))
                    {
                        meshRenderFeature.RenderFeatures.Insert(insertIndex + 1, instRendFeature);
                    }
                    else
                    {
                        meshRenderFeature.RenderFeatures.Add(instRendFeature);
                    }
                }
                _isInstancingRenderFeatureCheckRequired = false;
            }
        }
        foreach (var kv in ComponentDatas)
        {
            var painterComp = kv.Key;
            var data = kv.Value;

            if (!painterComp.IsInitialized)
            {
                continue;
            }
            var terrainMapAssetVm = painterComp.GetTerrainInternalAssetViewModel();
            var terrainMapAsset = terrainMapAssetVm?.Asset;
            if (terrainMapAsset is null)
            {
                continue;
            }
            var logger = GetLogger();
            var packageFolderPath = terrainMapAssetVm?.AssetItem?.Package?.FullPath.GetFullDirectory();
            if (packageFolderPath is null)
            {
                continue;
            }
            terrainMapAsset.EnsureFinalizeContentDeserialization(logger, packageFolderPath);

            if (data.LoadedTerrainMap != painterComp.TerrainMap && painterComp.TerrainMap is not null)
            {
                // HACK: editor assigns a proxy object before properly loading the object.
                // We are required to check for this.
                var terrainMapAttachedRef = AttachedReferenceManager.GetAttachedReference(painterComp.TerrainMap);
                if (terrainMapAttachedRef?.IsProxy == false && !painterComp.TerrainMap.IsInitialized)
                {
                    painterComp.TerrainMap.Initialize();
                    data.LoadedTerrainMap = painterComp.TerrainMap;
                    if (data.TerrainComponent is not null)
                    {
                        data.TerrainComponent.TerrainMap = painterComp.TerrainMap;
                    }
                }
            }
            if (data.LoadedTerrainMap is not null && data.TerrainComponent is null)
            {
                data.TerrainComponent = new()
                {
                    TerrainMap = data.LoadedTerrainMap,
                    TerrainMaterial = data.LoadedTerrainMaterial,
                    MaxChunkRenderDistance = 1000,     // Increase viewing distance for the editor
                };
                painterComp.Entity.Add(data.TerrainComponent);
            }

            if (data.LoadedTerrainMaterial != painterComp.TerrainMaterial && painterComp.TerrainMaterial is not null)
            {
                var terrainMaterialAttachedRef = AttachedReferenceManager.GetAttachedReference(painterComp.TerrainMaterial);
                if (terrainMaterialAttachedRef?.IsProxy == false)
                {
                    data.LoadedTerrainMaterial = painterComp.TerrainMaterial;
                    if (data.TerrainComponent is not null)
                    {
                        data.TerrainComponent.TerrainMaterial = data.LoadedTerrainMaterial;
                    }
                }
            }

            break;     // Only deal with one painterComp...
        }
    }

    private static void PopulateBrushTileCellIndices(
        BoundingSphere boundingSphere, Vector3 posToTileCellIndex,
        HashSet<TileCellIndexXZ> tileCellIndicesOutput)
    {
        //int radius = (int)MathF.Round(boundingSphere.Radius * posToTileCellIndex.X, digits: 0, mode: MidpointRounding.ToPositiveInfinity);
        int radius = (int)MathF.Floor(boundingSphere.Radius * posToTileCellIndex.X);
        var indexVec = boundingSphere.Center * posToTileCellIndex;
        var tileCellCenterIndex = TileCellIndexXZ.ToTileCellIndex(indexVec);

        if (radius <= 0)
        {
            tileCellIndicesOutput.Add(tileCellCenterIndex);
            return;
        }

        // Use Midpoint Circle Algorithm for to determine the brush's tile indices
        int decisionCriterion = (5 - radius * 4) / 4;
        int x = 0;
        int y = radius;

        do
        {
            FillCellIndices(tileCellCenterIndex.X, x, tileCellCenterIndex.Z - y, tileCellIndicesOutput);
            FillCellIndices(tileCellCenterIndex.X, x, tileCellCenterIndex.Z + y, tileCellIndicesOutput);

            FillCellIndices(tileCellCenterIndex.X, y, tileCellCenterIndex.Z - x, tileCellIndicesOutput);
            FillCellIndices(tileCellCenterIndex.X, y, tileCellCenterIndex.Z + x, tileCellIndicesOutput);

            if (decisionCriterion < 0)
            {
                decisionCriterion += 2 * x + 1;
            }
            else
            {
                decisionCriterion += 2 * (x - y) + 1;
                y--;
            }
            x++;
        } while (x <= y);

        static void FillCellIndices(int xCenter, int halfLength, int z, HashSet<TileCellIndexXZ> tileCellIndicesOutput)
        {
            int xStart = xCenter - halfLength;
            int xEnd = xCenter + halfLength;
            for (int x = xStart ; x <= xEnd; x++)
            {
                tileCellIndicesOutput.Add(new TileCellIndexXZ(x, z));
            }
        }
    }

    private static float GetRandomValue(Random random, Vector2 valueRange)
    {
        float rndValue = random.NextSingle();
        float range = valueRange.Y - valueRange.X;
        float finalRndValue = valueRange.X + range * rndValue;
        return finalRndValue;
    }

    public class AssociatedData
    {
        public bool IsInitialInstancingDisplayed = false;
        //public bool IsEnabled = false;

        public TerrainMap? LoadedTerrainMap;
        public Material? LoadedTerrainMaterial;

        public TerrainComponent? TerrainComponent;
        public Entity? PaintPreviewEntity;
        public ModelComponent? PaintPreviewModelComponent;
        public float BrushSize;
    }
}

public struct TileCellIndexXZ : IEquatable<TileCellIndexXZ>, IComparable<TileCellIndexXZ>
{
    public int X;
    public int Z;

    public TileCellIndexXZ(int x, int z)
    {
        X = x;
        Z = z;
    }

    public readonly bool Equals(TileCellIndexXZ other)
    {
        return X == other.X && Z == other.Z;
    }

    public override readonly bool Equals([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] object? obj)
    {
        return obj is TileCellIndexXZ cellIndex && Equals(cellIndex);
    }

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(X, Z);
    }

    public readonly int CompareTo(TileCellIndexXZ other)
    {
        if (Z != other.Z)
        {
            return Z.CompareTo(other.Z);
        }
        return X.CompareTo(other.X);
    }

    public static TileCellIndexXZ ToTileCellIndex(Vector3 vector)
    {
        var cellIndex = new TileCellIndexXZ(ToIntFloor(vector.X), ToIntFloor(vector.Z));
        return cellIndex;
    }

    private static int ToIntFloor(float value) => (int)MathF.Floor(value);

    public static bool operator ==(TileCellIndexXZ left, TileCellIndexXZ right) => left.Equals(right);

    public static bool operator !=(TileCellIndexXZ left, TileCellIndexXZ right) => !left.Equals(right);

    public static bool operator <(TileCellIndexXZ left, TileCellIndexXZ right) => left.CompareTo(right) < 0;

    public static bool operator <=(TileCellIndexXZ left, TileCellIndexXZ right) => left.CompareTo(right) <= 0;

    public static bool operator >(TileCellIndexXZ left, TileCellIndexXZ right) => left.CompareTo(right) > 0;

    public static bool operator >=(TileCellIndexXZ left, TileCellIndexXZ right) => left.CompareTo(right) >= 0;
}
#endif
