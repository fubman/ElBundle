﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;


namespace ElNamiReborn
{
	internal enum Spells {
		Q,
		W,
		E,
		R
	}

	class Nami {

		public static Orbwalking.Orbwalker Orbwalker;
		static SpellSlot _ignite;

		public static Dictionary<Spells, Spell> spells = new Dictionary<Spells, Spell> () {
			{ Spells.Q, new Spell (SpellSlot.Q, 875) },
			{ Spells.W, new Spell (SpellSlot.W, 725) },
			{ Spells.E, new Spell (SpellSlot.E, 800) },
			{ Spells.R, new Spell (SpellSlot.R, 2750) },
		};

		#region hitchance

		static HitChance CustomHitChance
		{
			get { return GetHitchance(); }
		}

		static HitChance GetHitchance()
		{
			switch (ElNamiMenu._menu.Item("ElNamiReborn.hitChance").GetValue<StringList>().SelectedIndex)
			{
			case 0:
				return HitChance.Low;
			case 1:
				return HitChance.Medium;
			case 2:
				return HitChance.High;
			case 3:
				return HitChance.VeryHigh;
			default:
				return HitChance.Medium;
			}
		}

		#endregion

		#region Gameloaded 

		public static void Game_OnGameLoad(EventArgs args)
		{
			if (!ObjectManager.Player.ChampionName.Equals("Nami", StringComparison.CurrentCultureIgnoreCase)) 
				return;
		
			Notifications.AddNotification ("ElNamiReborn by jQuery v1.0.0.0", 1000);

			spells[Spells.Q].SetSkillshot(1f, 150f, float.MaxValue, false, SkillshotType.SkillshotCircle);
			spells[Spells.R].SetSkillshot(0.5f, 260f, 850f, false, SkillshotType.SkillshotLine);

			_ignite = ObjectManager.Player.GetSpellSlot("summonerdot");

			ElNamiMenu.Initialize ();
			Game.OnUpdate += OnUpdate;
			Interrupter2.OnInterruptableTarget += Interrupter2_OnInterruptableTarget;
			AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
		}

		#endregion

		#region OnGameUpdate

		static void OnUpdate(EventArgs args)
		{
			if (ObjectManager.Player.IsDead)
				return;

			var target = TargetSelector.GetTarget(spells[Spells.Q].Range, TargetSelector.DamageType.Magical);

			switch (Orbwalker.ActiveMode)
			{
				case Orbwalking.OrbwalkingMode.Combo:
					Combo(target);
					break;

				case Orbwalking.OrbwalkingMode.Mixed:
					Harass (target);
					break;
			}
				
			PlayerHealing ();
			AllyHealing ();
			AutoHarass (target);
		}

		#endregion

		#region AutoHarass

		static void AutoHarass(Obj_AI_Base target) 
		{
			if (target == null || !target.IsValidTarget())
				return;

			var autoHarass = ElNamiMenu._menu.Item ("ElNamiReborn.AutoHarass.Activated").GetValue<bool> ();
			var useQ = ElNamiMenu._menu.Item ("ElNamiReborn.AutoHarass.Q").GetValue<bool> ();
			var useW = ElNamiMenu._menu.Item ("ElNamiReborn.AutoHarass.W").GetValue<bool> ();
			var minimumMana = ElNamiMenu._menu.Item ("ElNamiReborn.AutoHarass.Mana").GetValue<Slider> ().Value;

			if (ObjectManager.Player.ManaPercentage < minimumMana)
				return;
				
			if (!autoHarass)
				return;

			if(useQ && spells[Spells.Q].IsReady)
			{
				var prediction = spells [Spells.Q].GetPrediction (target).Hitchance;

				if (prediction >= CustomHitChance) 
				{
					spells [Spells.Q].Cast (target);
				}
			}

			if(useW && spells[Spells.W].IsReady)
			{
				spells [Spells.W].Cast (target);
			}
			
		}

		#endregion

		#region PlayerHealing

