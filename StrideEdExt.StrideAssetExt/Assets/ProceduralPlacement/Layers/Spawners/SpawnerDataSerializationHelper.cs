using Stride.Core.Mathematics;
using StrideEdExt.SharedData.AssetSerialization;
using StrideEdExt.SharedData.ProceduralPlacement.Layers.Spawners;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace StrideEdExt.StrideAssetExt.Assets.ProceduralPlacement.Layers.Spawners;

public static class SpawnerDataSerializationHelper
{
    public delegate bool DeserializeMetadataFunc(StreamReader reader, out int assetRefListCount, out int objectPlacementDataListCount, [NotNullWhen(false)] out string? errorMessage);

    public static void SerializeObjectPlacementsToFile(Action<AssetTextWriter> serializeMetadata, List<ObjectPlacementSpawnPlacementData> objectPlacementDataList, string outputObjectPlacementFilePath)
    {
        using var writer = new AssetTextWriter(outputObjectPlacementFilePath);  // Will overwrite the file if it already exists

        // Write metadata at the start
        serializeMetadata(writer);

        // Write the ObjectPlacementData list
        var objectPlacementDataListSpan = CollectionsMarshal.AsSpan(objectPlacementDataList);
        for (int i = 0; i < objectPlacementDataListSpan.Length; i++)
        {
            ref var objPlacementData = ref objectPlacementDataListSpan[i];

            writer.Write(objPlacementData.AssetUrlListIndex);
            writer.WriteTab();
            writer.WriteTabDelimited(objPlacementData.Position);
            writer.WriteTab();
            var eulerOrientationDegrees = GetEulerOrientationDegrees(objPlacementData.Orientation);
            writer.WriteTabDelimited(eulerOrientationDegrees);
            writer.WriteTab();
            writer.WriteTabDelimited(objPlacementData.Scale);
            writer.WriteTab();
            writer.WriteTabDelimited(objPlacementData.SurfaceNormalModelSpace);
            writer.WriteLine();
        }
    }

    public static void SerializeManualObjectPlacementsToFile(Action<AssetTextWriter> serializeMetadata, List<ObjectPlacementSpawnPlacementData> objectPlacementDataList, string outputObjectPlacementFilePath)
    {
        using var writer = new AssetTextWriter(outputObjectPlacementFilePath);  // Will overwrite the file if it already exists

        // Write metadata at the start
        serializeMetadata(writer);

        // Write the ObjectPlacementData list
        var objectPlacementDataListSpan = CollectionsMarshal.AsSpan(objectPlacementDataList);
        for (int i = 0; i < objectPlacementDataListSpan.Length; i++)
        {
            ref var objPlacementData = ref objectPlacementDataListSpan[i];

            writer.Write(objPlacementData.SpawnInstancingId);
            writer.WriteTab();
            writer.Write(objPlacementData.AssetUrlListIndex);
            writer.WriteTab();
            writer.WriteTabDelimited(objPlacementData.Position);
            writer.WriteTab();
            var eulerOrientationDegrees = GetEulerOrientationDegrees(objPlacementData.Orientation);
            writer.WriteTabDelimited(eulerOrientationDegrees);
            writer.WriteTab();
            writer.WriteTabDelimited(objPlacementData.Scale);
            writer.WriteTab();
            writer.WriteTabDelimited(objPlacementData.SurfaceNormalModelSpace);
            writer.WriteLine();
        }
    }

    private static Vector3 GetEulerOrientationDegrees(Quaternion orientation)
    {
        Quaternion.RotationYawPitchRoll(ref orientation, out float yawRad, out float pitchRad, out float rollRad);

        float yawDeg = MathUtil.RadiansToDegrees(yawRad);
        float pitchDeg = MathUtil.RadiansToDegrees(pitchRad);
        float rollDeg = MathUtil.RadiansToDegrees(rollRad);
        return new Vector3(pitchDeg, yawDeg, rollDeg);
    }

    private static Quaternion GetOrientationFromEulerDegrees(Vector3 eulerOrientationDegrees)
    {
        float yawRad = MathUtil.DegreesToRadians(eulerOrientationDegrees.Y);
        float pitchRad = MathUtil.DegreesToRadians(eulerOrientationDegrees.X);
        float rollRad = MathUtil.DegreesToRadians(eulerOrientationDegrees.Z);
        Quaternion.RotationYawPitchRoll(yawRad, pitchRad, rollRad, out var orientation);
        return orientation;
    }

