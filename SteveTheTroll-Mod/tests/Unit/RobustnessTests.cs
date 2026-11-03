using System;
using FsCheck;
using SteveTheTroll.Ui;

namespace SteveTheTroll.Tests.Unit
{
    /// <summary>
    /// D-086 (user: "Are exceptions asserted and handled by tests?"): the pure code reads saved / synced text and game numbers. It must never
    /// throw on bad data - a throw inside an Update tick used to stop every later tick (TrollZones, TEST-037). Property tests: hundreds of
    /// generated inputs per rule, the smallest failing input is printed.
    /// </summary>
    [TestFixture]
    public class RobustnessTests
    {
        const string Rules = "SteveTheTroll.Trolls.Taming.Domain.TamingRules";
        static string K(string key) { return key; }

        [Test]
        public void TrollInfoDecode_AnyText_NullOrRecord_NeverThrows()
        {
            var gen = Props.TextFrom("|||~=;,.-+eE0123456789abcXYZ \t", 160);
            Props.Check(Prop.ForAll(gen.ToArbitrary(), s => { var t = TrollInfo.Decode(s); return t == null || !string.IsNullOrEmpty(t.Id); }));
        }

        [Test]
        public void TrollInfo_RoundTrip_SurvivesSeparatorsInNames()
        {
            var gen = Props.TextFrom("|\r\nabc ~;=", 30);
            Props.Check(Prop.ForAll(gen.ToArbitrary(), name =>
            {
                var t = new TrollInfo { Id = "id1", Name = name, Status = "s", Focus = "", Farming = "", MaxHealth = 1f, Attacks = "", Resists = "" };
                var back = TrollInfo.Decode(t.Encode());
                return back != null && back.Id == "id1" && back.Name.IndexOf('|') < 0;
            }));
        }

        [Test]
        public void TrollAttacks_AnyText_NeverThrows()
        {
            var gen = Props.TextFrom("~=;,.-0123456789PunchBlt ", 120);
            Props.Check(Prop.ForAll(gen.ToArbitrary(), s => TrollDetailsBuilder.Attacks(s, K) != null));
            Assert.That(TrollDetailsBuilder.Attacks(null, K), Is.Not.Null);
        }

        [Test]
        public void TrollDetails_NullRecord_NeverThrows()
        {
            Assert.DoesNotThrow(() => TrollDetailsBuilder.Build(null, K, 0L));
            Assert.DoesNotThrow(() => TrollDetailsBuilder.Ago(long.MinValue, K));
            Assert.DoesNotThrow(() => TrollDetailsBuilder.Ago(long.MaxValue, K));
        }

        [Test]
        public void BedHeal_AnyNumbers_NeverNegative_NeverAboveMax()
        {
            var f = Props.GameFloat(10000f);
            var gen = Gen.Four(f).Select(t => new[] { t.Item1, t.Item2, t.Item3, t.Item4 });
            Props.Check(Prop.ForAll(gen.ToArbitrary(), a =>
            {
                float add = (float)ModAssembly.Call(Rules, "BedHeal", a[0], a[1], a[2], a[3]);
                return add >= 0f && (add == 0f || a[0] + add <= a[1] + 1e-3f);
            }));
        }

        [Test]
        public void SpawnCap_AnyInput_OnlyTwoSixOrNone()
        {
            var gen = Gen.Choose(-3, 12).SelectMany(n => Props.GameFloat(2000f).Select(d => Tuple.Create(n, d)));
            Props.Check(Prop.ForAll(gen.ToArbitrary(), x =>
            {
                foreach (bool slippers in new[] { false, true })
                    foreach (bool set in new[] { false, true })
                    {
                        int cap = (int)ModAssembly.Call(Rules, "SpawnCap", slippers, set, x.Item1, x.Item2, 500f);
                        if (cap != 0 && cap != 2 && cap != 6) return false;
                        if (!slippers && cap != 0) return false;
                    }
                return true;
            }));
        }

        [Test]
        public void TrollFocus_AnyStoredValue_NeverThrows()
        {
            var gen = Props.TextFrom("WoodTinOre|; é", 20);
            Props.Check(Prop.ForAll(gen.ToArbitrary(), s => TrollFocus.KeyOf(s) != null && TrollFocus.Shown(s, K) != null && TrollFocus.SearchOrder(s).Count > 0));
        }
    }
}