		static void PlayerHealing()
		{
			if (ObjectManager.Player.IsRecalling || ObjectManager.Player.InFountain)
				return;

			var useHeal = ElNamiMenu._menu.Item ("ElNamiReborn.Heal.Activate").GetValue<Bool> ();
			var playerHP = ElNamiMenu._menu.Item ("ElNamiReborn.Heal.Player.HP").GetValue<Slider> ().Value;
			var minumumMana = ElNamiMenu._menu.Item ("ElNamiReborn.Heal.Mana").GetValue<Slider> ().Value;

			if (useHeal && (ObjectManager.Player.Health / ObjectManager.Player.MaxHealth) * 100 && spells[Spells.W].IsReady() && ObjectManager.Player.ManaPercentage >= minumumMana) 
			{
				spells [Spells.W].Cast (ObjectManager.Player);
			}
		}

		#endregion

		#region AllyHealing

		static void AllyHealing()
		{
			if (ObjectManager.Player.IsRecalling || ObjectManager.Player.InFountain)
				return;

			var useHeal = ElNamiMenu._menu.Item ("ElNamiReborn.Heal.Ally.HP").GetValue<Bool> ();
			var AllyHP = ElNamiMenu._menu.Item ("ElNamiReborn.Heal.Ally.HP.Percentage").GetValue<Slider> ().Value;
			var minumumMana = ElNamiMenu._menu.Item ("ElNamiReborn.Heal.Mana").GetValue<Slider> ().Value;

			foreach (var hero in ObjectManager.Get<Obj_AI_Hero>().Where(h => h.IsAlly && !h.IsMe)) 
			{
				if (hero.IsRecalling || hero.InFountain)
					return;

				if (useHeal && (hero.Health / hero.MaxHealth) * 100 && spells[Spells.W].IsReady() && hero.Distance(ObjectManager.Player.ServerPosition) <= spells[Spells.W].Range && ObjectManager.Player.ManaPercentage >= minumumMana) 
				{
					spells [Spells.W].Cast (hero);
				}
			}
		}

		#endregion

		#region Harass

		static void Harass(Obj_AI_Base target) 
		{
			if (target == null || !target.IsValidTarget() || target.IsMinion)
				return;

			var useQ = ElNamiMenu._menu.Item ("ElNamiReborn.Harass.Q").GetValue<bool> ();
			var useW = ElNamiMenu._menu.Item ("ElNamiReborn.Harass.W").GetValue<bool> ();
			var useE = ElNamiMenu._menu.Item ("ElNamiReborn.Harass.E").GetValue<bool> ();
			var checkMana = ElNamiMenu._menu.Item ("ElNamiReborn.Harass.Mana").GetValue<Slider> ().Value;

			if (ObjectManager.Player.ManaPercentage < checkMana)
				return;

			if(useQ && spells[Spells.Q].IsReady()) 
			{
				var prediction = spells [Spells.Q].GetPrediction (target).Hitchance;

				if (prediction >= CustomHitChance) 
				{
					spells [Spells.Q].Cast (target);
				}
			}

			if (useE && spells [Spells.E].IsReady ()) 
			{			
				if (ObjectManager.Player.GetAlliesInRange (spells[Spells.E].Range).Any ()) 
				{
					var bestAlly = (ObjectManager.Player.GetAlliesInRange (spells [Spells.E].Range) 
						.OrderByDescending (x => (x.MagicDamageDealtPlayer + x.PhysicalDamageDealtPlayer) 
							&& x.IsAlly 
							&& !x.HasBuffOfType(BuffType.Invulnerability) 
							&& !x.HasBuffOfType(BuffType.SpellShield)
							&& !x.IsRecalling));

					spells[Spells.E].Cast(bestAlly);
				} 
				else 
				{
					spells [Spells.E].Cast (ObjectManager.Player);
				}
			}

			if (useW && spells [Spells.W].IsReady ()) 
			{
				spells [Spells.W].Cast ();
			}
		}

		#endregion

		#region Combo

