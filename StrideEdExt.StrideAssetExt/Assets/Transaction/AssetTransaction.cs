using StrideEdExt.StrideAssetExt.Assets.Transaction.Commands;

namespace StrideEdExt.StrideAssetExt.Assets.Transaction;

public class AssetTransaction
{
    private readonly List<ITransactionCommand> _transactionCommands;
    private readonly List<Action>? _postExecuteActions;

    public AssetTransaction(List<ITransactionCommand> commands, List<Action>? postExecuteActions)
    {
        _transactionCommands = commands;
        _postExecuteActions = postExecuteActions;
    }

    //public void AppendCommand(ITransactionCommand command)
    //{
    //    // HACK: You should be using AssetTransactionBuilder instead of appending commands
    //    // however there are situations where this is the only way due in order to integrate
    //    // with Stride's undo/redo system.
    //    _transactionCommands.Add(command);
    //}

    public void Execute()
    {
        foreach (var cmd in _transactionCommands)
        {
            cmd.Execute();
        }
        if (_postExecuteActions is not null)
        {
            foreach (var postExeAction in _postExecuteActions)
            {
                postExeAction();
            }
        }
    }

    public AssetTransaction CreateInverse()
    {
        var reversedCommands = _transactionCommands.Select(x => x.CreateInverse())
                                .Reverse()
                                .ToList();
        var transaction = new AssetTransaction(reversedCommands, _postExecuteActions);
        return transaction;
    }
}
