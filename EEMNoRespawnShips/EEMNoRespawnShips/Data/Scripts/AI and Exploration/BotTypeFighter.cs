﻿using System;
using System.Collections.Generic;
using System.Linq;
using EEMNoRespawnShips.Data.Extensions;
using EEMNoRespawnShips.Data.Helpers;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using IMyRemoteControl = Sandbox.ModAPI.IMyRemoteControl;
using IMySmallGatlingGun = Sandbox.ModAPI.IMySmallGatlingGun;
using IMySmallMissileLauncher = Sandbox.ModAPI.IMySmallMissileLauncher;
using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;
using Ingame = Sandbox.ModAPI.Ingame;


namespace EEMNoRespawnShips.Data
{
	public sealed class BotTypeFighter : BotBase
	{
		public static readonly BotTypeBase BotType = BotTypeBase.Fighter;

		//private bool Damaged { get; set; }

		public bool KeenAiLoaded { get; private set; }

		private FighterSettings _fighterSetup;

		private struct FighterSettings
		{
			public string Preset;
			public bool CallHelpOnDamage;
			public bool AssignToPirates;
			public bool DelayedAiEnable;
			public bool AmbushMode;
			public bool AttackNeutrals;
			public float SeekDistance;
			public float AiActivationDistance;
			//public int PlayerPriority;
			public int CallHelpProbability;

			/// <summary>
			/// Fills out the empty values in struct with default values, and leaves filled values untouched.
			/// </summary>
			public void Default(bool randomizeCallHelp = true)
			{
				if (Preset == default(string)) Preset = "DefaultDirect";
				if (AssignToPirates == default(bool)) { }
				if (AmbushMode == default(bool)) { }
				if (DelayedAiEnable == default(bool)) { }
				if (Math.Abs(SeekDistance - default(float)) < 0) SeekDistance = 10000;
				//if (PlayerPriority == default(int)) PlayerPriority = 10;
				if (CallHelpProbability == default(int)) CallHelpProbability = 100;
				if (CallHelpOnDamage == default(bool) || randomizeCallHelp) RandomizeCallHelp();
			}

			private void RandomizeCallHelp()
			{
				Random rand = new Random();
				int random = rand.Next(0, 101);

				if (random <= CallHelpProbability) CallHelpOnDamage = true;
			}

			public override string ToString()
			{
				return $"Preset='{Preset}|CallHelp={CallHelpOnDamage}|AssignToPirates={AssignToPirates}|SeekDistance={SeekDistance}|AttackNeutrals={AttackNeutrals}";
			}
		}

		public BotTypeFighter(IMyCubeGrid grid) : base(grid)
		{
		}

		public override bool Init(IMyRemoteControl rc = null)
		{
			if (!base.Init(rc)) return false;
			Update |= MyEntityUpdateEnum.EACH_10TH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;

			if (_fighterSetup.CallHelpOnDamage) OnDamaged += DamageHandler;

			if (rc != null)
			{
				rc.Name = DroneNameProvider;
				MyAPIGateway.Entities.SetEntityName(rc);
			}

			if (!_fighterSetup.DelayedAiEnable) LoadKeenAi();
			return true;
		}

		public void LoadKeenAi()
		{
			
			try
			{
				if (KeenAiLoaded) return;
				(Rc as MyRemoteControl)?.SetAutoPilotSpeedLimit(Rc.GetSpeedCap());
				MyVisualScriptLogicProvider.SetDroneBehaviourFull(Rc.Name, presetName: _fighterSetup.Preset, maxPlayerDistance: _fighterSetup.SeekDistance, playerPriority: 0, assignToPirates: _fighterSetup.AssignToPirates);
				if (_fighterSetup.AmbushMode) MyVisualScriptLogicProvider.DroneSetAmbushMode(Rc.Name);
				MyVisualScriptLogicProvider.TargetingSetWhitelist(Rc.Name);
				KeenAiLoaded = true;
			}
			catch (Exception scrap)
			{
				Grid.LogError("LoadKeenAI", scrap);
			}
		}

