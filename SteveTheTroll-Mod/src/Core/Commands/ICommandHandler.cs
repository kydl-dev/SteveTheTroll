using SteveTheTroll.Core.Results;

namespace SteveTheTroll.Core.Commands
{
    /// <summary>Architecture V2 (D-169): handles one command type (validate, decide, hand the decision to an executor) and says how it ended.</summary>
    internal interface ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult>
    {
        OperationResponse<TResult> Handle(TCommand command);
    }
}
