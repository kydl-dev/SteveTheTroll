

namespace SteveTheTroll.Tests.Unit;

/// <summary>T-012 (user 2026-10-28): tamed troll speed (D-111), set carry weight as a percentage (D-112).</summary>
[TestFixture]
public class PreReleaseChangesTests
{
    const string Rules = "SteveTheTroll.Features.Trolls.Taming.Domain.TamingRules";

    static float Free(float game, float factor) { return (float)ModAssembly.Call(Rules, "FreeAnimSpeed", game, factor); }
    static float Attack(float scale, float factor) { return (float)ModAssembly.Call(Rules, "AttackAnimSpeed", scale, factor); }

    [Test] public void FreeAnim_GameResetToOne_BecomesTheFactor() { Assert.That(Free(1f, 1.25f), Is.EqualTo(1.25f)); }
    [Test] public void FreeAnim_FreezeFrame_IsKept() { Assert.That(Free(0.0001f, 1.25f), Is.EqualTo(0.0001f)); }
    [Test] public void FreeAnim_AlreadyScaled_IsKept() { Assert.That(Free(1.25f, 1.25f), Is.EqualTo(1.25f)); }
    [Test] public void FreeAnim_OtherSpeed_IsKept() { Assert.That(Free(0.8f, 1.25f), Is.EqualTo(0.8f)); }
    [Test] public void FreeAnim_NoFactor_IsKept() { Assert.That(Free(1f, 0f), Is.EqualTo(1f)); }
    [Test] public void FreeAnim_JustAboveFreeze_IsScaledOnlyAtOne() { Assert.That(Free(0.01f, 1.25f), Is.EqualTo(0.01f)); }

    [Test] public void AttackAnim_ScaleTimesFactor() { Assert.That(Attack(1.6f, 1.25f), Is.EqualTo(2f).Within(1e-5)); }
    [Test] public void AttackAnim_NoFactor_ClipScale() { Assert.That(Attack(1.6f, 0f), Is.EqualTo(1.6f)); }

    static float Carry(float limit, float percent) { return (float)ModAssembly.Call("SteveTheTroll.Features.Costume.Infrastructure.SetCarryPatch", "WithSet", limit, percent); }

    [Test] public void Carry_Base300_Plus25Percent_Is375() { Assert.That(Carry(300f, 0.25f), Is.EqualTo(375f).Within(1e-3)); }

    [Test]
    public void Carry_WithMegingjord_GainsMoreThan75()
    {
        float total = 300f + 150f;                       // base + Megingjord
        Assert.That(Carry(total, 0.25f) - total, Is.EqualTo(112.5f).Within(1e-3));
    }

    [Test] public void Carry_NoPercent_Unchanged() { Assert.That(Carry(450f, 0f), Is.EqualTo(450f)); }

    [Test]
    public void Tooltip_GetsTheCarryLine()
    {
        var r = (string)ModAssembly.Call("SteveTheTroll.Features.Costume.Infrastructure.SetTooltipPatch", "WithPercent", "line one\n", "$stevethetroll_se_carry", 0.25f);
        Assert.That(r, Is.EqualTo("line one\n$stevethetroll_se_carry: <color=orange>+25%</color>"));
    }

    [Test]
    public void Tooltip_Empty_IsOnlyTheLine()
    {
        var r = (string)ModAssembly.Call("SteveTheTroll.Features.Costume.Infrastructure.SetTooltipPatch", "WithPercent", "", "X", 0.25f);
        Assert.That(r, Is.EqualTo("X: <color=orange>+25%</color>"));
    }
}
