using KydlCore.Common;
using KydlCore.Core;

namespace SteveTheTroll.Features.Trolls.Orders.Commands;

/// <summary>
///     D-170: the one dispatcher of the troll orders (built on first use, every handler registered once). Callers
///     send commands here.
/// </summary>
internal static class TrollOrderService
{
    private static CommandDispatcher _dispatcher;

    internal static CommandDispatcher Dispatcher
    {
        get
        {
            if (_dispatcher != null)
            {
                return _dispatcher;
            }

            var d = new CommandDispatcher(ModLog.Verbose);
            d.Register(new TrollOrderHandlers.FarmFocus());
            d.Register(new TrollOrderHandlers.BreakSource());
            d.Register(new TrollOrderHandlers.BreakWall());
            d.Register(new TrollOrderHandlers.AttackTarget());
            d.Register(new TrollOrderHandlers.SendHome());
            d.Register(new TrollOrderHandlers.Unload());
            _dispatcher = d;
            return _dispatcher;
        }
    }

    /// Expected outcome: true when the order was taken (Success with value true).
    internal static bool Send(ICommand<bool> command)
    {
        OperationResponse<bool> r = Dispatcher.Dispatch(command);
        if (r.Status == ResponseStatus.Failed)
        {
            ModLog.Warn("TrollOrderService: " + (command != null ? command.GetType().Name : "null") + " failed - " +
                        r.Reason);
        }

        return r.IsSuccess && r.Value;
    }
}
