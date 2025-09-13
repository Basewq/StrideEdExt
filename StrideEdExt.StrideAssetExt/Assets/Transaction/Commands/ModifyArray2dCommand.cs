using Stride.Core.Mathematics;
using StrideEdExt.SharedData;

namespace StrideEdExt.StrideAssetExt.Assets.Transaction.Commands;

public class ModifyArray2dCommand<TValue> : ITransactionCommand
{
    private readonly Array2d<TValue> _array2d;
    private readonly List<ValueChange> _valueChanges = [];

    public ModifyArray2dCommand(Array2d<TValue> array2d)
    {
        _array2d = array2d;
    }

    public void Execute()
    {
        foreach (var valueChange in _valueChanges)
        {
            _array2d[valueChange.Index] = valueChange.NewValue;
        }
    }

    public ITransactionCommand CreateInverse()
    {
        var cmd = new ModifyArray2dCommand<TValue>(_array2d);
        for (int i = _valueChanges.Count - 1; i >= 0; i--)
        {
            var valueChange = _valueChanges[i];
            cmd.AddValueChange(valueChange.Index, previousValue: valueChange.NewValue, newValue: valueChange.PreviousValue);
            _array2d[valueChange.Index] = valueChange.NewValue;
        }
        return cmd;
    }

    public void AddValueChange(Int2 index, TValue previousValue, TValue newValue)
    {
        var valueChange = new ValueChange(index, previousValue, newValue);
        _valueChanges.Add(valueChange);
    }

    private record struct ValueChange(Int2 Index, TValue PreviousValue, TValue NewValue);
}
