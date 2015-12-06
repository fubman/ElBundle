﻿namespace ElUtilitySuite
{
    using System;
    using System.Linq;

    using LeagueSharp;
    using LeagueSharp.Common;

    internal class Entry
    {
        #region Public Properties

        public static bool IsHowlingAbyss
        {
            get
            {
                return Game.MapId == GameMapId.HowlingAbyss;
            }
        }

        public static bool IsSummonersRift
        {
            get
            {
                return Game.MapId == GameMapId.SummonersRift;
            }
        }

        public static bool IsTwistedTreeline
        {
            get
            {
                return Game.MapId == GameMapId.TwistedTreeline;
            }
        }

        public static Obj_AI_Hero Player
        {
            get
            {
                return ObjectManager.Player;
            }
        }

        public static string ScriptVersion
        {
            get
            {
                return typeof(Entry).Assembly.GetName().Version.ToString();
            }
        }

        #endregion

        #region Public Methods and Operators

        public static Obj_AI_Hero Allies()
        {
            var target = Player;
            foreach (var unit in
                ObjectManager.Get<Obj_AI_Hero>()
                    .Where(x => x.IsAlly && x.IsValidTarget(900, false))
                    .OrderByDescending(xe => xe.Health / xe.MaxHealth * 100))
            {
                target = unit;
            }

            return target;
        }

        public static void OnLoad(EventArgs args)
        {
            try
            {
                //Lel let's re-do this another time, kappa 123 after the beep.
                new RecallTracker();
                Heal.Load();
                Ignite.Load();
                Barrier.Load();
                Potions.Load();
                Smite.Load();
                Offensive.Load();
                Defensive.Load();
                ProtectYourself.Load();
                Zhonya.Init();
                SpellCleanser.Init();
                JungleTracker.Init();
                InitializeMenu.Load();
                CheckVersion.Init();
                Notifications.AddNotification(string.Format("El Utility Suite by jQuery v{0}", ScriptVersion), 10000);

                var type = Type.GetType("ElUtilitySuite.Plugins." + Player.ChampionName);
                if (type != null)
                {
                    Base.Load(Player.ChampionName);
                    Console.WriteLine("Loaded");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("An error occurred: '{0}'", e);
            }
        }

        #endregion
    }
}