		static void Combo(Obj_AI_Base target) 
		{

			if (target == null || !target.IsValidTarget())
				return;
				
			var useQ = ElNamiMenu._menu.Item ("ElNamiReborn.Combo.Q").GetValue<bool> ();
			var useW = ElNamiMenu._menu.Item ("ElNamiReborn.Combo.W").GetValue<bool> ();
			var useE = ElNamiMenu._menu.Item ("ElNamiReborn.Combo.E").GetValue<bool> ();
			var useR = ElNamiMenu._menu.Item ("ElNamiReborn.Combo.R").GetValue<bool> ();
			var useIgnite = ElNamiMenu._menu.Item ("ElNamiReborn.Combo.Ignite").GetValue<bool> ();
			var countR = ElNamiMenu._menu.Item ("ElNamiReborn.Combo.R.Count").GetValue<Slider> ().Value;


			if(useQ && spells[Spells.Q].IsReady()) 
			{
				var prediction = spells [Spells.Q].GetPrediction (target).Hitchance;

				if (prediction >= CustomHitChance) 
				{
					spells [Spells.Q].Cast (target, true);
				}
			}

			if (useE && spells [Spells.E].IsReady ()) 
			{			
				if (ObjectManager.Player.GetAlliesInRange (spells[Spells.E].Range).Any ()) 
				{
					var bestAlly = (ObjectManager.Player.GetAlliesInRange (spells [Spells.E].Range) 
						.OrderByDescending (x => (x.MagicDamageDealtPlayer + x.PhysicalDamageDealtPlayer) 
							&& x.IsAlly 
							&& !x.HasBuffOfType(BuffType.Invulnerability) 
							&& !x.HasBuffOfType(BuffType.SpellShield)
							&& !x.IsRecalling));
						
					spells[Spells.E].Cast(bestAlly);
				} 
				else 
				{
					spells [Spells.E].Cast (ObjectManager.Player);
				}
			}

			if (useW && spells [Spells.W].IsReady ()) 
			{
				spells [Spells.W].Cast ();
			}

			if (useR && spells [Spells.R].IsReady () 
				&& ObjectManager.Player.CountEnemiesInRange(spells[Spells.W].Range) >= countR
				&& spells[Spells.R].IsInRange(target.ServerPosition)) 
			{
				spells[Spells.R].CastIfHitchanceEquals(target, CustomHitChance, true);
			}
				
			if (ObjectManager.Player.Distance(target) <= 600 && IgniteDamage(target) >= target.Health &&
				useIgnite)
			{
				ObjectManager.Player.Spellbook.CastSpell(_ignite, target);
			}
		}

		#endregion

		#region Intterupt

		private static void Interrupter2_OnInterruptableTarget(Obj_AI_Hero sender, Interrupter2.InterruptableTargetEventArgs args)
		{
			if (args.DangerLevel != Interrupter2.DangerLevel.High || sender.Distance(ObjectManager.Player) > spells[Spells.Q].Range)
				return;

			if (sender.IsValidTarget(spells[Spells.Q].Range) && args.DangerLevel == Interrupter2.DangerLevel.High && spells[Spells.Q].IsReady())
			{
				spells[Spells.Q].Cast(sender);
			}
		}

		#endregion

		#region GapCloser

		private static void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
		{
			if (!gapcloser.Sender.IsValidTarget(spells[Spells.Q].Range))
				return;

			if (gapcloser.Sender.Distance(ObjectManager.Player) > spells[Spells.Q].Range)
				return;


			var useQ = ElNamiMenu._menu.Item ("ElNamiReborn.Interupt.Q").GetValue<bool> ();
			var useR = ElNamiMenu._menu.Item ("ElNamiReborn.Interupt.R").GetValue<bool> ();


			if (gapcloser.Sender.IsValidTarget(spells[Spells.Q].Range))
			{
				if (useQ && spells[Spells.Q].IsReady())
				{
					spells [Spells.Q].Cast (gapcloser.Sender);
				}

				if (useR && !spells[Spells.Q].IsReady() && spells[Spells.R].IsReady())
				{
					spells [Spells.R].Cast (gapcloser.Sender);
				}
			}
		}

		#endregion

		#region IgniteDamage

		static float IgniteDamage(Obj_AI_Base target)
		{
			if (_ignite == SpellSlot.Unknown || ObjectManager.Player.Spellbook.CanUseSpell(_ignite) != SpellState.Ready)
			{
				return 0f;
			}
			return (float) ObjectManager.Player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite);
		}

		#endregion
	}
}

