namespace SteveTheTroll.Trolls.Combat.Infrastructure
{
    /// <summary>The troll's melee attacks (punch / log swing, never the throw): the one of the wanted swing direction first.</summary>
    internal static class TrollWeapons
    {
        /// Expected outcome: the melee item swinging `want` (else any horizontal / vertical melee item, else any melee item), or null.
        /// `tool` = only items with chop or pickaxe damage (farming).
        internal static ItemDrop.ItemData Pick(Humanoid me, Attack.AttackType want, bool tool)
        {
            ItemDrop.ItemData best = null;
            if (me == null) return null;
            foreach (var it in me.GetInventory().GetAllItems())
            {
                var s = it.m_shared;
                if (s.m_attack == null || (tool && s.m_damages.m_chop + s.m_damages.m_pickaxe <= 0f)) continue;
                var type = s.m_attack.m_attackType;
                if (type == Attack.AttackType.Projectile || type == Attack.AttackType.TriggerProjectile) continue;
                if (best == null || type == want || (best.m_shared.m_attack.m_attackType != want && (type == Attack.AttackType.Horizontal || type == Attack.AttackType.Vertical))) best = it;
            }
            return best;
        }

        /// Steve (the log / club troll). Expected outcome: true when it carries a troll_log item.
        internal static bool HasClub(Humanoid me)
        {
            if (me == null) return false;
            foreach (var it in me.GetInventory().GetAllItems()) if (it.m_dropPrefab != null && it.m_dropPrefab.name.StartsWith("troll_log")) return true;
            return false;
        }
    }
}
