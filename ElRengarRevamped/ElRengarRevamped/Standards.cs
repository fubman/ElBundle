﻿namespace ElRengarRevamped
{
    using System.Collections.Generic;
    using System.Linq;

    using LeagueSharp;
    using LeagueSharp.Common;

    public class Standards
    {
        #region Static Fields

        private static readonly int[] BlueSmite = { 3706, 1400, 1401, 1402, 1403 };

        private static readonly int[] RedSmite = { 3715, 1415, 1414, 1413, 1412 };

        protected static readonly Dictionary<Spells, Spell> spells = new Dictionary<Spells, Spell>
                                                                         {
                                                                             {
                                                                                 Spells.Q,
                                                                                 new Spell(
                                                                                 SpellSlot.Q,
                                                                                 Orbwalking.GetRealAutoAttackRange(
                                                                                     Player) + 100)
                                                                             },
                                                                             {
                                                                                 Spells.W,
                                                                                 new Spell(
                                                                                 SpellSlot.W,
                                                                                 500 + Player.BoundingRadius)
                                                                             },
                                                                             {
                                                                                 Spells.E,
                                                                                 new Spell(
                                                                                 SpellSlot.E,
                                                                                 1000 + Player.BoundingRadius)
                                                                             },
                                                                             { Spells.R, new Spell(SpellSlot.R, 2000) }
                                                                         };

        public static int LastSwitch;

        public static Orbwalking.Orbwalker Orbwalker;

        protected static SpellSlot Ignite;

        protected static SpellSlot Smite;

        protected static Items.Item Youmuu;

        #endregion

        #region Public Properties

        public static int Ferocity => (int)ObjectManager.Player.Mana;

        public static bool HasPassive => Player.Buffs.Any(x => x.Name.ToLower().Contains("rengarpassivebuff"));

        public static Obj_AI_Hero Player => ObjectManager.Player;

        public static bool RengarR => Player.Buffs.Any(x => x.Name.ToLower().Contains("RengarR"));

        public static string ScriptVersion => typeof(Rengar).Assembly.GetName().Version.ToString();

        #endregion

        #region Public Methods and Operators

        public static float IgniteDamage(Obj_AI_Hero target)
        {
            if (Ignite == SpellSlot.Unknown || Player.Spellbook.CanUseSpell(Ignite) != SpellState.Ready)
            {
                return 0f;
            }
            return (float)Player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite);
        }

        public static bool IsActive(string menuItem) => MenuInit.Menu.Item(menuItem).IsActive();

        #endregion

        #region Methods

        protected static StringList IsListActive(string menuItem) => MenuInit.Menu.Item(menuItem).GetValue<StringList>();

        protected static void SmiteCombo()
        {
            if (BlueSmite.Any(id => Items.HasItem(id)))
            {
                Smite = Player.GetSpellSlot("s5_summonersmiteplayerganker");
                return;
            }

            if (RedSmite.Any(id => Items.HasItem(id)))
            {
                Smite = Player.GetSpellSlot("s5_summonersmiteduel");
                return;
            }

            Smite = Player.GetSpellSlot("summonersmite");
        }

        #endregion
    }
}