		private void DamageHandler(IMySlimBlock block, MyDamageInformation damage)
		{
			//if (AiSessionCore.WarDeclared) return;
			//if (Damaged) return;
			//Damaged = true;

			IMyPlayer damager;
			ReactOnDamage(damage, out damager);

			if (_fighterSetup.DelayedAiEnable) LoadKeenAi();

			foreach (IMyTimerBlock timer in Term.GetBlocksOfType<IMyTimerBlock>(collect: x => x.IsFunctional && x.Enabled
			&& x.CustomName.Contains("Damage")))
				timer.Trigger();

			if (!_fighterSetup.CallHelpOnDamage) return;

			foreach (IMyTimerBlock timer in Term.GetBlocksOfType<IMyTimerBlock>(collect: x => x.IsFunctional && x.Enabled
			&& x.CustomName.Contains("Security")))
				timer.Trigger();
		}

		public override void Main()
		{
			if (_fighterSetup.DelayedAiEnable && !KeenAiLoaded) { DelayedAI_Main(); return; }

			List<Ingame.MyDetectedEntityInfo> enemiesInProximity = LookForEnemies(_fighterSetup.SeekDistance, _fighterSetup.AttackNeutrals);
			MyVisualScriptLogicProvider.DroneTargetLoseCurrent(Rc.Name);
			if (enemiesInProximity.Count > 0)
				MyVisualScriptLogicProvider.DroneSetTarget(Rc.Name, GetTopPriorityTarget(enemiesInProximity).GetEntity() as MyEntity);
		}

		private Ingame.MyDetectedEntityInfo GetTopPriorityTarget(List<Ingame.MyDetectedEntityInfo> targets)
		{
			if (targets == null || targets.Count == 0) return new Ingame.MyDetectedEntityInfo();
			if (targets.Count == 1) return targets.First();

			List<Ingame.MyDetectedEntityInfo> mostDangerous;
			if (targets.Any(x => Distance(x) <= 200 && RelSpeed(x) <= 40, out mostDangerous))
				return mostDangerous.OrderBy(Distance).First();

			List<Ingame.MyDetectedEntityInfo> targetsClose = targets.Where(x => Distance(x) <= 1200).ToList();
			if (targetsClose.Count > 0) return targetsClose.OrderBy(x => DangerIndex(x) / x.GetMassT()).First();
			List<Ingame.MyDetectedEntityInfo> targetsFar = targets.Where(x => Distance(x) > 1200).ToList();
			return targetsFar.Count > 0 ? targetsFar.OrderBy(x => DangerIndex(x) / x.GetMassT()).First() : new Ingame.MyDetectedEntityInfo();
		}

		private float DangerIndex(Ingame.MyDetectedEntityInfo enemy)
		{
			if (enemy.Type == Ingame.MyDetectedEntityType.CharacterHuman)
				return Distance(enemy) < 100 ? 100 : 10;
			if (!enemy.IsGrid()) return 0;

			float dangerIndex = 0;
			IMyCubeGrid enemyGrid = enemy.GetGrid();
			//if (MyTrashRemoval.IsTrash(EnemyGrid as MyEntity)) return 0;

			List<IMySlimBlock> enemySlimBlocks = new List<IMySlimBlock>();
			enemyGrid.GetBlocks(enemySlimBlocks, x => x.FatBlock is IMyTerminalBlock);
			List<IMyTerminalBlock> enemyBlocks = enemySlimBlocks.Select(x => x.FatBlock as IMyTerminalBlock).ToList();
			dangerIndex += enemyBlocks.Count(x => x is IMyLargeMissileTurret) * 300;
			dangerIndex += enemyBlocks.Count(x => x is IMyLargeGatlingTurret) * 100;
			dangerIndex += enemyBlocks.Count(x => x is IMySmallMissileLauncher) * 400;
			dangerIndex += enemyBlocks.Count(x => x is IMySmallGatlingGun) * 250;
			dangerIndex += enemyBlocks.Count(x => x is IMyLargeInteriorTurret) * 40;

			if (enemy.Type == Ingame.MyDetectedEntityType.LargeGrid) dangerIndex *= 2.5f;
			return dangerIndex;
		}

