﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LeagueSharp;
using LeagueSharp.Common;

using SharpDX;

using LeagueSharp.Common.Data;

using ItemData = LeagueSharp.Common.Data.ItemData;
using Color = System.Drawing.Color;

namespace ElTristana
{

    #region

    internal enum Spells
    {
        Q,

        E,

        R
    }

    #endregion

    internal static class Tristana
    {
        #region

        public static String ScriptVersion
        {
            get
            {
                return typeof(Tristana).Assembly.GetName().Version.ToString();
            }
        }

        private static Obj_AI_Hero Player
        {
            get
            {
                return ObjectManager.Player;
            }
        }

        #endregion

        public static Orbwalking.Orbwalker Orbwalker;

        public static Dictionary<Spells, Spell> spells = new Dictionary<Spells, Spell>()
                                                             {
                                                                 { Spells.Q, new Spell(SpellSlot.Q, 550) },
                                                                 { Spells.E, new Spell(SpellSlot.E, 625) },
                                                                 { Spells.R, new Spell(SpellSlot.R, 700) },
                                                             };

        #region

        public static void OnLoad(EventArgs args)
        {
            if (ObjectManager.Player.ChampionName != "Tristana")
            {
                return;
            }

            try
            {
                Console.WriteLine("Injected ElTristana AMK");
                Notifications.AddNotification(String.Format("ElTristana by jQuery v{0}", ScriptVersion), 8000);
                Game.PrintChat(
                        "[00:00] <font color='#f9eb0b'>HEEEEEEY!</font> Use ElUtilitySuite for optimal results! xo jQuery");

                MenuInit.Initialize();
                Game.OnUpdate += OnUpdate;
                Drawing.OnDraw += OnDraw;
                Spellbook.OnCastSpell += OnSpellCast;
                AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
                Interrupter2.OnInterruptableTarget += Interrupter2_OnInterruptableTarget;
            }
            catch (Exception e)
            {
                Console.WriteLine("An error occurred: '{0}'", e);
            }
        }

        #endregion

        #region OnUpdate    

        private static void OnSpellCast(Spellbook sender, SpellbookCastSpellEventArgs args)
        {
            if (Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.None && args.Slot == SpellSlot.W && IsActive("ElTristana.DumbRetards"))
            {
                args.Process = false;
            }
        }

        private static void OnUpdate(EventArgs args)
        {
            if (Player.IsDead)
            {
                return;
            }

            try
            {
                switch (Orbwalker.ActiveMode)
                {
                    case Orbwalking.OrbwalkingMode.Combo:
                        OnCombo();
                        break;
                    case Orbwalking.OrbwalkingMode.LaneClear:
                        OnLaneClear();
                        OnJungleClear();
                        break;
                    case Orbwalking.OrbwalkingMode.Mixed:
                        OnHarass();
                        break;
                }

                spells[Spells.Q].Range = 550 + 9 * (Player.Level - 1);
                spells[Spells.E].Range = 625 + 9 * (Player.Level - 1);
                spells[Spells.R].Range = 517 + 9 * (Player.Level - 1);
            }
            catch (Exception e)
            {
                Console.WriteLine("An error occurred: '{0}'", e);
            }
        }

        #endregion

        #region

        private static void OnCombo()
        {
            var eTarget =
                       HeroManager.Enemies.Find(x => x.HasBuff("TristanaECharge") && x.IsValidTarget(Orbwalking.GetRealAutoAttackRange(Player)));
            var target = eTarget ?? TargetSelector.GetTarget(spells[Spells.E].Range, TargetSelector.DamageType.Physical);

            if (target == null || !target.IsValidTarget()) return;
            if (eTarget != null && IsActive("ElTristana.Combo.Focus.E"))
            {
                TargetSelector.SetTarget(target);
                Hud.SelectedUnit = target;
                Console.WriteLine("Selected target: {0}", target.ChampionName);
            }

            if (spells[Spells.E].IsReady() && IsActive("ElTristana.Combo.E")
                    && Player.ManaPercent > MenuInit.Menu.Item("ElTristana.Combo.E.Mana").GetValue<Slider>().Value)
            {
                foreach (var hero in ObjectManager.Get<Obj_AI_Hero>())
                {
                    if (hero.IsEnemy)
                    {
                        MenuItem getEnemies = MenuInit.Menu.Item("ElTristana.E.On" + hero.CharData.BaseSkinName);
                        if (getEnemies != null && getEnemies.GetValue<bool>())
                        {
                            spells[Spells.E].Cast(hero);
                        }

                        if (getEnemies != null && !getEnemies.GetValue<bool>() && Player.CountEnemiesInRange(1500) == 1)
                        {
                            spells[Spells.E].Cast(hero);
                        }
                    }
                }
            }

            UseItems(target);

            if (spells[Spells.Q].IsReady() && IsActive("ElTristana.Combo.Q")
                && target.IsValidTarget(spells[Spells.E].Range))
            {
                spells[Spells.Q].Cast();
            }

            if (spells[Spells.R].IsReady() && IsActive("ElTristana.Combo.R"))
            {
                if (IsECharged(target) && IsActive("ElTristana.Combo.Always.RE"))
                {
                    if (IsBusterShotable(target))
                    {
                        spells[Spells.R].Cast(target);
                    }
                }

                if (GetExecuteDamage(target) > target.Health + 50)
                {
                    if (IsActive("ElTristana.Combo.Always.R") && GetExecuteDamage(target) > target.Health + 50)
                    {
                        spells[Spells.R].Cast(target);
                    }

                    if (IsECharged(target) && GetExecuteDamage(target) > target.Health + 50)
                    {
                        spells[Spells.R].Cast(target);
                    }

                    if (spells[Spells.R].GetDamage(target) > target.Health + 50
                        || GetExecuteDamage(target) > target.Health + 50)
                    {
                        spells[Spells.R].Cast(target);
                    }
                }
            }
        }

