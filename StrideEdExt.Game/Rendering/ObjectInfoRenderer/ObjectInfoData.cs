using Stride.Rendering;

namespace StrideEdExt.Rendering;

// The generated shader key will be public, so this struct must also be public.
public struct ObjectInfoData
{
    public RenderGroup RenderGroup;

    public ObjectInfoData(RenderGroup renderGroup)
    {
        RenderGroup = renderGroup;
    }
}
