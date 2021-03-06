﻿using System;
using System.Collections.Generic;
using System.Timers;
using EEMNoRespawnShips.Data.Extensions;
using EEMNoRespawnShips.Data.Helpers;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace EEMNoRespawnShips.Data
{
	public sealed class BotTypeStation : BotBase
	{
		private const string SecurityOnTimerPrefix = "[Alert_On]";

		private const string SecurityOffTimerPrefix = "[Alert_Off]";

		public static readonly BotTypeBase BotType = BotTypeBase.Station;

		private bool WasDamaged => _alertTriggerTime != null;

		private DateTime? _alertTriggerTime;

		private readonly Timer _calmdownTimer = new Timer();

		public BotTypeStation(IMyCubeGrid grid) : base(grid)
		{
		}

		public override bool Init(IMyRemoteControl rc = null)
		{
			if (!base.Init(rc)) return false;
			Update = MyEntityUpdateEnum.EACH_100TH_FRAME;
		   // Alert += OnAlert;
			OnDamaged += DamageHandler;
			OnBlockPlaced += BlockPlacedHandler;

			_calmdownTimer.AutoReset = false;
			_calmdownTimer.Elapsed += (trash1, trash2) =>
			{
				_calmdownTimer.Stop();
				CalmDown();
			};

			return true;
		}

		private void DamageHandler(IMySlimBlock block, MyDamageInformation damage)
		{
			if (block == null) return;
			if (!block.IsDestroyed && damage.IsThruster()) return;
			IMyPlayer damager;
			ReactOnDamage(damage, out damager);
			if (damager != null)
			{
				OnAlert();
			}
		}

		private void OnAlert()
		{
			try
			{
				Grid.DebugWrite("OnAlert", "Alert activated.");
				if (!WasDamaged) Default_SwitchTurretsAndRunTimers(securityState: true);
				_alertTriggerTime = DateTime.Now;
				_calmdownTimer.Start();
			}
			catch (Exception scrap)
			{
				LogError("OnAlert", scrap, "Station.");
			}
		}

		public override void Main()
		{
			//if (WasDamaged && DateTime.Now - AlertTriggerTime > CalmdownTime) CalmDown();
		}

		private void CalmDown()
		{
			try
			{
				Grid.DebugWrite("CalmDown", "Calmdown activated");
				_alertTriggerTime = null;
				Default_SwitchTurretsAndRunTimers(securityState: false);
			}
			catch (Exception scrap)
			{
				Grid.LogError("Calmdown", scrap);
			}
		}

		protected override bool ParseSetup()
		{
			return true;
		}

		private void Default_SwitchTurretsAndRunTimers(bool securityState)
		{
			/*try
			{
				List<IMyLargeTurretBase> Turrets = Term.GetBlocksOfType<IMyLargeTurretBase>();
				foreach (IMyLargeTurretBase Turret in Turrets)
				{
					Turret.SetSecurity_EEM(SecurityState);
				}
			}
			catch (Exception Scrap)
			{
				LogError("SwitchTurrets", Scrap, "StationBot.");
			}*/
			try
			{
				List<IMyTimerBlock> alertTimers = Term.GetBlocksOfType<IMyTimerBlock>
					(x => x.IsWorking && x.CustomName.Contains(securityState ? SecurityOnTimerPrefix : SecurityOffTimerPrefix)
					|| x.CustomData.Contains(securityState ? SecurityOnTimerPrefix : SecurityOffTimerPrefix));

				foreach (IMyTimerBlock timer in alertTimers)
				{
					timer.Trigger();
				}
			}
			catch (Exception scrap)
			{
				LogError("TriggerTimers", scrap, "StationBot.");
			}

			try
			{
				List<IMyRadioAntenna> callerAntennae = Term.GetBlocksOfType<IMyRadioAntenna>
					(x => x.IsWorking && x.CustomData.Contains("Security:CallForHelp"));
				foreach (IMyRadioAntenna antenna in callerAntennae)
				{
					antenna.Enabled = securityState;
				}
			}
			catch (Exception scrap)
			{
				LogError("EnableAntennae", scrap, "StationBot.");
			}

			if (Constants.DebugMode) MyAPIGateway.Utilities.ShowMessage($"{Grid.DisplayName}", $"{(securityState ? "Security Alert!" : "Security calmdown")}");
		}
	}

	// Unfinished
	/*
	public class FactoryManager
	{
		readonly IMyCubeGrid Grid;
		readonly IMyGridTerminalSystem Term;
		List<IMyTerminalBlock> InventoryOwners = new List<IMyTerminalBlock>();
		List<IMyAssembler> Assemblers = new List<IMyAssembler>();
		Dictionary<MyDefinitionId, float> ItemsTotal = new Dictionary<MyDefinitionId, float>();
		Dictionary<MyDefinitionId, float> ItemMinimalQuotas = new Dictionary<MyDefinitionId, float>();

		public FactoryManager(IMyCubeGrid Grid)
		{
			this.Grid = Grid;
			Term = Grid.GetTerminalSystem();
		}

		public void LoadInventoryOwners()
		{
			InventoryOwners = Grid.GetBlocks<IMyTerminalBlock>(x => x.HasInventory);
		}

		void ParseAssemblerQuotas(string Input)
		{
			var CustomData = Input.Trim().Replace("\r\n", "\n").Split('\n');
			foreach (string DataLine in CustomData)
			{
				// Syntax:
				// MinQuota:Type/Subtype:Amount
				if (DataLine.StartsWith("MinQuota"))
				{
					var Data = DataLine.Split(':');
					MyDefinitionId Definition;
					float Quota;
					if (MyDefinitionId.TryParse(Data[1], out Definition) && float.TryParse(Data[2], out Quota))
					{
						if (!ItemMinimalQuotas.ContainsKey(Definition))
							ItemMinimalQuotas.Add(Definition, Quota);
						else
							ItemMinimalQuotas[Definition] += Quota;
					}
				}
			}
		}

		public void SumItems()
		{
			ItemsTotal.Clear();
			foreach (IMyTerminalBlock InventoryOwner in InventoryOwners)
			{
				foreach (IMyInventory Inventory in InventoryOwner.GetInventories())
				{
					foreach (IMyInventoryItem Item in Inventory.GetItems())
					{
						var Blueprint = Item.GetBlueprint();
						if (ItemsTotal.ContainsKey(Blueprint))
							ItemsTotal[Blueprint] += (float)Item.Amount;
						else
							ItemsTotal.Add(Blueprint, (float)Item.Amount);
					}
				}
			}
		}
	}*/
}