        #endregion

        #region

        private static void OnHarass()
        {
            var target = TargetSelector.GetTarget(spells[Spells.E].Range, TargetSelector.DamageType.Physical);
            if (target == null || !target.IsValidTarget())
            {
                return;
            }

            if (spells[Spells.E].IsReady() && IsActive("ElTristana.Harass.E")
                && Player.ManaPercent > MenuInit.Menu.Item("ElTristana.Harass.E.Mana").GetValue<Slider>().Value)
            {
                foreach (var enemy in from enemy in ObjectManager.Get<Obj_AI_Hero>().Where(hero => hero.IsEnemy)
                                      let getEnemies =
                                          MenuInit.Menu.Item("ElTristana.E.On.Harass" + enemy.CharData.BaseSkinName)
                                      where getEnemies != null && getEnemies.GetValue<bool>()
                                      select enemy)
                {
                    spells[Spells.E].Cast(enemy);
                }
            }

            if (spells[Spells.Q].IsReady() && IsActive("ElTristana.Harass.Q")  && target.IsValidTarget(spells[Spells.E].Range))
            {
                if (IsECharged(target) && IsActive("ElTristana.Harass.QE"))
                {
                    spells[Spells.Q].Cast();
                }
                else if (!IsActive("ElTristana.Harass.QE"))
                {
                    spells[Spells.Q].Cast();
                }
            }
        }

        #endregion

        #region

        private static void OnLaneClear()
        {
            if (IsActive("ElTristana.LaneClear.Tower"))
            {
                foreach (Obj_AI_Turret tower in ObjectManager.Get<Obj_AI_Turret>())
                {
                    if (!tower.IsDead && tower.Health > 100 && tower.IsEnemy && tower.IsValidTarget()
                        && Player.ServerPosition.Distance(tower.ServerPosition)
                        < Orbwalking.GetRealAutoAttackRange(Player))
                    {
                        spells[Spells.E].Cast(tower);
                    }
                }
            }

            var minions = MinionManager.GetMinions(
                ObjectManager.Player.ServerPosition,
                spells[Spells.E].Range,
                MinionTypes.All,
                MinionTeam.NotAlly,
                MinionOrderTypes.MaxHealth);

            if (minions.Count <= 0)
            {
                return;
            }

            if (spells[Spells.E].IsReady() && IsActive("ElTristana.LaneClear.E") && minions.Count > 2
                && Player.ManaPercent > MenuInit.Menu.Item("ElTristana.LaneClear.E.Mana").GetValue<Slider>().Value)
            {
                foreach (var minion in
                    ObjectManager.Get<Obj_AI_Minion>()
                        .Where(minion => minion.IsValidTarget(Orbwalking.GetRealAutoAttackRange(minion))))
                {
                    spells[Spells.E].Cast(minion);
                }
            }

            var eminion =
                minions.Find(x => x.HasBuff("TristanaECharge") && x.IsValidTarget(Orbwalking.GetRealAutoAttackRange(x)));

            if (eminion != null)
            {
                Orbwalker.ForceTarget(eminion);
            }

            if (spells[Spells.Q].IsReady() && IsActive("ElTristana.LaneClear.Q"))
            {
                var eMob = minions.FindAll(x => x.IsValidTarget() && x.HasBuff("TristanaECharge"));
                if (eMob.Any())
                {
                    spells[Spells.Q].Cast();
                }
            }
        }

        #endregion

        #region

        private static void OnJungleClear()
        {

            var minions =
               MinionManager.GetMinions(
                   spells[Spells.Q].Range,
                   MinionTypes.All,
                   MinionTeam.Neutral,
                   MinionOrderTypes.MaxHealth).FirstOrDefault();

            if (!minions.IsValidTarget() || minions == null)
            {
                return;
            }

            if (spells[Spells.E].IsReady() && IsActive("ElTristana.JungleClear.E")
                && Player.ManaPercent > MenuInit.Menu.Item("ElTristana.JungleClear.E.Mana").GetValue<Slider>().Value)
            {
                spells[Spells.E].CastOnUnit(minions);
            }

            if (spells[Spells.Q].IsReady() && IsActive("ElTristana.JungleClear.Q"))
            {
                spells[Spells.Q].Cast();
            }
        }

