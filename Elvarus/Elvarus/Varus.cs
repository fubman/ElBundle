namespace Elvarus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using LeagueSharp;
    using LeagueSharp.Common;

    using ItemData = LeagueSharp.Common.Data.ItemData;

    internal enum Spells
    {
        Q,

        W,

        E,

        R
    }

    internal class Varus
    {
        #region Static Fields

        public static Orbwalking.Orbwalker Orbwalker;

        public static Dictionary<Spells, Spell> spells = new Dictionary<Spells, Spell>
                                                             {
                                                                 { Spells.Q, new Spell(SpellSlot.Q, 925) },
                                                                 { Spells.W, new Spell(SpellSlot.W, 0) },
                                                                 { Spells.E, new Spell(SpellSlot.E, 925) },
                                                                 { Spells.R, new Spell(SpellSlot.R, 1100) }
                                                             };

        #endregion

        #region Public Properties

        public static Obj_AI_Hero Player
        {
            get
            {
                return ObjectManager.Player;
            }
        }

        #endregion

        #region Public Methods and Operators

        public static void Game_OnGameLoad(EventArgs args)
        {
            if (ObjectManager.Player.ChampionName != "Varus")
            {
                return;
            }

            spells[Spells.Q].SetSkillshot(.25f, 70f, 1650f, false, SkillshotType.SkillshotLine);
            spells[Spells.E].SetSkillshot(0.35f, 120, 1500, false, SkillshotType.SkillshotCircle);
            spells[Spells.R].SetSkillshot(.25f, 120f, 1950f, false, SkillshotType.SkillshotLine);

            spells[Spells.Q].SetCharged("VarusQ", "VarusQ", 250, 1600, 1.2f);

            ElVarusMenu.Initialize();
            Game.OnUpdate += OnGameUpdate;
            Drawing.OnDraw += Drawings.Drawing_OnDraw;
        }

        #endregion

        #region Methods

        private static void Combo()
        {
            var wTarget =
                HeroManager.Enemies.Find(
                    x => x.HasBuff("varuswdebuff") && x.IsValidTarget(Orbwalking.GetRealAutoAttackRange(Player)));
            var target = wTarget
                         ?? TargetSelector.GetTarget(
                             spells[Spells.Q].ChargedMaxRange,
                             TargetSelector.DamageType.Physical);

            if (target == null || !target.IsValidTarget())
            {
                return;
            }
            if (wTarget != null && ElVarusMenu.Menu.Item("ElVarus.Combo.W.Focus").GetValue<bool>())
            {
                TargetSelector.SetTarget(target);
                Hud.SelectedUnit = target;
            }

            var stackCount = ElVarusMenu.Menu.Item("ElVarus.Combo.Stack.Count").GetValue<Slider>().Value;
            var rCount = ElVarusMenu.Menu.Item("ElVarus.Combo.R.Count").GetValue<Slider>().Value;
            var comboQ = ElVarusMenu.Menu.Item("ElVarus.Combo.Q").IsActive();
            var comboE = ElVarusMenu.Menu.Item("ElVarus.Combo.E").IsActive();
            var comboR = ElVarusMenu.Menu.Item("ElVarus.Combo.R").IsActive();
            var alwaysQ = ElVarusMenu.Menu.Item("ElVarus.combo.always.Q").IsActive();

            if (comboE && spells[Spells.E].IsReady() && target.IsValidTarget(spells[Spells.E].Range))
            {
                spells[Spells.E].Cast(target);
            }

            Items(target);

            if (spells[Spells.Q].IsReady() && comboQ && target.IsValidTarget(spells[Spells.Q].ChargedMaxRange))
            {
                if (alwaysQ)
                {
                    spells[Spells.Q].StartCharging();
                }
                else if (spells[Spells.W].Level == 0 || GetStacksOn(target) >= stackCount
                         || spells[Spells.Q].GetDamage(target) > target.Health)
                {
                    spells[Spells.Q].StartCharging();
                }

                if (spells[Spells.Q].IsCharging)
                {
                    spells[Spells.Q].Cast(target);
                }
            }

            if (IsQKillable(target))
            {
                if (!spells[Spells.Q].IsCharging)
                {
                    spells[Spells.Q].StartCharging();
                }

                if (spells[Spells.Q].IsCharging)
                {
                    spells[Spells.Q].Cast(target);
                }
            }

            if (comboR && Player.CountEnemiesInRange(spells[Spells.R].Range) >= rCount && spells[Spells.R].IsReady()
                && target.IsValidTarget(spells[Spells.R].Range))
            {
                spells[Spells.R].CastOnBestTarget();
            }
        }

        private static double GetExecuteDamage(Obj_AI_Base target)
        {
            if (spells[Spells.Q].IsReady())
            {
                return (spells[Spells.Q].GetDamage(target)) + (Player.TotalAttackDamage());
            }

            return 0;
        }

        private static float GetHealth(Obj_AI_Base target)
        {
            return target.Health;
        }

        private static int GetStacksOn(Obj_AI_Base target)
        {
            return
                target.Buffs.Where(
                    xBuff => xBuff.Name == "varuswdebuff" && target.IsValidTarget(spells[Spells.Q].ChargedMaxRange))
                    .Select(xBuff => xBuff.Count)
                    .FirstOrDefault();
        }

        private static void Harass()
        {
            var target = TargetSelector.GetTarget(spells[Spells.Q].ChargedMaxRange, TargetSelector.DamageType.Physical);
            if (target == null || !target.IsValidTarget())
            {
                return;
            }

            var harassQ = ElVarusMenu.Menu.Item("ElVarus.Harass.Q").GetValue<bool>();
            var harassE = ElVarusMenu.Menu.Item("ElVarus.Harass.E").GetValue<bool>();
            var minmana = ElVarusMenu.Menu.Item("minmanaharass").GetValue<Slider>().Value;

            if (Player.ManaPercent > minmana)
            {
                if (harassE && spells[Spells.E].IsReady())
                {
                    spells[Spells.E].CastOnBestTarget();
                }

                if (harassQ)
                {
                    if (!spells[Spells.Q].IsCharging)
                    {
                        spells[Spells.Q].StartCharging();
                    }

                    if (spells[Spells.Q].IsReady())
                    {
                        var prediction = spells[Spells.Q].GetPrediction(target);
                        var distance =
                            Player.ServerPosition.Distance(
                                prediction.UnitPosition
                                + 200 * (prediction.UnitPosition - Player.ServerPosition).Normalized(),
                                true);
                        if (distance < spells[Spells.Q].RangeSqr)
                        {
                            if (spells[Spells.Q].Cast(prediction.CastPosition))
                            {
                            }
                        }
                    }
                }
            }
        }

        private static bool IsQKillable(Obj_AI_Base target)
        {
            var hero = target as Obj_AI_Hero;
            return GetExecuteDamage(target) > GetHealth(target) && (hero == null);
        }

        private static void Items(Obj_AI_Base target)
        {
            var botrk = ItemData.Blade_of_the_Ruined_King.GetItem();
            var ghost = ItemData.Youmuus_Ghostblade.GetItem();
            var cutlass = ItemData.Bilgewater_Cutlass.GetItem();

            var useYoumuu = ElVarusMenu.Menu.Item("ElVarus.Items.Youmuu").GetValue<bool>();
            var useCutlass = ElVarusMenu.Menu.Item("ElVarus.Items.Cutlass").GetValue<bool>();
            var useBlade = ElVarusMenu.Menu.Item("ElVarus.Items.Blade").GetValue<bool>();

            var useBladeEhp = ElVarusMenu.Menu.Item("ElVarus.Items.Blade.EnemyEHP").GetValue<Slider>().Value;
            var useBladeMhp = ElVarusMenu.Menu.Item("ElVarus.Items.Blade.EnemyMHP").GetValue<Slider>().Value;

            if (botrk.IsReady() && botrk.IsOwned(Player) && botrk.IsInRange(target)
                && target.HealthPercent <= useBladeEhp && useBlade)
            {
                botrk.Cast(target);
            }

            if (botrk.IsReady() && botrk.IsOwned(Player) && botrk.IsInRange(target)
                && Player.HealthPercent <= useBladeMhp && useBlade)
            {
                botrk.Cast(target);
            }

            if (cutlass.IsReady() && cutlass.IsOwned(Player) && cutlass.IsInRange(target)
                && target.HealthPercent <= useBladeEhp && useCutlass)
            {
                cutlass.Cast(target);
            }

            if (ghost.IsReady() && ghost.IsOwned(Player) && target.IsValidTarget(spells[Spells.Q].Range) && useYoumuu)
            {
                ghost.Cast();
            }
        }

        private static void JungleClear()
        {
            var useQ = ElVarusMenu.Menu.Item("useQFarmJungle").GetValue<bool>();
            var useE = ElVarusMenu.Menu.Item("useEFarmJungle").GetValue<bool>();
            var minmana = ElVarusMenu.Menu.Item("minmanaclear").GetValue<Slider>().Value;
            var minions = MinionManager.GetMinions(
                ObjectManager.Player.ServerPosition,
                700,
                MinionTypes.All,
                MinionTeam.Neutral,
                MinionOrderTypes.MaxHealth);

            if (Player.ManaPercent >= minmana)
            {
                foreach (var minion in minions)
                {
                    if (spells[Spells.Q].IsReady() && useQ)
                    {
                        if (!spells[Spells.Q].IsCharging)
                        {
                            spells[Spells.Q].StartCharging();
                        }
                        else
                        {
                            spells[Spells.Q].CastOnUnit(minion);
                        }
                    }

                    if (spells[Spells.E].IsReady() && useE)
                    {
                        spells[Spells.E].CastOnUnit(minion);
                    }
                }
            }
        }

        //Credits to God :cat_lazy:
        private static void Killsteal()
        {
            if (ElVarusMenu.Menu.Item("ElVarus.KSSS").GetValue<bool>() && spells[Spells.Q].IsReady())
            {
                foreach (var target in
                    HeroManager.Enemies.Where(
                        enemy =>
                        enemy.IsValidTarget() && IsQKillable(enemy)
                        && Player.Distance(enemy.Position) <= spells[Spells.Q].ChargedMaxRange))
                {
                    spells[Spells.Q].StartCharging();

                    if (spells[Spells.Q].IsCharging)
                    {
                        Orbwalker.SetAttack(false);
                        if (IsQKillable(target) && !target.IsInvulnerable)
                        {
                            spells[Spells.Q].Cast(target);
                        }
                    }
                }
            }
        }

        private static void LaneClear()
        {
            var useQ = ElVarusMenu.Menu.Item("useQFarm").GetValue<bool>();
            var useE = ElVarusMenu.Menu.Item("useQFarm").GetValue<bool>();
            var countMinions = ElVarusMenu.Menu.Item("ElVarus.Count.Minions").GetValue<Slider>().Value;
            var countMinionsE = ElVarusMenu.Menu.Item("ElVarus.Count.Minions.E").GetValue<Slider>().Value;
            var minmana = ElVarusMenu.Menu.Item("minmanaclear").GetValue<Slider>().Value;

            if (Player.ManaPercent < minmana)
            {
                return;
            }

            var minions = MinionManager.GetMinions(Player.ServerPosition, spells[Spells.Q].Range);
            if (minions.Count <= 0)
            {
                return;
            }

            if (spells[Spells.Q].IsReady() && useQ)
            {
                var allMinions = MinionManager.GetMinions(Player.ServerPosition, spells[Spells.Q].Range);
                {
                    foreach (var minion in
                        allMinions.Where(
                            minion => minion.Health <= Player.GetSpellDamage(minion, SpellSlot.Q)))
                    {
                        var killcount = 0;

                        foreach (var colminion in minions)
                        {
                            if (colminion.Health <= spells[Spells.Q].GetDamage(colminion))
                            {
                                killcount++;
                            }
                            else
                            {
                                break;
                            }
                        }

                        if (killcount >= countMinions)
                        {
                            if (minion.IsValidTarget())
                            {
                                spells[Spells.Q].Cast(minion);
                                return;
                            }
                        }
                    }
                }
            }

            if (!useE || !spells[Spells.E].IsReady())
            {
                return;
            }

            var minionkillcount =
                minions.Count(x => spells[Spells.E].CanCast(x) && x.Health <= spells[Spells.E].GetDamage(x));

            if (minionkillcount >= countMinionsE)
            {
                foreach (var minion in minions.Where(x => x.Health <= spells[Spells.E].GetDamage(x)))
                {
                    spells[Spells.E].Cast(minion);
                }
            }
        }

        private static void OnGameUpdate(EventArgs args)
        {
            switch (Orbwalker.ActiveMode)
            {
                case Orbwalking.OrbwalkingMode.Combo:
                    Combo();
                    break;
                case Orbwalking.OrbwalkingMode.LaneClear:
                    LaneClear();
                    JungleClear();
                    break;
                case Orbwalking.OrbwalkingMode.Mixed:
                    Harass();
                    break;
            }

            Killsteal();

            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
            {
                if (Player.Buffs.Count(buf => buf.Name == "Muramana") == 0)
                {
                    var muramana = ItemData.Muramana.GetItem();
                    if (muramana.IsOwned(Player))
                    {
                        muramana.Cast();
                    }
                }
            }
            else
            {
                if (Player.Buffs.Count(buf => buf.Name == "Muramana") != 0)
                {
                    var muramana = ItemData.Muramana.GetItem();
                    if (muramana.IsOwned(Player))
                    {
                        muramana.Cast();
                    }
                }
            }

            var target = TargetSelector.GetTarget(spells[Spells.R].Range, TargetSelector.DamageType.Physical);

            if (spells[Spells.R].IsReady() && target.IsValidTarget()
                && ElVarusMenu.Menu.Item("ElVarus.SemiR").GetValue<KeyBind>().Active)
            {
                spells[Spells.R].CastOnUnit(target);
            }
        }

        #endregion
    }
}