    public static bool TryDeserializeObjectPlacementsFromFile(
        string outputObjectPlacementFilePath,
        DeserializeMetadataFunc deserializeMetadata,
        [NotNullWhen(true)] out List<ObjectPlacementSpawnPlacementData>? objectPlacementDataList,
        [NotNullWhen(false)] out string? errorMessage)
    {
        //assetRefList = null;
        objectPlacementDataList = null;
        errorMessage = null;

        using var reader = new StreamReader(outputObjectPlacementFilePath);  // Will overwrite the file if it already exists

        // Read metadata at the start
        if (!deserializeMetadata(reader, out int assetRefListCount, out int objectPlacementDataListCount, out errorMessage))
        {
            return false;
        }

        //assetRefList = new(capacity: assetRefListCount);
        objectPlacementDataList = new(capacity: objectPlacementDataListCount);

        // Read the ObjectPlacementData list
        var placementLine = reader.ReadLine();
        while (placementLine is not null)
        {
            if (!TryParsePlacementLine(
                placementLine,
                out ObjectPlacementSpawnPlacementData? objectPlacementData, out errorMessage))
            {
                return false;
            }
            objectPlacementDataList.Add(objectPlacementData);

            placementLine = reader.ReadLine();
        }

        return true;
    }

    private static bool TryParsePlacementLine(
        string placementLine,
        [NotNullWhen(true)] out ObjectPlacementSpawnPlacementData? objectPlacementData,
        [NotNullWhen(false)] out string? errorMessage)
    {
        objectPlacementData = default;

        const int Vec3TokensPerField = 3;
        const int TotalIntFields = 1;
        const int TotalVec3Fields = 4;
        const int TotalTokens = TotalIntFields + (Vec3TokensPerField * TotalVec3Fields);
        Span<Range> tokenRanges = stackalloc Range[TotalTokens];
        var placementLineSpan = placementLine.AsSpan();
        int tokenCount = placementLineSpan.Split(tokenRanges, '\t');
        if (tokenCount < TotalTokens)
        {
            errorMessage = $" Field count mismatched. Expected: {TotalTokens} - Actual: {tokenCount}";
            return false;
        }
        int nextTokenIndex = 0;
        if (!TryReadNextInt(placementLineSpan, tokenRanges, ref nextTokenIndex, out var assetUrlListIndex, out errorMessage))
        {
            return false;
        }
        if (!TryReadNextVector3(placementLineSpan, tokenRanges, ref nextTokenIndex, out var position, out errorMessage))
        {
            return false;
        }
        if (!TryReadNextVector3(placementLineSpan, tokenRanges, ref nextTokenIndex, out var eulerOrientationDegrees, out errorMessage))
        {
            return false;
        }
        if (!TryReadNextVector3(placementLineSpan, tokenRanges, ref nextTokenIndex, out var scale, out errorMessage))
        {
            return false;
        }
        if (!TryReadNextVector3(placementLineSpan, tokenRanges, ref nextTokenIndex, out var surfaceNormal, out errorMessage))
        {
            return false;
        }

        var orientation = GetOrientationFromEulerDegrees(eulerOrientationDegrees);
        objectPlacementData = new ObjectPlacementSpawnPlacementData
        {
            SpawnInstancingId = default,    // Not applicable
            AssetUrlListIndex = assetUrlListIndex,
            Position = position,
            Orientation = orientation,
            Scale = scale,
            SurfaceNormalModelSpace = surfaceNormal,
        };
        return true;
    }

    public static bool TryDeserializeManualObjectPlacementsFromFile(
        string outputObjectPlacementFilePath,
        DeserializeMetadataFunc deserializeMetadata,
        [NotNullWhen(true)] out List<ObjectPlacementSpawnPlacementData>? objectPlacementDataList,
        [NotNullWhen(false)] out string? errorMessage)
    {
        //assetRefList = null;
        objectPlacementDataList = null;
        errorMessage = null;

        using var reader = new StreamReader(outputObjectPlacementFilePath);  // Will overwrite the file if it already exists

        // Read metadata at the start
        if (!deserializeMetadata(reader, out int assetRefListCount, out int objectPlacementDataListCount, out errorMessage))
        {
            return false;
        }

        //assetRefList = new(capacity: assetRefListCount);
        objectPlacementDataList = new(capacity: objectPlacementDataListCount);

        // Read the ObjectPlacementData list
        var placementLine = reader.ReadLine();
        while (placementLine is not null)
        {
            if (!TryParseManualObjectPlacementLine(
                placementLine,
                out var objectPlacementData, out errorMessage))
            {
                return false;
            }
            objectPlacementDataList.Add(objectPlacementData);

            placementLine = reader.ReadLine();
        }

        return true;
    }

