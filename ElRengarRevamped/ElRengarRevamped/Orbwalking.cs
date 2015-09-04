﻿#region LICENSE

/*
 Copyright 2014 - 2015 LeagueSharp
 Orbwalking.cs is part of LeagueSharp.Common.
 
 LeagueSharp.Common is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.
 
 LeagueSharp.Common is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.
 
 You should have received a copy of the GNU General Public License
 along with LeagueSharp.Common. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

#region



#endregion

namespace ElRengarRevamped
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using LeagueSharp;
    using LeagueSharp.Common;

    using SharpDX;

    using Color = System.Drawing.Color;

    /// <summary>
    ///     This class offers everything related to auto-attacks and orbwalking.
    /// </summary>
    public static class Orbwalking
    {
        public delegate void AfterAttackEvenH(AttackableUnit unit, AttackableUnit target);

        public delegate void BeforeAttackEvenH(BeforeAttackEventArgs args);

        public delegate void OnAttackEvenH(AttackableUnit unit, AttackableUnit target);

        public delegate void OnNonKillableMinionH(AttackableUnit minion);

        public delegate void OnTargetChangeH(AttackableUnit oldTarget, AttackableUnit newTarget);

        public enum OrbwalkingMode
        {
            LastHit,
            Mixed,
            LaneClear,
            Combo,
            None
        }

        //Spells that reset the attack timer.
        private static readonly string[] AttackResets =
        {
            "dariusnoxiantacticsonh", "fioraflurry", "garenq",
            "hecarimrapidslash", "jaxempowertwo", "jaycehypercharge", "leonashieldofdaybreak", "luciane", "lucianq",
            "monkeykingdoubleattack", "mordekaisermaceofspades", "nasusq", "nautiluspiercinggaze", "netherblade",
            "parley", "poppydevastatingblow", "powerfist", "renektonpreexecute", "rengarq", "shyvanadoubleattack",
            "sivirw", "takedown", "talonnoxiandiplomacy", "trundletrollsmash", "vaynetumble", "vie", "volibearq",
            "xenzhaocombotarget", "yorickspectral", "reksaiq", "itemtitanichydracleave"
        };

        //Spells that are not attacks even if they have the "attack" word in their name.
        private static readonly string[] NoAttacks =
        {
            "jarvanivcataclysmattack", "monkeykingdoubleattack",
            "shyvanadoubleattack", "shyvanadoubleattackdragon", "zyragraspingplantattack", "zyragraspingplantattack2",
            "zyragraspingplantattackfire", "zyragraspingplantattack2fire", "viktorpowertransfer", "sivirwattackbounce"
        };

        //Spells that are attacks even if they dont have the "attack" word in their name.
        private static readonly string[] Attacks =
        {
            "caitlynheadshotmissile", "frostarrow", "garenslash2",
            "kennenmegaproc", "lucianpassiveattack", "masteryidoublestrike", "quinnwenhanced", "renektonexecute",
            "renektonsuperexecute", "rengarnewpassivebuffdash", "trundleq", "xenzhaothrust", "xenzhaothrust2",
            "xenzhaothrust3", "viktorqbuff"
        };

        // Champs whose auto attacks can't be cancelled
        private static readonly string[] NoCancelChamps = { "Kalista" };
        public static int LastAATick;
        public static bool Attack = true;
        public static bool DisableNextAttack;
        public static bool Move = true;
        public static int LastMoveCommandT;
        public static Vector3 LastMoveCommandPosition = Vector3.Zero;
        private static AttackableUnit _lastTarget;
        private static readonly Obj_AI_Hero Player;
        private static int _delay;
        private static float _minDistance = 400;
        private static bool _missileLaunched;
        private static readonly Random _random = new Random(DateTime.Now.Millisecond);

        static Orbwalking()
        {
            Player = ObjectManager.Player;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpell;
            MissileClient.OnCreate += MissileClient_OnCreate;
            Spellbook.OnStopCast += SpellbookOnStopCast;
        }

        /// <summary>
        ///     This event is fired before the player auto attacks.
        /// </summary>
        public static event BeforeAttackEvenH BeforeAttack;

        /// <summary>
        ///     This event is fired when a unit is about to auto-attack another unit.
        /// </summary>
        public static event OnAttackEvenH OnAttack;

        /// <summary>
        ///     This event is fired after a unit finishes auto-attacking another unit (Only works with player for now).
        /// </summary>
        public static event AfterAttackEvenH AfterAttack;

        /// <summary>
        ///     Gets called on target changes
        /// </summary>
        public static event OnTargetChangeH OnTargetChange;

        //  <summary>
        //      Gets called if you can't kill a minion with auto attacks
        //  </summary>
        public static event OnNonKillableMinionH OnNonKillableMinion;

        private static void FireBeforeAttack(AttackableUnit target)
        {
            if (BeforeAttack != null)
            {
                BeforeAttack(new BeforeAttackEventArgs { Target = target });
            }
            else
            {
                DisableNextAttack = false;
            }
        }

        private static void FireOnAttack(AttackableUnit unit, AttackableUnit target)
        {
            if (OnAttack != null)
            {
                OnAttack(unit, target);
            }
        }

        private static void FireAfterAttack(AttackableUnit unit, AttackableUnit target)
        {
            if (AfterAttack != null && target.IsValidTarget())
            {
                AfterAttack(unit, target);
            }
        }

        private static void FireOnTargetSwitch(AttackableUnit newTarget)
        {
            if (OnTargetChange != null && (!_lastTarget.IsValidTarget() || _lastTarget != newTarget))
            {
                OnTargetChange(_lastTarget, newTarget);
            }
        }

        private static void FireOnNonKillableMinion(AttackableUnit minion)
        {
            if (OnNonKillableMinion != null)
            {
                OnNonKillableMinion(minion);
            }
        }

        /// <summary>
        ///     Returns true if the spellname resets the attack timer.
        /// </summary>
        public static bool IsAutoAttackReset(string name)
        {
            return AttackResets.Contains(name.ToLower());
        }

        /// <summary>
        ///     Returns true if the unit is melee
        /// </summary>
        public static bool IsMelee(this Obj_AI_Base unit)
        {
            return unit.CombatType == GameObjectCombatType.Melee;
        }

        /// <summary>
        ///     Returns true if the spellname is an auto-attack.
        /// </summary>
        public static bool IsAutoAttack(string name)
        {
            return (name.ToLower().Contains("attack") && !NoAttacks.Contains(name.ToLower())) ||
                   Attacks.Contains(name.ToLower());
        }

        /// <summary>
        ///     Returns the auto-attack range.
        /// </summary>
        public static float GetRealAutoAttackRange(AttackableUnit target)
        {
            var result = Player.AttackRange + Player.BoundingRadius;
            if (target.IsValidTarget())
            {
                return result + target.BoundingRadius;
            }
            return result;
        }

        /// <summary>
        ///     Returns true if the target is in auto-attack range.
        /// </summary>
        public static bool InAutoAttackRange(AttackableUnit target)
        {
            if (!target.IsValidTarget())
            {
                return false;
            }
            var myRange = GetRealAutoAttackRange(target);
            return
                Vector2.DistanceSquared(
                    (target is Obj_AI_Base) ? ((Obj_AI_Base)target).ServerPosition.To2D() : target.Position.To2D(),
                    Player.ServerPosition.To2D()) <= myRange * myRange;
        }

        /// <summary>
        ///     Returns player auto-attack missile speed.
        /// </summary>
        public static float GetMyProjectileSpeed()
        {
            return IsMelee(Player) || Player.ChampionName == "Azir" ? float.MaxValue : Player.BasicAttack.MissileSpeed;
        }

        /// <summary>
        ///     Returns if the player's auto-attack is ready.
        /// </summary>
        public static bool CanAttack()
        {
            return Utils.GameTimeTickCount + Game.Ping / 2 + 25 >= LastAATick + Player.AttackDelay * 1000 && Attack;
        }

        /// <summary>
        ///     Returns true if moving won't cancel the auto-attack.
        /// </summary>
        public static bool CanMove(float extraWindup)
        {
            if (!Move)
            {
                return false;
            }

            if (_missileLaunched && Orbwalker.MissileCheck)
            {
                return true;
            }

            return NoCancelChamps.Contains(Player.ChampionName) || (Utils.GameTimeTickCount + Game.Ping / 2 >= LastAATick + Player.AttackCastDelay * 1000 + extraWindup);
        }

        public static void SetMovementDelay(int delay)
        {
            _delay = delay;
        }

        public static void SetMinimumOrbwalkDistance(float d)
        {
            _minDistance = d;
        }

        public static float GetLastMoveTime()
        {
            return LastMoveCommandT;
        }

        public static Vector3 GetLastMovePosition()
        {
            return LastMoveCommandPosition;
        }

        public static void MoveTo(Vector3 position,
            float holdAreaRadius = 0,
            bool overrideTimer = false,
            bool useFixedDistance = true,
            bool randomizeMinDistance = true)
        {
            if (Utils.GameTimeTickCount - LastMoveCommandT < _delay && !overrideTimer)
            {
                return;
            }

            LastMoveCommandT = Utils.GameTimeTickCount;

            var playerPosition = Player.ServerPosition;

            if (playerPosition.Distance(position, true) < holdAreaRadius * holdAreaRadius)
            {
                if (Player.Path.Length > 0)
                {
                    Player.IssueOrder(GameObjectOrder.Stop, playerPosition);
                    LastMoveCommandPosition = playerPosition;
                }
                return;
            }

            var point = position;
            if (useFixedDistance)
            {
                point = playerPosition.Extend(
                    position, (randomizeMinDistance ? (_random.NextFloat(0.6f, 1) + 0.2f) * _minDistance : _minDistance));
            }
            else
            {
                if (randomizeMinDistance)
                {
                    point = playerPosition.Extend(position, (_random.NextFloat(0.6f, 1) + 0.2f) * _minDistance);
                }
                else if (playerPosition.Distance(position) > _minDistance)
                {
                    point = playerPosition.Extend(position, _minDistance);
                }
            }

            Player.IssueOrder(GameObjectOrder.MoveTo, point);
            LastMoveCommandPosition = point;
        }

        /// <summary>
        ///     Orbwalk a target while moving to Position.
        /// </summary>
        public static void Orbwalk(AttackableUnit target,
            Vector3 position,
            float extraWindup = 90,
            float holdAreaRadius = 0,
            bool useFixedDistance = true,
            bool randomizeMinDistance = true)
        {
            try
            {
                if (target.IsValidTarget() && CanAttack())
                {
                    DisableNextAttack = false;
                    FireBeforeAttack(target);

                    if (!DisableNextAttack)
                    {
                        if (!NoCancelChamps.Contains(Player.ChampionName))
                        {
                            LastAATick = Utils.GameTimeTickCount + Game.Ping + 100 - (int)(ObjectManager.Player.AttackCastDelay * 1000f);
                            _missileLaunched = false;

                            var d = GetRealAutoAttackRange(target) - 65;
                            if (Player.Distance(target, true) > d * d && !Player.IsMelee)
                            {
                                LastAATick = Utils.GameTimeTickCount + Game.Ping + 400 - (int)(ObjectManager.Player.AttackCastDelay * 1000f);
                            }
                        }

                        if (!Player.IssueOrder(GameObjectOrder.AttackUnit, target))
                        {
                            //ResetAutoAttackTimer();
                        }

                        _lastTarget = target;
                        return;
                    }
                }

                if (CanMove(extraWindup))
                {
                    MoveTo(position, holdAreaRadius, false, useFixedDistance, randomizeMinDistance);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        /// <summary>
        ///     Resets the Auto-Attack timer.
        /// </summary>
        public static void ResetAutoAttackTimer()
        {
            LastAATick = 0;
        }

        private static void SpellbookOnStopCast(Spellbook spellbook, SpellbookStopCastEventArgs args)
        {
            if (spellbook.Owner.IsValid && spellbook.Owner.IsMe && args.DestroyMissile && args.StopAnimation)
            {
                ResetAutoAttackTimer();
            }
        }

        private static void MissileClient_OnCreate(GameObject sender, EventArgs args)
        {
            var missile = sender as MissileClient;
            if (missile != null && missile.SpellCaster.IsMe && IsAutoAttack(missile.SData.Name))
            {
                _missileLaunched = true;
            }
        }

        private static void OnProcessSpell(Obj_AI_Base unit, GameObjectProcessSpellCastEventArgs Spell)
        {
            try
            {
                var spellName = Spell.SData.Name;

                if (IsAutoAttackReset(spellName) && unit.IsMe)
                {
                    Utility.DelayAction.Add(250, ResetAutoAttackTimer);
                }

                if (!IsAutoAttack(spellName))
                {
                    return;
                }

                if (unit.IsMe &&
                    (Spell.Target is Obj_AI_Base || Spell.Target is Obj_BarracksDampener || Spell.Target is Obj_HQ))
                {
                    LastAATick = Utils.GameTimeTickCount - Game.Ping / 2;
                    _missileLaunched = false;

                    if (Spell.Target is Obj_AI_Base)
                    {
                        var target = (Obj_AI_Base)Spell.Target;
                        if (target.IsValid)
                        {
                            FireOnTargetSwitch(target);
                            _lastTarget = target;
                        }

                        //Trigger it for ranged until the missiles catch normal attacks again!
                        Utility.DelayAction.Add(
                            (int)(unit.AttackCastDelay * 1000 + 40), () => FireAfterAttack(unit, _lastTarget));
                    }
                }

                FireOnAttack(unit, _lastTarget);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public class BeforeAttackEventArgs
        {
            private bool _process = true;
            public AttackableUnit Target;
            public Obj_AI_Base Unit = ObjectManager.Player;

            public bool Process
            {
                get { return this._process; }
                set
                {
                    DisableNextAttack = !value;
                    this._process = value;
                }
            }
        }

        /// <summary>
        ///     This class allows you to add an instance of "Orbwalker" to your assembly in order to control the orbwalking in an
        ///     easy way.
        /// </summary>
        public class Orbwalker
        {
            private const float LaneClearWaitTimeMod = 2f;
            private static Menu _config;
            private readonly Obj_AI_Hero Player;
            private Obj_AI_Base _forcedTarget;
            private OrbwalkingMode _mode = OrbwalkingMode.None;
            private Vector3 _orbwalkingPoint;
            private Obj_AI_Minion _prevMinion;
            public static List<Orbwalker> Instances = new List<Orbwalker>();

            public Orbwalker(Menu attachToMenu)
            {
                _config = attachToMenu;
                /* Drawings submenu */
                var drawings = new Menu("Drawings", "drawings");
                drawings.AddItem(
                    new MenuItem("AACircle", "AACircle").SetShared()
                        .SetValue(new Circle(true, Color.FromArgb(255, 255, 0, 255))));
                drawings.AddItem(
                    new MenuItem("AACircle2", "Enemy AA circle").SetShared()
                        .SetValue(new Circle(false, Color.FromArgb(255, 255, 0, 255))));
                drawings.AddItem(
                    new MenuItem("HoldZone", "HoldZone").SetShared()
                        .SetValue(new Circle(false, Color.FromArgb(255, 255, 0, 255))));
                _config.AddSubMenu(drawings);

                /* Misc options */
                var misc = new Menu("Misc", "Misc");
                misc.AddItem(
                    new MenuItem("HoldPosRadius", "Hold Position Radius").SetShared().SetValue(new Slider(0, 0, 250)));
                misc.AddItem(new MenuItem("PriorizeFarm", "Priorize farm over harass").SetShared().SetValue(true));

                _config.AddSubMenu(misc);

                /* Missile check */
                _config.AddItem(new MenuItem("MissileCheck", "Use Missile Check").SetShared().SetValue(true));

                /* Delay sliders */
                _config.AddItem(
                    new MenuItem("ExtraWindup", "Extra windup time").SetShared().SetValue(new Slider(80, 0, 200)));
                _config.AddItem(new MenuItem("FarmDelay", "Farm delay").SetShared().SetValue(new Slider(0, 0, 200)));
                _config.AddItem(
                    new MenuItem("MovementDelay", "Movement delay").SetShared().SetValue(new Slider(30, 0, 250)))
                    .ValueChanged += (sender, args) => SetMovementDelay(args.GetNewValue<Slider>().Value);


                /*Load the menu*/
                _config.AddItem(
                    new MenuItem("LastHit", "Last hit").SetShared().SetValue(new KeyBind('X', KeyBindType.Press)));

                _config.AddItem(new MenuItem("Farm", "Mixed").SetShared().SetValue(new KeyBind('C', KeyBindType.Press)));

                _config.AddItem(
                    new MenuItem("LaneClear", "LaneClear").SetShared().SetValue(new KeyBind('V', KeyBindType.Press)));

                _config.AddItem(
                    new MenuItem("Orbwalk", "Combo").SetShared().SetValue(new KeyBind(32, KeyBindType.Press)));

                _delay = _config.Item("MovementDelay").GetValue<Slider>().Value;


                this.Player = ObjectManager.Player;
                Game.OnUpdate += this.GameOnOnGameUpdate;
                Drawing.OnDraw += this.DrawingOnOnDraw;
                Instances.Add(this);
            }

            public virtual bool InAutoAttackRange(AttackableUnit target)
            {
                return Orbwalking.InAutoAttackRange(target);
            }

            private int FarmDelay
            {
                get { return _config.Item("FarmDelay").GetValue<Slider>().Value; }
            }

            public static bool MissileCheck
            {
                get { return _config.Item("MissileCheck").GetValue<bool>(); }
            }

            public OrbwalkingMode ActiveMode
            {
                get
                {
                    if (this._mode != OrbwalkingMode.None)
                    {
                        return this._mode;
                    }

                    if (_config.Item("Orbwalk").GetValue<KeyBind>().Active)
                    {
                        return OrbwalkingMode.Combo;
                    }

                    if (_config.Item("LaneClear").GetValue<KeyBind>().Active)
                    {
                        return OrbwalkingMode.LaneClear;
                    }

                    if (_config.Item("Farm").GetValue<KeyBind>().Active)
                    {
                        return OrbwalkingMode.Mixed;
                    }

                    if (_config.Item("LastHit").GetValue<KeyBind>().Active)
                    {
                        return OrbwalkingMode.LastHit;
                    }

                    return OrbwalkingMode.None;
                }
                set { this._mode = value; }
            }

            /// <summary>
            ///     Enables or disables the auto-attacks.
            /// </summary>
            public void SetAttack(bool b)
            {
                Attack = b;
            }

            /// <summary>
            ///     Enables or disables the movement.
            /// </summary>
            public void SetMovement(bool b)
            {
                Move = b;
            }

            /// <summary>
            ///     Forces the orbwalker to attack the set target if valid and in range.
            /// </summary>
            public void ForceTarget(Obj_AI_Base target)
            {
                this._forcedTarget = target;
            }

            /// <summary>
            ///     Forces the orbwalker to move to that point while orbwalking (Game.CursorPos by default).
            /// </summary>
            public void SetOrbwalkingPoint(Vector3 point)
            {
                this._orbwalkingPoint = point;
            }

            private bool ShouldWait()
            {
                return
                    ObjectManager.Get<Obj_AI_Minion>()
                        .Any(
                            minion =>
                                minion.IsValidTarget() && minion.Team != GameObjectTeam.Neutral &&
                                this.InAutoAttackRange(minion) &&
                                HealthPrediction.LaneClearHealthPrediction(
                                    minion, (int)((this.Player.AttackDelay * 1000) * LaneClearWaitTimeMod), this.FarmDelay) <=
                                this.Player.GetAutoAttackDamage(minion));
            }

            public virtual AttackableUnit GetTarget()
            {
                AttackableUnit result = null;

                if ((this.ActiveMode == OrbwalkingMode.Mixed || this.ActiveMode == OrbwalkingMode.LaneClear) &&
                    !_config.Item("PriorizeFarm").GetValue<bool>())
                {
                    var target = TargetSelector.GetTarget(-1, TargetSelector.DamageType.Physical);
                    if (target != null)
                    {
                        return target;
                    }
                }

                /*Killable Minion*/
                if (this.ActiveMode == OrbwalkingMode.LaneClear || this.ActiveMode == OrbwalkingMode.Mixed ||
                    this.ActiveMode == OrbwalkingMode.LastHit)
                {
                    var MinionList =
                        ObjectManager.Get<Obj_AI_Minion>()
                            .Where(
                                minion =>
                                    minion.IsValidTarget() && this.InAutoAttackRange(minion) &&
                                    minion.Health <
                                    2 *
                                    (ObjectManager.Player.BaseAttackDamage + ObjectManager.Player.FlatPhysicalDamageMod))
                                    .OrderByDescending(minion => minion.CharData.BaseSkinName.Contains("Siege"))
                                    .ThenBy(minion => minion.CharData.BaseSkinName.Contains("Super"))
                                    .ThenBy(minion => minion.Health)
                                    .ThenByDescending(minion => minion.MaxHealth);

                    foreach (var minion in MinionList)
                    {
                        var t = (int)(this.Player.AttackCastDelay * 1000) - 100 + Game.Ping / 2 +
                                1000 * (int)this.Player.Distance(minion) / (int)GetMyProjectileSpeed();
                        var predHealth = HealthPrediction.GetHealthPrediction(minion, t, this.FarmDelay);

                        if (minion.Team != GameObjectTeam.Neutral && MinionManager.IsMinion(minion, true))
                        {
                            if (predHealth <= 0)
                            {
                                FireOnNonKillableMinion(minion);
                            }

                            if (predHealth > 0 && predHealth <= this.Player.GetAutoAttackDamage(minion, true))
                            {
                                return minion;
                            }
                        }
                    }
                }

                //Forced target
                if (this._forcedTarget.IsValidTarget() && this.InAutoAttackRange(this._forcedTarget))
                {
                    return this._forcedTarget;
                }

                /* turrets / inhibitors / nexus */
                if (this.ActiveMode == OrbwalkingMode.LaneClear)
                {
                    /* turrets */
                    foreach (var turret in
                        ObjectManager.Get<Obj_AI_Turret>().Where(t => t.IsValidTarget() && this.InAutoAttackRange(t)))
                    {
                        return turret;
                    }

                    /* inhibitor */
                    foreach (var turret in
                        ObjectManager.Get<Obj_BarracksDampener>().Where(t => t.IsValidTarget() && this.InAutoAttackRange(t)))
                    {
                        return turret;
                    }

                    /* nexus */
                    foreach (var nexus in
                        ObjectManager.Get<Obj_HQ>().Where(t => t.IsValidTarget() && this.InAutoAttackRange(t)))
                    {
                        return nexus;
                    }
                }

                /*Champions*/
                if (this.ActiveMode != OrbwalkingMode.LastHit)
                {
                    var target = TargetSelector.GetTarget(-1, TargetSelector.DamageType.Physical);
                    if (target.IsValidTarget())
                    {
                        return target;
                    }
                }

                /*Jungle minions*/
                if (this.ActiveMode == OrbwalkingMode.LaneClear || this.ActiveMode == OrbwalkingMode.Mixed)
                {
                    result =
                        ObjectManager.Get<Obj_AI_Minion>()
                            .Where(
                                mob =>
                                    mob.IsValidTarget() && mob.Team == GameObjectTeam.Neutral && this.InAutoAttackRange(mob) && mob.CharData.BaseSkinName != "gangplankbarrel")
                            .MaxOrDefault(mob => mob.MaxHealth);
                    if (result != null)
                    {
                        return result;
                    }
                }

                /*Lane Clear minions*/
                if (this.ActiveMode == OrbwalkingMode.LaneClear)
                {
                    if (!this.ShouldWait())
                    {
                        if (this._prevMinion.IsValidTarget() && this.InAutoAttackRange(this._prevMinion))
                        {
                            var predHealth = HealthPrediction.LaneClearHealthPrediction(
                                this._prevMinion, (int)((this.Player.AttackDelay * 1000) * LaneClearWaitTimeMod), this.FarmDelay);
                            if (predHealth >= 2 * this.Player.GetAutoAttackDamage(this._prevMinion) ||
                                Math.Abs(predHealth - this._prevMinion.Health) < float.Epsilon)
                            {
                                return this._prevMinion;
                            }
                        }

                        result = (from minion in
                                      ObjectManager.Get<Obj_AI_Minion>()
                                          .Where(minion => minion.IsValidTarget() && this.InAutoAttackRange(minion) && minion.CharData.BaseSkinName != "gangplankbarrel")
                                  let predHealth =
                                      HealthPrediction.LaneClearHealthPrediction(
                                          minion, (int)((this.Player.AttackDelay * 1000) * LaneClearWaitTimeMod), this.FarmDelay)
                                  where
                                      predHealth >= 2 * this.Player.GetAutoAttackDamage(minion) ||
                                      Math.Abs(predHealth - minion.Health) < float.Epsilon
                                  select minion).MaxOrDefault(m => m.Health);

                        if (result != null)
                        {
                            this._prevMinion = (Obj_AI_Minion)result;
                        }
                    }
                }

                return result;
            }

            private void GameOnOnGameUpdate(EventArgs args)
            {
                try
                {
                    if (this.ActiveMode == OrbwalkingMode.None)
                    {
                        return;
                    }

                    //Prevent canceling important spells
                    if (this.Player.IsCastingInterruptableSpell(true))
                    {
                        return;
                    }

                    var target = this.GetTarget();
                    Orbwalk(
                        target, (this._orbwalkingPoint.To2D().IsValid()) ? this._orbwalkingPoint : Game.CursorPos,
                        _config.Item("ExtraWindup").GetValue<Slider>().Value,
                        _config.Item("HoldPosRadius").GetValue<Slider>().Value);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            private void DrawingOnOnDraw(EventArgs args)
            {
                if (_config.Item("AACircle").GetValue<Circle>().Active)
                {
                    Render.Circle.DrawCircle(
                        this.Player.Position, GetRealAutoAttackRange(null) + 65,
                        _config.Item("AACircle").GetValue<Circle>().Color);
                }

                if (_config.Item("AACircle2").GetValue<Circle>().Active)
                {
                    foreach (var target in
                        HeroManager.Enemies.FindAll(target => target.IsValidTarget(1175)))
                    {
                        Render.Circle.DrawCircle(
                            target.Position, GetRealAutoAttackRange(target) + 65,
                            _config.Item("AACircle2").GetValue<Circle>().Color);
                    }
                }

                if (_config.Item("HoldZone").GetValue<Circle>().Active)
                {
                    Render.Circle.DrawCircle(
                        this.Player.Position, _config.Item("HoldPosRadius").GetValue<Slider>().Value,
                        _config.Item("HoldZone").GetValue<Circle>().Color, 5, true);
                }

            }
        }
    }
}