        #endregion

        #region

        private static void UseItems(Obj_AI_Base target)
        {
            var botrk = ItemData.Blade_of_the_Ruined_King.GetItem();
            var ghost = ItemData.Youmuus_Ghostblade.GetItem();
            var cutlass = ItemData.Bilgewater_Cutlass.GetItem();

            var useBladeEhp = MenuInit.Menu.Item("ElTristana.Items.Blade.EnemyEHP").GetValue<Slider>().Value;
            var useBladeMhp = MenuInit.Menu.Item("ElTristana.Items.Blade.EnemyMHP").GetValue<Slider>().Value;

            if (botrk.IsReady() && botrk.IsOwned(Player) && botrk.IsInRange(target)
                && target.HealthPercent <= useBladeEhp && IsActive("ElTristana.Items.Blade"))
            {
                botrk.Cast(target);
            }

            if (botrk.IsReady() && botrk.IsOwned(Player) && botrk.IsInRange(target)
                && Player.HealthPercent <= useBladeMhp && IsActive("ElTristana.Items.Blade"))
            {
                botrk.Cast(target);
            }

            if (cutlass.IsReady() && cutlass.IsOwned(Player) && cutlass.IsInRange(target)
                && target.HealthPercent <= useBladeEhp && IsActive("ElTristana.Items.Blade"))
            {
                cutlass.Cast(target);
            }

            if (ghost.IsReady() && ghost.IsOwned(Player) && target.IsValidTarget(spells[Spells.Q].Range)
                && IsActive("ElTristana.Items.Youmuu"))
            {
                ghost.Cast();
            }
        }

        #endregion

        #region

        private static void OnDraw(EventArgs args)
        {
            if (IsActive("ElTristana.Draw.off"))
            {
                return;
            }

            if (MenuInit.Menu.Item("ElTristana.Draw.Q").GetValue<Circle>().Active)
            {
                if (spells[Spells.Q].Level > 0)
                {
                    Render.Circle.DrawCircle(ObjectManager.Player.Position, spells[Spells.Q].Range, Color.White);
                }
            }

            if (MenuInit.Menu.Item("ElTristana.Draw.E").GetValue<Circle>().Active)
            {
                if (spells[Spells.E].Level > 0)
                {
                    Render.Circle.DrawCircle(ObjectManager.Player.Position, spells[Spells.E].Range, Color.White);
                }
            }

            if (MenuInit.Menu.Item("ElTristana.Draw.R").GetValue<Circle>().Active)
            {
                if (spells[Spells.R].Level > 0)
                {
                    Render.Circle.DrawCircle(ObjectManager.Player.Position, spells[Spells.R].Range, Color.White);
                }
            }
        }

        #endregion

        #region

        private static void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (IsActive("ElTristana.Antigapcloser"))
            {
                if (gapcloser.Sender.IsValidTarget(250f) && spells[Spells.R].IsReady())
                {
                    spells[Spells.R].Cast(gapcloser.Sender);
                }
            }
        }

        #endregion

        #region

        private static void Interrupter2_OnInterruptableTarget(
            Obj_AI_Hero sender,
            Interrupter2.InterruptableTargetEventArgs args)
        {
            if (!IsActive("ElTristana.Interrupter"))
            {
                return;
            }
            if (args.DangerLevel != Interrupter2.DangerLevel.High || sender.Distance(Player) > spells[Spells.R].Range)
            {
                return;
            }

            if (spells[Spells.R].CanCast(sender) && args.DangerLevel >= Interrupter2.DangerLevel.High)
            {
                spells[Spells.R].Cast(sender);
            }
        }

        #endregion

        #region

        private static bool IsActive(string menuItem)
        {
            return MenuInit.Menu.Item(menuItem).GetValue<bool>();
        }

        #endregion

        #region

        private static bool IsBusterShotable(this Obj_AI_Base target)
        {
            var hero = target as Obj_AI_Hero;
            return GetExecuteDamage(target) + spells[Spells.R].GetDamage(target) > GetHealth(target) && (hero == null);
        }

        private static float GetHealth(Obj_AI_Base target)
        {
            return target.Health;
        }

        private static BuffInstance GetECharge(this Obj_AI_Base target)
        {
            return target.Buffs.Find(x => x.DisplayName == "TristanaECharge");
        }

        private static bool IsECharged(this Obj_AI_Base target)
        {
            return target.GetECharge() != null;
        }

        private static double GetExecuteDamage(Obj_AI_Base target)
        {
            if (target.GetBuffCount("TristanaECharge") != 0)
            {
                return (spells[Spells.E].GetDamage(target) * ((0.3 * target.GetBuffCount("TristanaECharge") + 1))
                        + (Player.TotalAttackDamage()) + (Player.TotalMagicalDamage * 0.5));
            }

            return 0;
        }

        private static float TotalAttackDamage(this Obj_AI_Base target)
        {
            return target.BaseAttackDamage + target.FlatPhysicalDamageMod;
        }

        #endregion
    }
}
