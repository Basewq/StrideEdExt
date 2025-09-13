using Stride.Engine;
using Stride.Rendering;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace StrideEdExt;

public static class EntityExtensions
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

    public static void SetToEntity(this TransformTRS transformTRS, Entity entity)
    {
        entity.Transform.Position = transformTRS.Position;
        entity.Transform.Rotation = transformTRS.Rotation;
        entity.Transform.Scale = transformTRS.Scale;
    }

    public static bool IsSame(this in TransformTRS transformTRS, in TransformTRS otherTransformTRS)
    {
        bool isSame = transformTRS.Position == otherTransformTRS.Position
            && transformTRS.Rotation == otherTransformTRS.Rotation
            && transformTRS.Scale == otherTransformTRS.Scale;
        return isSame;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetComponent<TComponent>(this Entity entity, [NotNullWhen(true)] out TComponent? component)
    {
        foreach (var entComp in entity.Components)
        {
            if (entComp is TComponent castedComp)
            {
                component = castedComp;
                return true;
            }
        }

        component = default;
        return false;
    }

    public static bool TryFindFirstComponent<TComponent>(this Entity entity, Func<TComponent, bool> isMatchPredicate, [NotNullWhen(true)] out TComponent? component)
    {
        foreach (var entComp in entity.Components)
        {
            if (entComp is TComponent castedComp && isMatchPredicate(castedComp))
            {
                component = castedComp;
                return true;
            }
        }

        component = default;
        return false;
    }

    public static bool TryFindComponentOnAncestor<TComponent>(this Entity entity, [NotNullWhen(true)] out TComponent? component)
    {
        var parentEnt = entity.GetParent();
        while (parentEnt is not null)
        {
            if (TryGetComponent(parentEnt, out component))
            {
                return true;
            }
            parentEnt = parentEnt.GetParent();
        }

        component = default;
        return false;
    }

    /// <summary>
    /// Try get the first <see cref="EntityComponent"/> matching <typeparamref name="TComponent"/> from a descendant of <paramref name="entity"/>.
    /// </summary>
    public static bool TryFindComponentOnDescendant<TComponent>(this Entity entity, [NotNullWhen(true)] out TComponent? component)
    {
        // Depth first search
        foreach (var childTransfComp in entity.Transform.Children)
        {
            if (TryGetComponent(childTransfComp.Entity, out component))
            {
                return true;
            }

            if (TryFindComponentOnDescendant(childTransfComp.Entity, out component))
            {
                return true;
            }
        }
        component = default;
        return false;
    }

    /// <summary>
    /// Try get the first <see cref="EntityComponent"/> matching <typeparamref name="TComponent"/> from a descendant of <paramref name="entity"/>.
    /// </summary>
    public static bool TryFindComponentOnDescendant<TComponent>(this Entity entity, Func<TComponent, bool> isMatchPredicate, [NotNullWhen(true)] out TComponent? component)
    {
        // Depth first search
        foreach (var childTransfComp in entity.Transform.Children)
        {
            if (TryFindFirstComponent(childTransfComp.Entity, isMatchPredicate, out component))
            {
                return true;
            }

            if (TryFindComponentOnDescendant(childTransfComp.Entity, isMatchPredicate, out component))
            {
                return true;
            }
        }
        component = default;
        return false;
    }
}
