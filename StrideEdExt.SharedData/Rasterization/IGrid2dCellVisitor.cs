namespace StrideEdExt.SharedData.Rasterization;

public interface IGrid2dCellVisitor
{
    void Visit(int x, int y);
}
