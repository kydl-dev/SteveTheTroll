using SteveTheTroll.Features.Trolls.Contracts;

namespace SteveTheTroll.Features.Trolls.Backpack.Infrastructure
{
    /// <summary>D-173 (Architecture V2, P6): ITrollInventory over the troll's ZDO chests (BackpackStore). Reads only; false / true safe defaults without a valid network view.</summary>
    internal sealed class ValheimTrollInventory : ITrollInventory
    {
        readonly ZNetView _view;

        internal ValheimTrollInventory(ZNetView view) { _view = view; }

        public bool IsFull { get { return _view != null && _view.IsValid() && BackpackStore.IsFullCached(_view.GetZDO()); } }
        public bool HasSpace { get { return _view != null && _view.IsValid() && !BackpackStore.IsFullCached(_view.GetZDO()); } }
    }
}
