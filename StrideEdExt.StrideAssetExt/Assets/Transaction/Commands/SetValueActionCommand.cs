namespace StrideEdExt.StrideAssetExt.Assets.Transaction.Commands;

public class SetValueActionCommand<TRootObject, TValue> : ITransactionCommand
{
    private readonly TRootObject _rootObject;
    private readonly Action<TRootObject, TValue?> _setValueAction;
    private readonly TValue? _oldValue;
    private readonly TValue? _newValue;

    public SetValueActionCommand(TRootObject rootObject, Action<TRootObject, TValue?> setValueAction, TValue? oldValue, TValue? newValue)
    {
        _rootObject = rootObject;
        _setValueAction = setValueAction;
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public void Execute()
    {
        _setValueAction(_rootObject, _newValue!);
    }

    public ITransactionCommand CreateInverse()
    {
        var cmd = new SetValueActionCommand<TRootObject, TValue>(_rootObject, _setValueAction, oldValue: _newValue, newValue: _oldValue);
        return cmd;
    }
}

public static class SetValueActionCommandExtensions
{
    public static SetValueActionCommand<TRootObject, TValue> CreateSetValueCommand<TRootObject, TValue>(
        this TRootObject rootObject, Action<TRootObject, TValue?> setValueAction, TValue? oldValue, TValue? newValue)
    {
        var cmd = new SetValueActionCommand<TRootObject, TValue>(rootObject, setValueAction, oldValue, newValue);
        return cmd;
    }
}
