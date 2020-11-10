using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game;
using VRageMath;

namespace IngameScript {
  /**
   * Main class of the script, handles basic tracking, device management, communication
   */
    partial class Program : MyGridProgram {

        private const float PASSIVE_ROAM_DISTANCE = 50;

        List<IMyTextPanel> logPanels = new List<IMyTextPanel>();
        Queue<string> logMessages = new Queue<string>();

        #region DEBUG
        List<IMyTextPanel> debugPanels = new List<IMyTextPanel>();

        List<IMyRadioAntenna> debugAntennae = new List<IMyRadioAntenna>();
        List<string> antennaDebug = new List<string>();
        #endregion

        readonly List<IMyShipController> controllers = new List<IMyShipController>();

        readonly Dictionary<long, MyDetectedEntityInfo> trackedEntities = new Dictionary<long, MyDetectedEntityInfo>();
        readonly List<FilteredOutput> filteredOutputs = new List<FilteredOutput>();

        long currentTimestamp = -1;
        public Vector3 myCenterOfMassWorld;
        //angle speeds in Radians
        public Vector3 myAngleSpeeds;

        public Program() {
          FindComponents();
          Runtime.UpdateFrequency = UpdateFrequency.Once | UpdateFrequency.Update1;

          logMessages.Enqueue( "Initiating system" );
          instance = this;
        }

        public static Program instance;

        void FindComponents()
        {
          GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(logPanels, (x => x.CubeGrid == Me.CubeGrid && x.CustomName.StartsWith("[TL]")));
          foreach (var panel in logPanels)
            panel.WritePublicText("");
          GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(debugPanels, (x => x.CubeGrid == Me.CubeGrid && x.CustomName.StartsWith("[TDBG]")));
          foreach (var panel in debugPanels)
            panel.WritePublicText("");
          //OUTPUTS
          List<IMyTextPanel> output = new List<IMyTextPanel>();
          GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(output, (x => x.CubeGrid == Me.CubeGrid && x.CustomName.StartsWith("[TR]")));
          foreach (var panel in output)
          {
            filteredOutputs.Add(new PanelOutput(panel));
            logMessages.Enqueue(string.Format("Registering {0} as output panel", panel.CustomName));
            panel.WritePublicText("");
          }

          filteredOutputs.Add(new AntennaOutput(this));

          GridTerminalSystem.GetBlocksOfType(debugAntennae, (x => x.CubeGrid == Me.CubeGrid && x.CustomData.StartsWith("[TDBG]")));

          /*List<IMyProgrammableBlock> pbs = new List<IMyProgrammableBlock>();
          GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>(pbs, (x => x.CubeGrid == Me.CubeGrid && x.CustomName.StartsWith("[TR]")));
          foreach (IMyProgrammableBlock pb in pbs)
          {
            filteredOutputs.Add(new ProgrammableBlockOutput(pb));
            logMessages.Enqueue(string.Format("Registering {0} as output PB", pb.CustomName));
          }*/

          GridTerminalSystem.GetBlocksOfType<IMyShipController>(controllers);
        }

        public void Main( string argument, UpdateType updateType ) {
            switch(updateType) {
                case UpdateType.Terminal: {
                        if(argument.Equals( "CheckSystem" ))
                            MainCheckSystem();
                        else
                            ModuleUpdate( argument, updateType );
                        break;
                    }
                case UpdateType.Once: {
                        ModuleIntialize( Me.CustomData );
                        break;
                    }
                case UpdateType.Update100:
                        FindComponents();
                        goto case UpdateType.Update1;
                case UpdateType.Update1:
                case UpdateType.Update10:{
                        MainUpdate(argument,updateType);
                        break;
                    }
                case UpdateType.IGC: {
                        MainSignal( argument );
                        ModuleUpdate( argument, updateType );
                        break;
                    }
                default: {
                        logMessages.Enqueue("Wrong argument/updatetype");
                        break;
                    }
            }
        }

        MyDetectedEntityInfo GetMyMDEI() => trackedEntities[Me.EntityId];