    private static bool TryParseManualObjectPlacementLine(
        string placementLine,
        [NotNullWhen(true)] out ObjectPlacementSpawnPlacementData? objectPlacementData,
        [NotNullWhen(false)] out string? errorMessage)
    {
        objectPlacementData = default;

        const int Vec3TokensPerField = 3;
        const int TotalGuidFields = 1;
        const int TotalIntFields = 1;
        const int TotalVec3Fields = 4;
        const int TotalTokens = TotalGuidFields + TotalIntFields + (Vec3TokensPerField * TotalVec3Fields);
        Span<Range> tokenRanges = stackalloc Range[TotalTokens];
        var placementLineSpan = placementLine.AsSpan();
        int tokenCount = placementLineSpan.Split(tokenRanges, '\t');
        if (tokenCount < TotalTokens)
        {
            errorMessage = $" Field count mismatched. Expected: {TotalTokens} - Actual: {tokenCount}";
            return false;
        }
        int nextTokenIndex = 0;
        if (!TryReadNextGuid(placementLineSpan, tokenRanges, ref nextTokenIndex, out var spawnInstancingId, out errorMessage))
        {
            return false;
        }
        if (!TryReadNextInt(placementLineSpan, tokenRanges, ref nextTokenIndex, out var assetUrlListIndex, out errorMessage))
        {
            return false;
        }
        if (!TryReadNextVector3(placementLineSpan, tokenRanges, ref nextTokenIndex, out var position, out errorMessage))
        {
            return false;
        }
        if (!TryReadNextVector3(placementLineSpan, tokenRanges, ref nextTokenIndex, out var eulerOrientationDegrees, out errorMessage))
        {
            return false;
        }
        if (!TryReadNextVector3(placementLineSpan, tokenRanges, ref nextTokenIndex, out var scale, out errorMessage))
        {
            return false;
        }
        if (!TryReadNextVector3(placementLineSpan, tokenRanges, ref nextTokenIndex, out var surfaceNormal, out errorMessage))
        {
            return false;
        }

        var orientation = GetOrientationFromEulerDegrees(eulerOrientationDegrees);
        objectPlacementData = new ObjectPlacementSpawnPlacementData
        {
            SpawnInstancingId = spawnInstancingId,
            AssetUrlListIndex = assetUrlListIndex,
            Position = position,
            Orientation = orientation,
            Scale = scale,
            SurfaceNormalModelSpace = surfaceNormal,
        };
        return true;
    }

    public static bool TryReadNextVector3(
        ReadOnlySpan<char> lineSpan, Span<Range> tokenRanges, ref int nextTokenIndex,
        out Vector3 value, [NotNullWhen(false)] out string? errorMessage)
    {
        if (!TryReadNextFloat(lineSpan, tokenRanges, ref nextTokenIndex, out float x, out errorMessage)
            || !TryReadNextFloat(lineSpan, tokenRanges, ref nextTokenIndex, out float y, out errorMessage)
            || !TryReadNextFloat(lineSpan, tokenRanges, ref nextTokenIndex, out float z, out errorMessage))
        {
            value = Vector3.Zero;
            return false;
        }

        value = new Vector3(x, y, z);
        return true;
    }

    public static bool TryReadNextFloat(
        ReadOnlySpan<char> lineSpan, Span<Range> tokenRanges, ref int nextTokenIndex,
        out float value, [NotNullWhen(false)] out string? errorMessage)
    {
        var tokenRange = tokenRanges[nextTokenIndex];
        nextTokenIndex++;
        int tokenLength = tokenRange.End.Value - tokenRange.Start.Value;
        var tokenSpan = lineSpan.Slice(tokenRange.Start.Value, tokenLength);
        if (!float.TryParse(tokenSpan, out value))
        {
            errorMessage = $"Failed to parse float value from token: '{tokenSpan}'";
            return false;
        }

        errorMessage = null;
        return true;
    }

    public static bool TryReadNextInt(
        ReadOnlySpan<char> lineSpan, Span<Range> tokenRanges, ref int nextTokenIndex,
        out int value, [NotNullWhen(false)] out string? errorMessage)
    {
        var tokenRange = tokenRanges[nextTokenIndex];
        nextTokenIndex++;
        int tokenLength = tokenRange.End.Value - tokenRange.Start.Value;
        var tokenSpan = lineSpan.Slice(tokenRange.Start.Value, tokenLength);
        if (!int.TryParse(tokenSpan, out value))
        {
            errorMessage = $"Failed to parse int value from token: '{tokenSpan}'";
            return false;
        }

        errorMessage = null;
        return true;
    }

    public static bool TryReadNextGuid(
        ReadOnlySpan<char> lineSpan, Span<Range> tokenRanges, ref int nextTokenIndex,
        out Guid value, [NotNullWhen(false)] out string? errorMessage)
    {
        var tokenRange = tokenRanges[nextTokenIndex];
        nextTokenIndex++;
        int tokenLength = tokenRange.End.Value - tokenRange.Start.Value;
        var tokenSpan = lineSpan.Slice(tokenRange.Start.Value, tokenLength);
        if (!Guid.TryParse(tokenSpan, out value))
        {
            errorMessage = $"Failed to parse int value from token: '{tokenSpan}'";
            return false;
        }

        errorMessage = null;
        return true;
    }

    public static bool TryReadNextString(
        ReadOnlySpan<char> lineSpan, Span<Range> tokenRanges, ref int nextTokenIndex,
        [NotNullWhen(true)] out string? value, [NotNullWhen(false)] out string? errorMessage)
    {
        var tokenRange = tokenRanges[nextTokenIndex];
        nextTokenIndex++;
        int tokenLength = tokenRange.End.Value - tokenRange.Start.Value;
        var tokenSpan = lineSpan.Slice(tokenRange.Start.Value, tokenLength);
        value = tokenSpan.ToString();

        errorMessage = null;
        return true;
    }
}
