﻿using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace EEMNoRespawnShips.Data.Helpers
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    // ReSharper disable once ClassNeverInstantiated.Global
    public class CleanEem : MySessionComponentBase
    {
        public override void LoadData()
        {

        }

        private bool _init;
        private int _skip = SkipUpdates;
        private const int SkipUpdates = 100;

        public static int RangeSq = -1;
        public static readonly List<IMyPlayer> Players = new List<IMyPlayer>();
        public static readonly HashSet<IMyCubeGrid> Grids = new HashSet<IMyCubeGrid>();
        public static readonly List<IMySlimBlock> Blocks = new List<IMySlimBlock>(); // never filled

        public void Init()
        {
            _init = true;
            MyAPIGateway.Session.SessionSettings.MaxDrones = Constants.ForceMaxDrones;
        }

        protected override void UnloadData()
        {
            _init = false;

            Players.Clear();
            Grids.Clear();
            Blocks.Clear();
        }

        public override void UpdateBeforeSimulation()
        {
            if (!MyAPIGateway.Multiplayer.IsServer) return; // only server-side/SP

            if (!_init)
            {
                if (MyAPIGateway.Session == null)
                    return;

                Init();
            }

            if (++_skip < SkipUpdates) return;
            try
            {
                _skip = 0;

                // the range used to check player distance from ships before removing them
                RangeSq = Math.Max(MyAPIGateway.Session.SessionSettings.ViewDistance, Constants.CleanupMinRange);
                RangeSq *= RangeSq;

                Players.Clear();
                MyAPIGateway.Players.GetPlayers(Players);

                //if(Constants.CLEANUP_DEBUG)
                //	Log.Info("player list updated; view range updated: " + Math.Round(Math.Sqrt(RangeSq), 1));
            }
            catch (Exception e)
            {
                AiSessionCore.GeneralLog.WriteToLog("CleanEem", $"Exception: {e}");
            }
        }

        public static void GetAttachedGrids(IMyCubeGrid grid)
        {
            Grids.Clear();
            RecursiveGetAttachedGrids(grid);
        }

        private static void RecursiveGetAttachedGrids(IMyCubeGrid grid)
        {
            grid.GetBlocks(Blocks, GetAttachedGridsLoopBlocks);
        }

        private static bool GetAttachedGridsLoopBlocks(IMySlimBlock slim) // should always return false!
        {
            IMyCubeBlock block = slim.FatBlock;

            if (block == null)
                return false;

            //if(Constants.CLEANUP_CONNECTOR_CONNECTED)
            //{
            //	IMyShipConnector connector = block as IMyShipConnector;

            //	if(connector != null)
            //	{
            //		IMyCubeGrid otherGrid = connector.OtherConnector?.CubeGrid;

            //		if(otherGrid != null && !grids.Contains(otherGrid))
            //		{
            //			grids.Add(otherGrid);
            //			RecursiveGetAttachedGrids(otherGrid);
            //		}

            //		return false;
            //	}
            //}

            IMyMotorStator rotorBase = block as IMyMotorStator;

            if (rotorBase != null)
            {
                IMyCubeGrid otherGrid = rotorBase.TopGrid;

                if (otherGrid == null || Grids.Contains(otherGrid)) return false;
                Grids.Add(otherGrid);
                RecursiveGetAttachedGrids(otherGrid);

                return false;
            }

            IMyMotorRotor rotorTop = block as IMyMotorRotor;

            if (rotorTop != null)
            {
                IMyCubeGrid otherGrid = rotorTop.Base?.CubeGrid;

                if (otherGrid == null || Grids.Contains(otherGrid)) return false;
                Grids.Add(otherGrid);
                RecursiveGetAttachedGrids(otherGrid);

                return false;
            }

            IMyPistonBase pistonBase = block as IMyPistonBase;

            if (pistonBase != null)
            {
                IMyCubeGrid otherGrid = pistonBase.TopGrid;

                if (otherGrid == null || Grids.Contains(otherGrid)) return false;
                Grids.Add(otherGrid);
                RecursiveGetAttachedGrids(otherGrid);

                return false;
            }

            IMyPistonTop pistonTop = block as IMyPistonTop;

            if (pistonTop == null) return false;
            {
                IMyCubeGrid otherGrid = pistonTop.Piston?.CubeGrid;
                if (otherGrid == null || Grids.Contains(otherGrid)) return false;
                Grids.Add(otherGrid);
                RecursiveGetAttachedGrids(otherGrid);
                return false;
            }

        }
    }
}