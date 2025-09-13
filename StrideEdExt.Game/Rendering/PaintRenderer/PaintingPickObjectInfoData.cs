namespace StrideEdExt.Rendering;

// The generated shader key will be public, so this struct must also be public.
public struct PaintingPickObjectInfoData
{
    public uint IsPickable;

    public PaintingPickObjectInfoData(bool isPickable)
    {
        IsPickable = isPickable ? 1u : 0u;
    }
}