        Vector3 lastPosition = Vector3.Zero;
        private void MainUpdate( string arguments, UpdateType upd ) {
            try {

                //faking current timestamp
                if(currentTimestamp > 0)
                    currentTimestamp += (int)Runtime.TimeSinceLastRun.TotalMilliseconds;

                Vector3 speed;
                // track self
                if(controllers.Count > 0) {
                    //PRECISE TRACKING
                    var speeds = controllers[0].GetShipVelocities();
                    speed = speeds.LinearVelocity;
                    myCenterOfMassWorld = controllers[0].CenterOfMass;
                    myAngleSpeeds = speeds.AngularVelocity / 180 * (float)Math.PI;
                } else {
                    // estimate tracking
                    speed = lastPosition == Vector3.Zero ? Vector3.Zero : Me.Position - lastPosition;
                }
                Sandbox.ModAPI.Ingame.MyDetectedEntityType thisEntType = Me.CubeGrid.GridSize == 0 ? Sandbox.ModAPI.Ingame.MyDetectedEntityType.LargeGrid : Sandbox.ModAPI.Ingame.MyDetectedEntityType.SmallGrid;
                RegisterNewSignal( new MyDetectedEntityInfo( Me.EntityId, Me.CubeGrid.CustomName, thisEntType, null, Me.WorldMatrix, speed, MyRelationsBetweenPlayerAndBlock.Owner, Me.WorldAABB, currentTimestamp < 0 ? 1 : currentTimestamp ), false );

                // update modules

                ModuleUpdate( arguments, upd );

                //output tracking info
                foreach(var output in filteredOutputs)
                    output.Output( trackedEntities.Values, currentTimestamp );

            } catch(Exception e) { logMessages.Enqueue( e.ToString() ); Echo( e.ToString() ); }

            //---------------------------------LOG
            while(logMessages.Count > 16)
                logMessages.Dequeue();
            StringBuilder bld = new StringBuilder();
            foreach(string str in logMessages)
                bld.AppendFormat( "{0}\n", str );
            foreach(var logPanel in logPanels)
                logPanel.WritePublicText( bld.ToString() );

            DebugPrintModules();
            Echo( string.Format( "@{0}, took {1:0.0} ms", currentTimestamp, Runtime.LastRunTimeMs ) );
            Echo( string.Format( "{0:00000} / {1} instructions", Runtime.CurrentInstructionCount, Runtime.MaxInstructionCount ) );
            Echo( string.Format( "{0} output devices", filteredOutputs.Count ) );

            if(antennaDebug.Count > 0) {
                string chosenString = antennaDebug[(int)(currentTimestamp / 20L) % antennaDebug.Count];
                string resultString = string.Join( " ", antennaDebug );
                Echo( resultString );

                antennaDebug.Clear();
                Echo( " " + debugAntennae.Count );
                foreach(var antenna in debugAntennae) {
                    antenna.CustomName = resultString;
                }
            }
        }

        private void MainCheckSystem() {
            Echo( String.Format( "#output: {0}", filteredOutputs.Count ) );
            ModuleCheck();
        }

        private void MainSignal( string signal ) {
            foreach(var mdei in StringToMDEIs( signal ))
                RegisterNewSignal( mdei, false );
        }

        private void RegisterNewSignal( MyDetectedEntityInfo mdei, bool isTrueAndFresh ) {
            if(mdei.IsEmpty())
                return;
            if(!trackedEntities.ContainsKey( mdei.EntityId ) || trackedEntities[mdei.EntityId].TimeStamp < mdei.TimeStamp)
                trackedEntities[mdei.EntityId] = mdei;
            if(isTrueAndFresh || mdei.TimeStamp > currentTimestamp) {
                currentTimestamp = mdei.TimeStamp;
                long diff = (currentTimestamp - mdei.TimeStamp);
                if(diff != 0)
                    logMessages.Enqueue( "Diff: " + diff );
            }
        }

        public void DebugWithAntenna(string debugString) {
            antennaDebug.Add( debugString );
        }

        public void Log(string msg) => logMessages.Enqueue(msg);

        private struct PrioritizedScan {
            Vector3 position;
            float priority;
        }

    }
}