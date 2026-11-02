using System.Collections.Generic;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>D-088 Costume tab only once all 4 costume items were crafted (known); D-089 which body bones count as feet.</summary>
    [TestFixture]
    public class CostumeUnlockTests
    {
        const string R = "SteveTheTroll.Items.Costume.RecipeUnlockPatch", F = "SteveTheTroll.Items.Costume.CostumeBareFeet";
        static readonly string[] Four = { "$h", "$c", "$l", "$club" };

        static bool All(ICollection<string> items, ICollection<string> known) { return (bool)ModAssembly.Call(R, "AllKnown", items, known); }

        [Test] public void AllFourKnown_Unlocked() { Assert.That(All(Four, new HashSet<string>(Four) { "$other" }), Is.True); }
        [Test] public void ClubMissing_Locked() { Assert.That(All(Four, new HashSet<string> { "$h", "$c", "$l" }), Is.False); }
        [Test] public void NothingKnown_Locked() { Assert.That(All(Four, new HashSet<string>()), Is.False); }
        [Test] public void ItemNotInGame_Locked() { Assert.That(All(new[] { "$h", null }, new HashSet<string> { "$h" }), Is.False); }
        [Test] public void NoItems_Locked() { Assert.That(All(new string[0], new HashSet<string> { "$h" }), Is.False); }
        [Test] public void NullInputs_Locked_NoThrow() { Assert.That(All(null, null), Is.False); Assert.That(All(Four, null), Is.False); }

        [TestCase("LeftFoot", true)]
        [TestCase("RightToeBase", true)]
        [TestCase("RightFoot_end", true)]
        [TestCase("LeftLeg", false)]
        [TestCase("Hips", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void FootBones(string bone, bool expected) { Assert.That((bool)ModAssembly.Call(F, "IsFootBone", bone), Is.EqualTo(expected)); }
    }
}
