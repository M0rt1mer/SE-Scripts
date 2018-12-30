using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game;
using VRageMath;

namespace IngameScript {
    partial class Program : MyGridProgram {

        private const float PASSIVE_ROAM_DISTANCE = 50;

        List<IMyTextPanel> logPanels = new List<IMyTextPanel>();
        Queue<string> logMessages = new Queue<string>();

        #region DEBUG
        List<IMyTextPanel> debugPanels = new List<IMyTextPanel>();

        List<IMyRadioAntenna> debugAntennae = new List<IMyRadioAntenna>();
        List<string> antennaDebug = new List<string>();
        #endregion



        Dictionary<long, MyDetectedEntityInfo> trackedEntities = new Dictionary<long, MyDetectedEntityInfo>();
        List<FilteredOutput> filteredOutputs = new List<FilteredOutput>();

        long currentTimestamp = -1;


        public Program() {

            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>( logPanels, (x => x.CubeGrid == Me.CubeGrid && x.CustomName.StartsWith( "[TL]" )) );
            foreach(var panel in logPanels)
                panel.WritePublicText( "" );
            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>( debugPanels, (x => x.CubeGrid == Me.CubeGrid && x.CustomName.StartsWith( "[TDBG]" )) );
            foreach(var panel in debugPanels)
                panel.WritePublicText( "" );
            //OUTPUTS
            List<IMyTextPanel> output = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>( output, (x => x.CubeGrid == Me.CubeGrid && x.CustomName.StartsWith( "[TR]" )) );
            foreach(var panel in output) {
                filteredOutputs.Add( new PanelOutput( panel ) );
                logMessages.Enqueue( string.Format( "Registering {0} as output panel", panel.CustomName ) );
                panel.WritePublicText( "" );
            }

            List<IMyLaserAntenna> laserAntennas = new List<IMyLaserAntenna>();
            GridTerminalSystem.GetBlocksOfType<IMyLaserAntenna>( laserAntennas, (x => x.CubeGrid == Me.CubeGrid && x.CustomName.StartsWith( "[TR]" )) );
            foreach(IMyLaserAntenna antenna in laserAntennas) {
                filteredOutputs.Add( new LaserAntennaOutput( antenna ) );
                logMessages.Enqueue( string.Format( "Registering {0} as output antenna", antenna.CustomName ) );
            }

            List<IMyRadioAntenna> radioAntennas = new List<IMyRadioAntenna>();
            GridTerminalSystem.GetBlocksOfType<IMyRadioAntenna>( radioAntennas, (x => x.CubeGrid == Me.CubeGrid && x.CustomName.StartsWith( "[TR]" )) );
            foreach(IMyRadioAntenna antenna in radioAntennas) {
                filteredOutputs.Add( new AntennaOutput( antenna ) );
                logMessages.Enqueue( string.Format( "Registering {0} as output antenna", antenna.CustomName ) );
            }

            GridTerminalSystem.GetBlocksOfType( debugAntennae, (x => x.CubeGrid == Me.CubeGrid && x.CustomData.StartsWith( "[TDBG]" )) );

            List<IMyProgrammableBlock> pbs = new List<IMyProgrammableBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>( pbs, (x => x.CubeGrid == Me.CubeGrid && x.CustomName.StartsWith( "[TR]" )) );
            foreach(IMyProgrammableBlock pb in pbs) {
                filteredOutputs.Add( new ProgrammableBlockOutput( pb ) );
                logMessages.Enqueue( string.Format( "Registering {0} as output PB", pb.CustomName ) );
            }

            Runtime.UpdateFrequency = UpdateFrequency.Once | UpdateFrequency.Update1;

            logMessages.Enqueue( "Initiating system" );
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
                case UpdateType.Update1:
                case UpdateType.Update10:
                case UpdateType.Update100: {
                        MainUpdate(argument,updateType);
                        break;
                    }
                case UpdateType.Antenna: {
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

        Vector3 lastPosition = Vector3.Zero;
        private void MainUpdate( string arguments, UpdateType upd ) {
            try {

                //faking current timestamp
                if(currentTimestamp > 0)
                    currentTimestamp += (int)Runtime.TimeSinceLastRun.TotalMilliseconds;

                // track self
                Vector3 speed = lastPosition == Vector3.Zero ? Vector3.Zero : Me.Position - lastPosition;
                Sandbox.ModAPI.Ingame.MyDetectedEntityType thisEntType = Me.CubeGrid.GridSize == 0 ? Sandbox.ModAPI.Ingame.MyDetectedEntityType.LargeGrid : Sandbox.ModAPI.Ingame.MyDetectedEntityType.SmallGrid;
                RegisterNewSignal( new MyDetectedEntityInfo( Me.EntityId, Me.CubeGrid.CustomName, thisEntType, null, Me.WorldMatrix, speed, MyRelationsBetweenPlayerAndBlock.Owner, Me.WorldAABB, currentTimestamp < 0 ? 1 : currentTimestamp ), false );

                // update modules

                ModuleUpdate( arguments, upd );

                //output tracking info
                foreach(var output in filteredOutputs)
                    output.Output( trackedEntities.Values, currentTimestamp );

            } catch(Exception e) { logMessages.Enqueue( e.ToString() ); Echo( e.ToString() ); }
            //---------------------------------LOG
            while(logMessages.Count > 10)
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

        private struct PrioritizedScan {
            Vector3 position;
            float priority;
        }

    }
}