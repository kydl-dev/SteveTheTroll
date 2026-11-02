using SteveTheTroll.Core.Results;

namespace SteveTheTroll.Core.Pipeline
{
    /// <summary>Architecture V2 (D-169): the deciding step of a pipeline: facts in, typed decision out. Pure: no side effects, no Unity / Valheim calls.</summary>
    internal interface IOperationStep<TInput, TOutput>
    {
        OperationResponse<TOutput> Run(TInput input);
    }
}
