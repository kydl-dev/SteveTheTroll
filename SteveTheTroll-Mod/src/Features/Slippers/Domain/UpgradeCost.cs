namespace SteveTheTroll.Features.Slippers.Domain
{
    /// <summary>
    /// D-035 (user 2026-10-27): every upgrade of the slippers costs the SAME amount of each upgrade resource (2 TrollHide + 2 Blueberries).
    /// Vanilla Piece.Requirement.GetAmount (1.0.20) multiplies m_amountPerLevel by (quality - 1) up to quality 3 and by 4 + (quality - 4) / 2
    /// from quality 4 on, so the slippers' 4th quality asked for 8. Pure logic (no game types): unit-tested in SteveTheTroll-Mod/tests/Unit.
    /// </summary>
    internal static class UpgradeCost
    {
        /// Amount of one requirement for crafting (quality 1) or upgrading to <paramref name="quality"/> (2+): quality 1 = base amount,
        /// every upgrade = perLevel (+ the base amount when the requirement is an upgrader resource, as vanilla).
        public static int Amount(int quality, int baseAmount, int perLevel, bool upgraderResource)
        {
            if (quality <= 1) return baseAmount;
            int amount = perLevel + (upgraderResource ? baseAmount : 0);
            return amount < 0 ? 0 : amount;
        }
    }
}
