﻿namespace ElVladimirReborn
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using LeagueSharp;
    using LeagueSharp.Common;

    internal enum Spells
    {
        Q,

        W,

        E,

        R
    }

    internal static class Vladimir
    {
        #region Static Fields

        public static Orbwalking.Orbwalker Orbwalker;

        public static Dictionary<Spells, Spell> spells = new Dictionary<Spells, Spell>
                                                             {
                                                                 { Spells.Q, new Spell(SpellSlot.Q, 600) },
                                                                 { Spells.W, new Spell(SpellSlot.W) },
                                                                 { Spells.E, new Spell(SpellSlot.E, 600) },
                                                                 { Spells.R, new Spell(SpellSlot.R, 700) }
                                                             };

        private static SpellSlot ignite;

        #endregion

        #region Properties

        private static Obj_AI_Hero Player
        {
            get
            {
                return ObjectManager.Player;
            }
        }

        #endregion

        #region Public Methods and Operators

        public static void OnLoad(EventArgs args)
        {
            if (ObjectManager.Player.CharData.BaseSkinName != "Vladimir")
            {
                return;
            }

            spells[Spells.Q].SetTargetted(0.25f, spells[Spells.Q].Instance.SData.MissileSpeed);
            spells[Spells.R].SetSkillshot(0.25f, 175, 700, false, SkillshotType.SkillshotCircle);

            Notifications.AddNotification("ElVladimirReborn", 1000);
            ignite = Player.GetSpellSlot("summonerdot");

            ElVladimirMenu.Initialize();
            Game.OnUpdate += OnUpdate;
            Drawing.OnDraw += Drawings.OnDraw;
            AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
        }

        #endregion

        #region Methods

        private static void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            var gapCloserActive = ElVladimirMenu.Menu.Item("ElVladimir.Settings.AntiGapCloser.Active").GetValue<bool>();

            if (gapCloserActive && spells[Spells.W].IsReady()
                && gapcloser.Sender.Distance(Player) < spells[Spells.W].Range
                && Player.CountEnemiesInRange(spells[Spells.Q].Range) >= 1)
            {
                spells[Spells.W].Cast(Player);
            }
        }

        private static bool CheckMenu(string menuName)
        {
            return ElVladimirMenu.Menu.Item(menuName).IsActive();
        }

        private static float GetComboDamage(Obj_AI_Base enemy)
        {
            var damage = 0d;

            if (spells[Spells.Q].IsReady())
            {
                damage += Player.GetSpellDamage(enemy, SpellSlot.Q);
            }

            if (spells[Spells.W].IsReady())
            {
                damage += Player.GetSpellDamage(enemy, SpellSlot.W);
            }

            if (spells[Spells.E].IsReady())
            {
                damage += Player.GetSpellDamage(enemy, SpellSlot.E);
            }

            if (spells[Spells.R].IsReady())
            {
                damage += Player.GetSpellDamage(enemy, SpellSlot.R);
            }
            else if (enemy.HasBuff("vladimirhemoplaguedebuff"))
            {
                damage += damage * 1.12;
            }

            return (float)(damage + Player.GetAutoAttackDamage(enemy));
        }

        //Credits to Lizzarin
        private static Tuple<int, List<Obj_AI_Hero>> GetEHits()
        {
            try
            {
                var hits =
                    HeroManager.Enemies.Where(
                        e =>
                        e.IsValidTarget() && e.Distance(Player) < 600f * 0.8f
                        || e.Distance(Player) < 600f && e.IsFacing(Player)).ToList();

                return new Tuple<int, List<Obj_AI_Hero>>(hits.Count, hits);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            return new Tuple<int, List<Obj_AI_Hero>>(0, null);
        }

        private static float IgniteDamage(Obj_AI_Hero target)
        {
            if (ignite == SpellSlot.Unknown || Player.Spellbook.CanUseSpell(ignite) != SpellState.Ready)
            {
                return 0f;
            }
            return (float)Player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite);
        }

        private static void OnAutoHarass()
        {
            var target = TargetSelector.GetTarget(spells[Spells.Q].Range, TargetSelector.DamageType.Magical);
            if (target == null || !target.IsValid)
            {
                return;
            }

            var useQ = ElVladimirMenu.Menu.Item("ElVladimir.AutoHarass.Q").IsActive();
            var useE = ElVladimirMenu.Menu.Item("ElVladimir.AutoHarass.E").IsActive();
            var playerHp = ElVladimirMenu.Menu.Item("ElVladimir.AutoHarass.Health.E").GetValue<Slider>().Value;

            if (spells[Spells.Q].IsReady() && target.IsValidTarget() && useQ)
            {
                spells[Spells.Q].CastOnUnit(target, true);
            }

            if (spells[Spells.E].IsReady() && target.IsValidTarget(spells[Spells.E].Range)
                && (Player.Health / Player.MaxHealth) * 100 >= playerHp && useE)
            {
                if (GetEHits().Item1 > 0)
                {
                    spells[Spells.E].Cast();
                }
            }
        }

        private static BuffInstance GetEBuff()
        {
            try
            {
                return
                    Player.Buffs.FirstOrDefault(
                        b => b.Name.Equals("vladimirtidesofbloodcost", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception e)
            {
                //
            }
            return null;
        }


        private static void OnAutoStack()
        {
            if (Player.IsRecalling() || Player.InFountain() || MenuGUI.IsChatOpen || MenuGUI.IsShopOpen)
            {
                return;
            }

            var buff = GetEBuff();
            if (buff == null || buff.EndTime - Game.Time <= Game.Ping / 2000f + 0.5f)
            {
                spells[Spells.E].Cast();
            }
        }

        private static void OnCombo()
        {
            var target = TargetSelector.GetTarget(spells[Spells.E].Range, TargetSelector.DamageType.Magical);
            if (target == null)
            {
                return;
            }

            var countEnemy = ElVladimirMenu.Menu.Item("ElVladimir.Combo.Count.R").GetValue<Slider>().Value;

            var comboDamage = GetComboDamage(target);

            if (CheckMenu("ElVladimir.Combo.E") && spells[Spells.E].IsReady())
            {
                if (GetEHits().Item1 > 0)
                {
                    spells[Spells.E].Cast();
                }
            }

            if (CheckMenu("ElVladimir.Combo.Q") && spells[Spells.Q].IsReady()
                && target.IsValidTarget(spells[Spells.Q].Range))
            {
                spells[Spells.Q].CastOnUnit(target);
            }

            if (CheckMenu("ElVladimir.Combo.W") && spells[Spells.W].IsReady()
                && target.IsValidTarget(spells[Spells.W].Range))
            {
                spells[Spells.W].Cast();
            }

            if (CheckMenu("ElVladimir.Combo.R.Killable"))
            {
                if (CheckMenu("ElVladimir.Combo.SmartUlt"))
                {
                    var eQDamage = (spells[Spells.Q].GetDamage(target) + spells[Spells.E].GetDamage(target));

                    if (spells[Spells.Q].IsReady() && target.IsValidTarget(spells[Spells.Q].Range)
                        && spells[Spells.Q].GetDamage(target) >= target.Health)
                    {
                        spells[Spells.Q].Cast();
                    }
                    else if (spells[Spells.E].IsReady() && spells[Spells.E].GetDamage(target) >= target.Health)
                    {
                        if (GetEHits().Item1 > 0)
                        {
                            spells[Spells.E].Cast();
                        }
                    }
                    else if (spells[Spells.Q].IsReady() && spells[Spells.E].IsReady()
                             && target.IsValidTarget(spells[Spells.Q].Range) && eQDamage >= target.Health)
                    {
                        spells[Spells.Q].Cast();
                        if (GetEHits().Item1 > 0)
                        {
                            spells[Spells.E].Cast();
                        }
                    }
                    else if (spells[Spells.R].IsReady() && GetComboDamage(target) >= target.Health && !target.IsDead)
                    {
                        spells[Spells.R].Cast(target);
                    }
                }
                else
                {
                    if (CheckMenu("ElVladimir.Combo.R") && comboDamage >= target.Health && !target.IsDead)
                    {
                        spells[Spells.R].Cast(target);
                    }
                }
            }
            else
            {
                if (CheckMenu("ElVladimir.Combo.R") && spells[Spells.R].IsReady())
                {
                    foreach (var x in
                        HeroManager.Enemies.Where((hero => !hero.IsDead && hero.IsValidTarget(spells[Spells.R].Range))))
                    {
                        var pred = spells[Spells.R].GetPrediction(x);
                        if (pred.AoeTargetsHitCount >= countEnemy)
                        {
                            spells[Spells.R].Cast(pred.CastPosition);
                        }
                    }
                }
            }

            if (CheckMenu("ElVladimir.Combo.Ignite") && Player.Distance(target) <= 600
                && IgniteDamage(target) >= target.Health)
            {
                Player.Spellbook.CastSpell(ignite, target);
            }
        }

        private static void OnHarass()
        {
            var target = TargetSelector.GetTarget(spells[Spells.Q].Range, TargetSelector.DamageType.Magical);
            if (target == null)
            {
                return;
            }

            if (CheckMenu("ElVladimir.Harass.Q") && spells[Spells.Q].IsReady()
                && target.IsValidTarget(spells[Spells.Q].Range))
            {
                spells[Spells.Q].CastOnUnit(target);
            }

            if (CheckMenu("ElVladimir.Harass.E") && spells[Spells.E].IsReady()
                && target.IsValidTarget(spells[Spells.E].Range))
            {
                if (GetEHits().Item1 > 0)
                {
                    spells[Spells.E].Cast();
                }
            }
        }

        private static void OnJungleClear()
        {
            var useQ = ElVladimirMenu.Menu.Item("ElVladimir.JungleClear.Q").GetValue<bool>();
            var useE = ElVladimirMenu.Menu.Item("ElVladimir.JungleClear.E").GetValue<bool>();
            var playerHp = ElVladimirMenu.Menu.Item("ElVladimir.WaveClear.Health.E").GetValue<Slider>().Value;

            if (spells[Spells.Q].IsReady() && useQ)
            {
                var allMinions = MinionManager.GetMinions(
                    ObjectManager.Player.ServerPosition,
                    spells[Spells.W].Range,
                    MinionTypes.All,
                    MinionTeam.Neutral,
                    MinionOrderTypes.MaxHealth);
                {
                    foreach (var minion in
                        allMinions.Where(minion => minion.IsValidTarget()))
                    {
                        spells[Spells.Q].CastOnUnit(minion);
                        return;
                    }
                }
            }

            if (spells[Spells.E].IsReady() && (Player.Health / Player.MaxHealth) * 100 >= playerHp && useE)
            {
                var minions = MinionManager.GetMinions(
                    ObjectManager.Player.ServerPosition,
                    spells[Spells.W].Range,
                    MinionTypes.All,
                    MinionTeam.Neutral,
                    MinionOrderTypes.MaxHealth);
                if (minions.Count <= 0)
                {
                    return;
                }

                if (minions.Count > 1)
                {
                    var farmLocation = spells[Spells.E].GetCircularFarmLocation(minions);
                    spells[Spells.E].Cast(farmLocation.Position);
                }
            }
        }

        private static void OnLaneClear()
        {
            var useQ = ElVladimirMenu.Menu.Item("ElVladimir.WaveClear.Q").GetValue<bool>();
            var useE = ElVladimirMenu.Menu.Item("ElVladimir.WaveClear.E").GetValue<bool>();
            var playerHp = ElVladimirMenu.Menu.Item("ElVladimir.WaveClear.Health.E").GetValue<Slider>().Value;

            if (spells[Spells.Q].IsReady() && useQ)
            {
                var allMinions = MinionManager.GetMinions(ObjectManager.Player.ServerPosition, spells[Spells.Q].Range);
                {
                    foreach (var minion in
                        allMinions.Where(
                            minion => minion.Health <= ObjectManager.Player.GetSpellDamage(minion, SpellSlot.Q)))
                    {
                        if (minion.IsValidTarget())
                        {
                            spells[Spells.Q].CastOnUnit(minion);
                            return;
                        }
                    }
                }
            }

            if (spells[Spells.E].IsReady() && (Player.Health / Player.MaxHealth) * 100 >= playerHp && useE)
            {
                var minions = MinionManager.GetMinions(Player.ServerPosition, spells[Spells.E].Range);
                if (minions.Count <= 0)
                {
                    return;
                }

                if (minions.Count > 1)
                {
                    spells[Spells.E].Cast();
                }
            }
        }

        private static void OnUpdate(EventArgs args)
        {
            switch (Orbwalker.ActiveMode)
            {
                case Orbwalking.OrbwalkingMode.Combo:
                    OnCombo();
                    break;

                case Orbwalking.OrbwalkingMode.Mixed:
                    OnHarass();
                    break;

                case Orbwalking.OrbwalkingMode.LaneClear:
                    OnLaneClear();
                    OnJungleClear();
                    break;
            }

            if (ElVladimirMenu.Menu.Item("ElVladimir.AutoHarass.Activated").GetValue<KeyBind>().Active)
            {
                OnAutoHarass();
            }

            if (ElVladimirMenu.Menu.Item("ElVladimir.Settings.Stack.E").GetValue<KeyBind>().Active)
            {
                OnAutoStack();
            }
        }

        #endregion
    }
}