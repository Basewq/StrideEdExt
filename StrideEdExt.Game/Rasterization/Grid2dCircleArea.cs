using Stride.Core.Mathematics;

namespace StrideEdExt.Rasterization;

public static class Grid2dCircleArea
{
    public static Rectangle GetBounds(Vector2 circleCenterPosition, float radius)
    {
        var cellCenterIndex = MathExt.ToInt2Floor(circleCenterPosition);
        int intRadius = (int)MathF.Floor(radius);
        int intDiameter = (int)MathF.Ceiling((radius + 0.5f) * 2);
        var bounds = new Rectangle(cellCenterIndex.X - intRadius, cellCenterIndex.Y - intRadius, width: intDiameter, height: intDiameter);
        return bounds;
    }

    public static void ScanArea<TVisitor>(Vector2 circleCenterPosition, float radius, ref TVisitor visitor)
        where TVisitor : IGrid2dCellVisitor
    {
        BuildFilledCircleRanges(circleCenterPosition, radius, out int[] cellHalfWidthArrayOutput, out int intDiameter, out Int2 cellCenterIndex);
        var indexOffset = cellCenterIndex;
        indexOffset.Y -= intDiameter / 2;    // int division will truncate
        for (int i = 0; i < intDiameter; i++)
        {
            int y = indexOffset.Y + i;

            int halfWidth = cellHalfWidthArrayOutput[i];
            int startX = indexOffset.X - halfWidth;
            int endX = indexOffset.X + halfWidth;
            for (int x = startX; x <= endX; x++)
            {
                visitor.Visit(x, y);
            }
        }
        //ArrayPool<int>.Shared.Return(cellHalfWidthArrayOutput);
    }

    private static void BuildFilledCircleRanges(
        Vector2 circleCenterPosition, float radius,
        out int[] cellHalfWidthArrayOutput, out int intDiameter, out Int2 cellCenterIndex)
    {
        cellCenterIndex = MathExt.ToInt2Floor(circleCenterPosition);

        int intRadius = (int)MathF.Floor(radius);
        intDiameter = (int)MathF.Ceiling((radius + 0.5f) * 2);
        int cellHalfWidthArrayIndexOffset = intRadius;
        //cellHalfWidthArrayOutput = ArrayPool<int>.Shared.Rent(intDiameter);
        //Array.Clear(cellHalfWidthArrayOutput);
        cellHalfWidthArrayOutput = new int[intDiameter];

        // Use Midpoint Circle Algorithm for to determine the brush's tile indices
        int decisionCriterion = 1 - intRadius;
        int x = 0;
        int y = intRadius;

        do
        {
            // Populate circle cell info
            cellHalfWidthArrayOutput[y + cellHalfWidthArrayIndexOffset] = Math.Max(cellHalfWidthArrayOutput[y + cellHalfWidthArrayIndexOffset], x);
            cellHalfWidthArrayOutput[-y + cellHalfWidthArrayIndexOffset] = Math.Max(cellHalfWidthArrayOutput[-y + cellHalfWidthArrayIndexOffset], x);

            cellHalfWidthArrayOutput[x + cellHalfWidthArrayIndexOffset] = Math.Max(cellHalfWidthArrayOutput[x + cellHalfWidthArrayIndexOffset], y);
            cellHalfWidthArrayOutput[-x + cellHalfWidthArrayIndexOffset] = Math.Max(cellHalfWidthArrayOutput[-x + cellHalfWidthArrayIndexOffset], y);

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
    }
}
