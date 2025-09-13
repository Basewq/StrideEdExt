using Stride.Core.Mathematics;
using StrideEdExt.SharedData.Rendering;
using Half = System.Half;

namespace StrideEdExt.SharedData.Terrain3d.Layers;

public static class TerrainMapLayerExtensions
{
    /// <summary>
    /// Returns the actual region on <paramref name="heightmapTextureSize"/> that can be written into.
    /// </summary>
    public static Rectangle CalculateWritableRegion(
        Rectangle subRegion, Size2 heightmapTextureSize)
    {
        int startIndexX = Math.Max(subRegion.X, 0);
        int startIndexY = Math.Max(subRegion.Y, 0);

        int endIndexXExcl = MathUtil.Clamp(subRegion.X + subRegion.Width, min: 0, max: heightmapTextureSize.Width);
        int endIndexYExcel = MathUtil.Clamp(subRegion.Y + subRegion.Height, min: 0, max: heightmapTextureSize.Height);

        int width = Math.Max(endIndexXExcl - startIndexX, 0);
        int height = Math.Max(endIndexYExcel - startIndexY, 0);
        var writableRegion = new Rectangle(startIndexX, startIndexY, width, height);
        return writableRegion;
    }

    public static void UpdateHeightmapRegion(
        Array2d<float> localRegionData, Int2 startingIndex,
        Array2d<float> outputHeightmapData, Vector2 heightRange,
        TerrainHeightmapLayerBlendType layerBlendType,
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
            outputHeightmapData,
            layerBlendType,
            localRegionDataValueToNormalizedFloatFunc: toNormalizedHeightmapValue,
            isValidLocalRegionDataValuePredicate: x => true);
    }

    public static void UpdateHeightmapRegion(
        Array2d<float?> localRegionData, Int2 startingIndex,
        Array2d<float> outputHeightmapData, Vector2 heightRange,
        TerrainHeightmapLayerBlendType layerBlendType,
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
            outputHeightmapData,
            layerBlendType,
            localRegionDataValueToNormalizedFloatFunc: toNormalizedHeightmapValue,
            isValidLocalRegionDataValuePredicate: x => x.HasValue);
    }

    public static void UpdateHeightmapRegion(
        Array2d<Half> localRegionData, Int2 startingIndex,
        Array2d<float> outputHeightmapData, Vector2 heightRange,
        TerrainHeightmapLayerBlendType layerBlendType,
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
            outputHeightmapData,
            layerBlendType,
            localRegionDataValueToNormalizedFloatFunc: toNormalizedHeightmapValue,
            isValidLocalRegionDataValuePredicate: x => true);
    }

    public static void UpdateHeightmapRegion(
        Array2d<Half?> localRegionData, Int2 startingIndex,
        Array2d<float> outputHeightmapData, Vector2 heightRange,
        TerrainHeightmapLayerBlendType layerBlendType,
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
            outputHeightmapData,
            layerBlendType,
            localRegionDataValueToNormalizedFloatFunc: toNormalizedHeightmapValue,
            isValidLocalRegionDataValuePredicate: x => x.HasValue);
    }

    public static void UpdateHeightmapRegion(
        Array2d<ushort> localRegionData, Int2 startingIndex,
        Array2d<float> outputHeightmapData,
        TerrainHeightmapLayerBlendType layerBlendType)
    {
        UpdateHeightmapRegion(
            localRegionData, startingIndex,
            outputHeightmapData,
            layerBlendType,
            localRegionDataValueToNormalizedFloatFunc: HeightmapTextureHelper.Int16ToNormalizedFloat,
            isValidLocalRegionDataValuePredicate: x => true);
    }

    public static void UpdateHeightmapRegion(
        Array2d<ushort?> localRegionData, Int2 startingIndex,
        Array2d<float> outputHeightmapData,
        TerrainHeightmapLayerBlendType layerBlendType)
    {
        UpdateHeightmapRegion(
            localRegionData, startingIndex,
            outputHeightmapData,
            layerBlendType,
            localRegionDataValueToNormalizedFloatFunc: x => HeightmapTextureHelper.Int16ToNormalizedFloat(x ?? 0),
            isValidLocalRegionDataValuePredicate: x => x.HasValue);
    }

    public static void UpdateHeightmapRegion(
        Array2d<byte> localRegionData, Int2 startingIndex,
        Array2d<float> outputHeightmapData,
        TerrainHeightmapLayerBlendType layerBlendType)
    {
        UpdateHeightmapRegion(
            localRegionData, startingIndex,
            outputHeightmapData,
            layerBlendType,
            localRegionDataValueToNormalizedFloatFunc: HeightmapTextureHelper.ByteToNormalizedFloat,
            isValidLocalRegionDataValuePredicate: x => true);
    }

    public static void UpdateHeightmapRegion(
        Array2d<byte?> localRegionData, Int2 startingIndex,
        Array2d<float> outputHeightmapData,
        TerrainHeightmapLayerBlendType layerBlendType)
    {
        UpdateHeightmapRegion(
            localRegionData, startingIndex,
            outputHeightmapData,
            layerBlendType,
            localRegionDataValueToNormalizedFloatFunc: x => HeightmapTextureHelper.ByteToNormalizedFloat(x ?? 0),
            isValidLocalRegionDataValuePredicate: x => x.HasValue);
    }

    private static void UpdateHeightmapRegion<TLocalData>(
        Array2d<TLocalData> localRegionData, Int2 startingIndex,
        Array2d<float> outputHeightmapData,
        TerrainHeightmapLayerBlendType layerBlendType,
        Func<TLocalData, float> localRegionDataValueToNormalizedFloatFunc,
        Predicate<TLocalData> isValidLocalRegionDataValuePredicate)
    {
        var blendFunc = GetFloatBlendFunction(layerBlendType);
        var expectedWritableRegionRect = new Rectangle(startingIndex.X, startingIndex.Y, localRegionData.LengthX, localRegionData.LengthY);
        var writableRegion = CalculateWritableRegion(expectedWritableRegionRect, outputHeightmapData.Length2d);
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
                var currentTerrainHeightmapValue = outputHeightmapData[terrainMapIndexX, terrainMapIndexY];
                outputHeightmapData[terrainMapIndexX, terrainMapIndexY] = blendFunc(localHeightmapValue, currentTerrainHeightmapValue);
            }
        }
    }

    public static void UpdateMaterialWeightMapRegion(
        Array2d<Half> localRegionData, byte materialIndex, Int2 startingIndex,
        Array2d<Half> currentMaterialWeightMapData, Array2d<byte> outputMaterialIndexMapData)
    {
        var expectedWritableRegionRect = new Rectangle(startingIndex.X, startingIndex.Y, localRegionData.LengthX, localRegionData.LengthY);
        var writableRegion = CalculateWritableRegion(expectedWritableRegionRect, currentMaterialWeightMapData.Length2d);
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
                int localMaterialWeightMapIndexX = terrainMapIndexX - startingIndex.X;
                int localMaterialWeightMapIndexY = terrainMapIndexY - startingIndex.Y;
                var weightValue = localRegionData[localMaterialWeightMapIndexX, localMaterialWeightMapIndexY];
                var existingWeightValue = currentMaterialWeightMapData[terrainMapIndexX, terrainMapIndexY];
                if (weightValue > Half.Zero && existingWeightValue <= weightValue)
                {
                    // Overwrite with our material
                    currentMaterialWeightMapData[terrainMapIndexX, terrainMapIndexY] = (Half)Math.Clamp((float)weightValue, 0, 1);
                    outputMaterialIndexMapData[terrainMapIndexX, terrainMapIndexY] = materialIndex;
                }
            }
        }
    }

    private static readonly Func<float, float, float> FloatBlendFuncAverage = (val1, val2) => Math.Clamp((val1 + val2) * 0.5f, 0, 1);
    private static readonly Func<float, float, float> FloatFuncMinimum = (val1, val2) => (val1 <= val2) ? val1 : val2;
    private static readonly Func<float, float, float> FloatFuncMaximum = (val1, val2) => (val1 >= val2) ? val1 : val2;
    private static Func<float, float, float> GetFloatBlendFunction(TerrainHeightmapLayerBlendType layerBlendType)
    {
        return layerBlendType switch
        {
            TerrainHeightmapLayerBlendType.Minimum => FloatFuncMinimum,
            TerrainHeightmapLayerBlendType.Maximum => FloatFuncMaximum,
            _ => FloatBlendFuncAverage,
        };
    }

    private static readonly Func<Half, Half, Half> HalfBlendFuncAverage = (val1, val2) => (Half)Math.Clamp(((float)val1 + (float)val2) * 0.5f, 0, 1);
    private static readonly Func<Half, Half, Half> HalfFuncMinimum = (val1, val2) => (val1 <= val2) ? val1 : val2;
    private static readonly Func<Half, Half, Half> HalfFuncMaximum = (val1, val2) => (val1 >= val2) ? val1 : val2;
    private static Func<Half, Half, Half> GetHalfBlendFunction(TerrainHeightmapLayerBlendType layerBlendType)
    {
        return layerBlendType switch
        {
            TerrainHeightmapLayerBlendType.Minimum => HalfFuncMinimum,
            TerrainHeightmapLayerBlendType.Maximum => HalfFuncMaximum,
            _ => HalfBlendFuncAverage,
        };
    }
}
