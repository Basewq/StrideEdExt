using Stride.Core.Mathematics;

namespace StrideEdExt.Rasterization;

public static class Grid2dLineScanner
{
    public static void ScanLine<TVisitor>(Vector2 point0, Vector2 point1, ref TVisitor visitor)
        where TVisitor : IGrid2dCellTraversalVisitor
    {
        // Code adapted from https://playtechs.blogspot.com/2007/03/raytracing-on-grid.html

        double x0 = point0.X;
        double y0 = point0.Y;
        double x1 = point1.X;
        double y1 = point1.Y;

        double dx = Math.Abs(x1 - x0);
        double dy = Math.Abs(y1 - y0);

        int x = (int)Math.Floor(x0);
        int y = (int)Math.Floor(y0);

        int n = 1;
        int xInc, yInc;
        double error;

        if (dx == 0)
        {
            xInc = 0;
            error = double.PositiveInfinity;
        }
        else if (x1 > x0)
        {
            xInc = 1;
            n += (int)Math.Floor(x1) - x;
            error = (Math.Floor(x0) + 1 - x0) * dy;
        }
        else
        {
            xInc = -1;
            n += x - (int)Math.Floor(x1);
            error = (x0 - Math.Floor(x0)) * dy;
        }

        if (dy == 0)
        {
            yInc = 0;
            error -= double.PositiveInfinity;
        }
        else if (y1 > y0)
        {
            yInc = 1;
            n += (int)Math.Floor(y1) - y;
            error -= (Math.Floor(y0) + 1 - y0) * dx;
        }
        else
        {
            yInc = -1;
            n += y - (int)Math.Floor(y1);
            error -= (y0 - Math.Floor(y0)) * dx;
        }

        for (; n > 0; --n)
        {
            bool canContinue = visitor.Visit(x, y);
            if (!canContinue)
            {
                return;
            }

            if (error > 0)
            {
                y += yInc;
                error -= dx;
            }
            else
            {
                x += xInc;
                error += dy;
            }
        }
    }
}
