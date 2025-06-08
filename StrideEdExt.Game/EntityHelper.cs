using Stride.Engine;
using Stride.Rendering;
using System.Diagnostics.CodeAnalysis;

namespace SceneEditorExtensionExample;

public static class EntityHelper
{
    public static TransformTRS UnsetTransformTRS => new()
    {
        Position = new(float.NaN),
        Rotation = default,
        Scale = new(float.NaN)
    };

    public static TransformTRS GetTransformTRS(this Entity entity)
    {
        var transformTRS = new TransformTRS
        {
            Position = entity.Transform.Position,
            Rotation = entity.Transform.Rotation,
            Scale = entity.Transform.Scale,
        };
        return transformTRS;
    }

    public static bool IsSame(this in TransformTRS transformTRS, in TransformTRS otherTransformTRS)
    {
        bool isSame = transformTRS.Position == otherTransformTRS.Position
            && transformTRS.Rotation == otherTransformTRS.Rotation
            && transformTRS.Scale == otherTransformTRS.Scale;
        return isSame;
    }

    public static bool TryFindComponentOnAncestor<TComponent>(this Entity entity, [NotNullWhen(true)] out TComponent? component)
        where TComponent : EntityComponent
    {
        var parentEnt = entity.GetParent();
        while (parentEnt is not null)
        {
            component = parentEnt.Get<TComponent>();
            if (component is not null)
            {
                return true;
            }
            parentEnt = parentEnt.GetParent();
        }

        component = null;
        return false;
    }
}