		private void DelayedAI_Main()
		{
			try
			{
				if (_fighterSetup.DelayedAiEnable && !KeenAiLoaded && _fighterSetup.AiActivationDistance > 0)
				{
					try
					{
						List<Ingame.MyDetectedEntityInfo> enemiesInProximity = LookForEnemies(_fighterSetup.AiActivationDistance);
						if (enemiesInProximity == null)
							throw new Exception("WEIRD: EnemiesInProximity == null");
						if (enemiesInProximity.Count > 0) LoadKeenAi();
					}
					catch (Exception scrap)
					{
						Grid.LogError("Fighter.DelayedAI.LookForEnemies", scrap);
					}
				}
			}
			catch (Exception scrap)
			{
				Grid.LogError("Fighter.DelayedAI", scrap);
			}
		}

		protected override bool ParseSetup()
		{
			if (ReadBotType(Rc) != BotType) return false;

			List<string> customData = Rc.CustomData.Trim().Replace("\r\n", "\n").Split('\n').ToList();
			foreach (string dataLine in customData)
			{
				if (dataLine.Contains("EEM_AI")) continue;
				if (dataLine.Contains("Type")) continue;
				string[] data = dataLine.Trim().Split(':');
				data[0] = data[0].Trim();
				data[1] = data[1].Trim();

				switch (data[0])
				{
					case "Faction":
						break;
					case "Preset":
						_fighterSetup.Preset = data[1];
						break;
					case "DelayedAI":
						if (!bool.TryParse(data[1], out _fighterSetup.DelayedAiEnable))
						{
							DebugWrite("ParseSetup", "AI setup error: DelayedAI cannot be parsed");
							return false;
						}
						break;
					case "AssignToPirates":
						if (!bool.TryParse(data[1], out _fighterSetup.AssignToPirates))
						{
							DebugWrite("ParseSetup", "AI setup error: AssignToPirates cannot be parsed");
							return false;
						}
						break;
					case "AttackNeutrals":
						if (!bool.TryParse(data[1], out _fighterSetup.AttackNeutrals))
						{
							DebugWrite("ParseSetup", "AI setup error: AttackNeutrals cannot be parsed");
							return false;
						}
						break;
					case "AmbushMode":
						if (!bool.TryParse(data[1], out _fighterSetup.AmbushMode))
						{
							DebugWrite("ParseSetup", "AI setup error: AmbushMode cannot be parsed");
							return false;
						}
						break;
					case "SeekDistance":
						if (!float.TryParse(data[1], out _fighterSetup.SeekDistance))
						{
							DebugWrite("ParseSetup", "AI setup error: SeekDistance cannot be parsed");
							return false;
						}
						break;
					case "ActivationDistance":
						if (!float.TryParse(data[1], out _fighterSetup.AiActivationDistance))
						{
							DebugWrite("ParseSetup", "AI setup error: ActivationDistance cannot be parsed");
							return false;
						}
						break;
					case "PlayerPriority":
						DebugWrite("ParseSetup", "AI setup warning: PlayerPriority is deprecated and no longer used.");
						break;
					case "CallHelpProbability":
						int probability;
						if (!int.TryParse(data[1], out probability))
						{
							DebugWrite("ParseSetup", "AI setup error: CallHelpProbability cannot be parsed");
							return false;
						}
						else if (probability < 0 || probability > 100)
						{
							DebugWrite("ParseSetup", "AI setup error: CallHelpProbability out of bounds. Must be between 0 and 100");
							return false;
						}
						else
						{
							_fighterSetup.CallHelpProbability = probability;
						}
						break;
					case "ThrustMultiplier":
						float multiplier;
						if (!float.TryParse(data[1], out multiplier))
						{
							DebugWrite("ParseSetup", "AI setup error: ThrustMultiplier cannot be parsed");
							return false;
						}
						else
						{
							ApplyThrustMultiplier(multiplier);
						}
						break;
					default:
						DebugWrite("ParseSetup", $"AI setup error: Cannot parse '{dataLine}'");
						return false;
				}
			}
			_fighterSetup.Default();
			return true;
		}
	}
}