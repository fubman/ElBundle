﻿namespace ElUtilitySuite.Summoners
{
    using System.Linq;

    using ElUtilitySuite.Utility;

    using LeagueSharp;
    using LeagueSharp.Common;

    public class Heal : IPlugin
    {
        #region Public Properties

        /// <summary>
        ///     Gets or sets the heal spell.
        /// </summary>
        /// <value>
        ///     The heal spell.
        /// </value>
        public Spell HealSpell { get; set; }

        #endregion

        #region Properties

        /// <summary>
        ///     Gets the player.
        /// </summary>
        /// <value>
        ///     The player.
        /// </value>
        private Obj_AI_Hero Player
        {
            get
            {
                return ObjectManager.Player;
            }
        }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        ///     Loads this instance.
        /// </summary>
        public void Load()
        {
            var healSlot = this.Player.GetSpell(SpellSlot.Summoner1).Name == "summonerheal"
                               ? SpellSlot.Summoner1
                               : this.Player.GetSpell(SpellSlot.Summoner2).Name == "summonerheal"
                                     ? SpellSlot.Summoner2
                                     : SpellSlot.Unknown;

            if (healSlot == SpellSlot.Unknown)
            {
                return;
            }

            this.HealSpell = new Spell(healSlot, 550);

            AttackableUnit.OnDamage += this.AttackableUnit_OnDamage;
        }

        #endregion

        #region Methods

        private void AttackableUnit_OnDamage(AttackableUnit sender, AttackableUnitDamageEventArgs args)
        {
            if (!InitializeMenu.Menu.Item("Heal.Activated").IsActive())
            {
                return;
            }

            var obj = ObjectManager.GetUnitByNetworkId<GameObject>(args.TargetNetworkId);

            if (obj.Type != GameObjectType.obj_AI_Hero)
            {
                return;
            }

            var hero = (Obj_AI_Hero)obj;

            if (hero.IsEnemy)
            {
                return;
            }

            if (
                ObjectManager.Get<Obj_AI_Hero>()
                    .Any(
                        x =>
                        x.IsAlly && InitializeMenu.Menu.Item(string.Format("healon{0}", x.ChampionName)).IsActive()
                        && ((int)(args.Damage / x.MaxHealth * 100)
                            > InitializeMenu.Menu.Item("Heal.Damage").GetValue<Slider>().Value
                            || x.HealthPercent < InitializeMenu.Menu.Item("Heal.HP").GetValue<Slider>().Value)
                        && x.Distance(this.Player) < 850 && x.CountEnemiesInRange(1000) >= 1))
            {
                this.HealSpell.Cast();
            }
        }

        #endregion
    }
}