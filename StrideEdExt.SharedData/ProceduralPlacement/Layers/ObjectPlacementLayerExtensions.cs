using Stride.Core.Mathematics;
using StrideEdExt.SharedData.Rendering;
using Half = System.Half;

namespace StrideEdExt.SharedData.ProceduralPlacement.Layers;

public static class ObjectPlacementLayerExtensions
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

    public static void UpdateDensityMapRegion(
        Array2d<float> localRegionData, Int2 startingIndex,
        Array2d<float> outputDensityMapData, Vector2 heightRange,
        bool isLocalRegionDataNormalized)
    {
        Func<float, float> toNormalizedDensityMapValue;
        if (isLocalRegionDataNormalized)
        {
            toNormalizedDensityMapValue = x => x;
        }
        else
        {
            toNormalizedDensityMapValue = x => Math.Clamp(MathUtil.InverseLerp(heightRange.X, heightRange.Y, x), 0, 1);
        }
        UpdateDensityMapRegion(
            localRegionData, startingIndex,
            outputDensityMapData,
            localRegionDataValueToNormalizedFloatFunc: toNormalizedDensityMapValue,
            isValidLocalRegionDataValuePredicate: x => true);
    }

    public static void UpdateDensityMapRegion(
        Array2d<float?> localRegionData, Int2 startingIndex,
        Array2d<float> outputDensityMapData, Vector2 heightRange,
        bool isLocalRegionDataNormalized)
    {
        Func<float?, float> toNormalizedDensityMapValue;
        if (isLocalRegionDataNormalized)
        {
            toNormalizedDensityMapValue = x => x ?? 0;
        }
        else
        {
            toNormalizedDensityMapValue = x => Math.Clamp(MathUtil.InverseLerp(heightRange.X, heightRange.Y, x ?? 0), 0, 1);
        }
        UpdateDensityMapRegion(
            localRegionData, startingIndex,
            outputDensityMapData,
            localRegionDataValueToNormalizedFloatFunc: toNormalizedDensityMapValue,
            isValidLocalRegionDataValuePredicate: x => x.HasValue);
    }

    public static void UpdateDensityMapRegion(
        Array2d<Half> localRegionData, Int2 startingIndex,
        Array2d<float> outputDensityMapData, Vector2 heightRange,
        bool isLocalRegionDataNormalized)
    {
        Func<Half, float> toNormalizedDensityMapValue;
        if (isLocalRegionDataNormalized)
        {
            toNormalizedDensityMapValue = x => (float)x;
        }
        else
        {
            toNormalizedDensityMapValue = x => Math.Clamp(MathUtil.InverseLerp(heightRange.X, heightRange.Y, (float)x), 0, 1);
        }
        UpdateDensityMapRegion(
            localRegionData, startingIndex,
            outputDensityMapData,
            localRegionDataValueToNormalizedFloatFunc: toNormalizedDensityMapValue,
            isValidLocalRegionDataValuePredicate: x => true);
    }

    public static void UpdateDensityMapRegion(
        Array2d<Half?> localRegionData, Int2 startingIndex,
        Array2d<float> outputDensityMapData, Vector2 heightRange,
        bool isLocalRegionDataNormalized)
    {
        Func<Half?, float> toNormalizedDensityMapValue;
        if (isLocalRegionDataNormalized)
        {
            toNormalizedDensityMapValue = x => (float)(x ?? Half.Zero);
        }
        else
        {
            toNormalizedDensityMapValue = x => Math.Clamp(MathUtil.InverseLerp(heightRange.X, heightRange.Y, (float)(x ?? Half.Zero)), 0, 1);
        }
        UpdateDensityMapRegion(
            localRegionData, startingIndex,
            outputDensityMapData,
            localRegionDataValueToNormalizedFloatFunc: toNormalizedDensityMapValue,
            isValidLocalRegionDataValuePredicate: x => x.HasValue);
    }

    public static void UpdateDensityMapRegion(
        Array2d<ushort> localRegionData, Int2 startingIndex,
        Array2d<float> outputDensityMapData)
    {
        UpdateDensityMapRegion(
            localRegionData, startingIndex,
            outputDensityMapData,
            localRegionDataValueToNormalizedFloatFunc: HeightmapTextureHelper.Int16ToNormalizedFloat,
            isValidLocalRegionDataValuePredicate: x => true);
    }

    public static void UpdateDensityMapRegion(
        Array2d<ushort?> localRegionData, Int2 startingIndex,
        Array2d<float> outputDensityMapData)
    {
        UpdateDensityMapRegion(
            localRegionData, startingIndex,
            outputDensityMapData,
            localRegionDataValueToNormalizedFloatFunc: x => HeightmapTextureHelper.Int16ToNormalizedFloat(x ?? 0),
            isValidLocalRegionDataValuePredicate: x => x.HasValue);
    }

    public static void UpdateDensityMapRegion(
        Array2d<byte> localRegionData, Int2 startingIndex,
        Array2d<float> outputDensityMapData)
    {
        UpdateDensityMapRegion(
            localRegionData, startingIndex,
            outputDensityMapData,
            localRegionDataValueToNormalizedFloatFunc: HeightmapTextureHelper.ByteToNormalizedFloat,
            isValidLocalRegionDataValuePredicate: x => true);
    }

    public static void UpdateDensityMapRegion(
        Array2d<byte?> localRegionData, Int2 startingIndex,
        Array2d<float> outputDensityMapData)
    {
        UpdateDensityMapRegion(
            localRegionData, startingIndex,
            outputDensityMapData,
            localRegionDataValueToNormalizedFloatFunc: x => HeightmapTextureHelper.ByteToNormalizedFloat(x ?? 0),
            isValidLocalRegionDataValuePredicate: x => x.HasValue);
    }

    private static void UpdateDensityMapRegion<TLocalData>(
        Array2d<TLocalData> localRegionData, Int2 startingIndex,
        Array2d<float> outputDensityMapData,
        Func<TLocalData, float> localRegionDataValueToNormalizedFloatFunc,
        Predicate<TLocalData> isValidLocalRegionDataValuePredicate)
    {
        var expectedWritableRegionRect = new Rectangle(startingIndex.X, startingIndex.Y, localRegionData.LengthX, localRegionData.LengthY);
        var writableRegion = CalculateWritableRegion(expectedWritableRegionRect, outputDensityMapData.Length2d);
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
                int localDensityMapIndexX = terrainMapIndexX - startingIndex.X;
                int localDensityMapIndexY = terrainMapIndexY - startingIndex.Y;
                var dataValue = localRegionData[localDensityMapIndexX, localDensityMapIndexY];
                if (!isValidLocalRegionDataValuePredicate(dataValue))
                {
                    continue;
                }
                float localDensityMapValue = localRegionDataValueToNormalizedFloatFunc(dataValue);
                var currentTerrainDensityMapValue = outputDensityMapData[terrainMapIndexX, terrainMapIndexY];
                outputDensityMapData[terrainMapIndexX, terrainMapIndexY] = Math.Max(localDensityMapValue, currentTerrainDensityMapValue);
            }
        }
    }

    //public static void UpdateDensityMapRegion(
    //    Array2d<Half> localRegionData, byte materialIndex, Int2 startingIndex,
    //    Array2d<Half> currentDensityMapData, Array2d<byte> outputMaterialIndexMapData)
    //{
    //    var expectedWritableRegionRect = new Rectangle(startingIndex.X, startingIndex.Y, localRegionData.LengthX, localRegionData.LengthY);
    //    var writableRegion = CalculateWritableRegion(expectedWritableRegionRect, currentDensityMapData.Length2d);
    //    if (writableRegion.Height == 0 || writableRegion.Width == 0)
    //    {
    //        return;
    //    }
    //    for (int y = 0; y < writableRegion.Height; y++)
    //    {
    //        for (int x = 0; x < writableRegion.Width; x++)
    //        {
    //            int terrainMapIndexX = x + writableRegion.X;
    //            int terrainMapIndexY = y + writableRegion.Y;
    //            int localDensityMapIndexX = terrainMapIndexX - startingIndex.X;
    //            int localDensityMapIndexY = terrainMapIndexY - startingIndex.Y;
    //            var weightValue = localRegionData[localDensityMapIndexX, localDensityMapIndexY];
    //            var existingWeightValue = currentDensityMapData[terrainMapIndexX, terrainMapIndexY];
    //            if (weightValue > Half.Zero && existingWeightValue <= weightValue)
    //            {
    //                // Overwrite with our material
    //                currentDensityMapData[terrainMapIndexX, terrainMapIndexY] = (Half)Math.Clamp((float)weightValue, 0, 1);
    //                outputMaterialIndexMapData[terrainMapIndexX, terrainMapIndexY] = materialIndex;
    //            }
    //        }
    //    }
    //}
}
