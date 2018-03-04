using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;

namespace IngameScript {
    partial class Program : MyGridProgram {

        #region export to game
        private const float PASSIVE_ROAM_DISTANCE = 50;

        List<IMyCameraBlock> fixedCameras = new List<IMyCameraBlock>();
        List<IMySensorBlock> sensors = new List<IMySensorBlock>();

        List<IMyTextPanel> logPanels = new List<IMyTextPanel>();
        List<IMyTextPanel> debugPanels = new List<IMyTextPanel>();
        Queue<string> logMessages = new Queue<string>();
        Random rnd = new Random();
        Dictionary<long, MyDetectedEntityInfo> trackedEntities = new Dictionary<long, MyDetectedEntityInfo>();
        List<FilteredOutput> filteredOutputs = new List<FilteredOutput>();

        IMyTimerBlock restarter;

        Vector3 targetedScan;
        double targetedScanRuntime = 1;

        long currentTimestamp = -1;
        Vector3 lastPosition = Vector3.Zero;

        public Program() {
            GridTerminalSystem.GetBlocksOfType<IMyCameraBlock>( fixedCameras, (x => x.CubeGrid == Me.CubeGrid) );

            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>( logPanels, (x => x.CubeGrid == Me.CubeGrid && x.CustomName.StartsWith( "[TL]" )) );
            foreach(var panel in logPanels)
                panel.WritePublicText( "" );
            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>( debugPanels, (x => x.CubeGrid == Me.CubeGrid && x.CustomName.StartsWith( "[TDBG]" )) );
            foreach(var panel in debugPanels)
                panel.WritePublicText( "" );
            GridTerminalSystem.GetBlocksOfType<IMySensorBlock>( sensors, (x => x.CubeGrid == Me.CubeGrid) );
            foreach(var cam in fixedCameras)
                cam.EnableRaycast = true;
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

            List<IMyProgrammableBlock> pbs = new List<IMyProgrammableBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>( pbs, (x => x.CubeGrid == Me.CubeGrid && x.CustomName.StartsWith( "[TR]" )) );
            foreach(IMyProgrammableBlock pb in pbs) {
                filteredOutputs.Add( new ProgrammableBlockOutput( pb ) );
                logMessages.Enqueue( string.Format( "Registering {0} as output PB", pb.CustomName ) );
            }

            List<IMyTimerBlock> restarters = new List<IMyTimerBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyTimerBlock>( restarters, (x => x.CubeGrid == Me.CubeGrid && x.CustomName.StartsWith( "[TR]" )) );
            this.restarter = restarters[0];

            logMessages.Enqueue( "Initiating system" );
        }

        public void Main( string argument ) {
            switch(argument) {
                case "CheckSystem":
                    MainCheckSystem();
                    break;
                case "TargetedScan":
                    MainTargetScan();
                    break;
                case "Update":
                    MainUpdate();
                    break;
                default:
                    MainSignal( argument );
                    break;
            }
        }

        private void MainUpdate() {
            try {

                if(currentTimestamp > 0)
                    currentTimestamp += (int)Runtime.TimeSinceLastRun.TotalMilliseconds;

                if(targetedScanRuntime < 1) {
                    targetedScanRuntime += Runtime.TimeSinceLastRun.TotalSeconds;
                    float range = (float)targetedScanRuntime * 10000;
                    foreach(var cam in fixedCameras) {
                        if(cam.CanScan( range )) {
                            MyDetectedEntityInfo mde = cam.Raycast( range, targetedScan );
                            if(!mde.IsEmpty()) {
                                RegisterNewSignal( mde, true );
                                targetedScanRuntime = 1;
                                logMessages.Enqueue( string.Format( "Targeted scan hit {0}", mde.Name ) );
                            }
                        }
                    }
                }

                // track self
                Vector3 speed = lastPosition == Vector3.Zero ? Vector3.Zero : Me.Position - lastPosition;
                Sandbox.ModAPI.Ingame.MyDetectedEntityType thisEntType = Me.CubeGrid.GridSize == 0 ? Sandbox.ModAPI.Ingame.MyDetectedEntityType.LargeGrid : Sandbox.ModAPI.Ingame.MyDetectedEntityType.SmallGrid;
                RegisterNewSignal( new MyDetectedEntityInfo( Me.EntityId, "UNKNOWN", thisEntType, null, Me.WorldMatrix, speed, MyRelationsBetweenPlayerAndBlock.Owner, Me.WorldAABB, currentTimestamp < 0 ? 1 : currentTimestamp ), false );

                // read sensors
                foreach(var sensor in sensors)
                    if(!sensor.LastDetectedEntity.IsEmpty())
                        RegisterNewSignal( sensor.LastDetectedEntity, true );

                // ----------------------------- PRIORITY CAMERA SCANNING

                double[] scanPotential = new double[6];
                foreach(var cam in fixedCameras)
                    scanPotential[(int)cam.Orientation.Forward] += cam.AvailableScanRange;

                // set up all priority targets
                foreach(var signal in trackedEntities.Values) {

                }




                //update
                foreach(var signal in trackedEntities.Values)
                    if(signal.TimeStamp < currentTimestamp)
                        foreach(var cam in fixedCameras)
                            if(cam.CanScan( Vector3.Distance( Me.CubeGrid.GridIntegerToWorld( cam.Position ), signal.Position ) ))
                                RegisterNewSignal( cam.Raycast( signal.Position ), true );

                //--------------------------------------------- FREE SCAN
                foreach(var cam in fixedCameras)
                    if(cam.CanScan( PASSIVE_ROAM_DISTANCE ))
                        RegisterNewSignal( cam.Raycast( PASSIVE_ROAM_DISTANCE, (float)(rnd.NextDouble() * 2 - 1) * cam.RaycastConeLimit, (float)(rnd.NextDouble() * 2 - 1) * cam.RaycastConeLimit ), true );

                foreach(var output in filteredOutputs)
                    output.Output( trackedEntities.Values, currentTimestamp );

                if(restarter == null)
                    logMessages.Enqueue( "No restarter block found" );
                else
                    restarter.ApplyAction( "TriggerNow" );
            } catch(Exception e) { logMessages.Enqueue( e.ToString() ); Echo( e.ToString() ); }
            //---------------------------------LOG
            while(logMessages.Count > 10)
                logMessages.Dequeue();
            StringBuilder bld = new StringBuilder();
            foreach(string str in logMessages)
                bld.AppendFormat( "{0}\n", str );
            foreach(var logPanel in logPanels)
                logPanel.WritePublicText( bld.ToString() );

            foreach(var panel in debugPanels) {
                Vector3 facing = Me.CubeGrid.GridIntegerToWorld( Me.Position + Base6Directions.GetIntVector( Me.Orientation.Forward ) )
                        - Me.CubeGrid.GridIntegerToWorld( Me.Position );
                facing.Normalize();
                panel.WritePublicText( string.Format( "Facing: {0:0.00},{1:0.00},{2:0.00}\nTS dir: {3:0.00},{4:0.00},{5:0.00}\nTS time:{6:0.00}", facing.X, facing.Y, facing.Z, targetedScan.X, targetedScan.Y, targetedScan.Z, targetedScanRuntime ) );
            }
        }

        private void MainCheckSystem() {
            Echo( String.Format( "#Cam: {0}", fixedCameras.Count ) );
            Echo( String.Format( "#output: {0}", filteredOutputs.Count ) );
        }

        private void MainSignal( string signal ) {
            foreach(var mdei in StringToMDEIs( signal ))
                RegisterNewSignal( mdei, false );
        }

        private void MainTargetScan() {
            targetedScanRuntime = 0;
            targetedScan = Me.CubeGrid.GridIntegerToWorld( Me.Position + Base6Directions.GetIntVector( Me.Orientation.Forward ) )
                - Me.CubeGrid.GridIntegerToWorld( Me.Position );
            targetedScan.Normalize();
            logMessages.Enqueue( string.Format( "Initiating targeted scan:  {0:0.00},{1:0.00},{2:0.00}", targetedScan.X, targetedScan.Y, targetedScan.Z ) );
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

        private struct PrioritizedScan {
            Vector3 position;
            float priority;
        }

        abstract class FilteredOutput {

            public int relationshipFlags = 0;
            public int entityTypeFlags = 0;
            public bool fresh = false;

            public FilteredOutput( string str ) {
                string[] filters = str.Split( ',' );
                foreach(string flt in filters) {
                    try {
                        MyDetectedEntityType entType = (MyDetectedEntityType)Enum.Parse( typeof( MyDetectedEntityType ), flt );
                        entityTypeFlags |= 1 << (int)entType;
                    } catch(ArgumentException e) { }
                    try {
                        MyRelationsBetweenPlayerAndBlock entType = (MyRelationsBetweenPlayerAndBlock)Enum.Parse( typeof( MyRelationsBetweenPlayerAndBlock ), flt );
                        relationshipFlags |= 1 << (int)entType;
                    } catch(ArgumentException e) { }
                    if(flt.Equals( "Fresh" ))
                        fresh = true;
                }
            }

            public bool Filter( MyDetectedEntityInfo mdei, long currentTimestamp ) {
                if(relationshipFlags > 0 && (relationshipFlags & (1 << (int)mdei.Relationship)) == 0)
                    return false;
                if(entityTypeFlags > 0 && (entityTypeFlags & (1 << (int)mdei.Type)) == 0)
                    return false;
                if(fresh && currentTimestamp != mdei.TimeStamp)
                    return false;
                return true;
            }

            public void Output( IEnumerable<MyDetectedEntityInfo> signals, long currentTimestamp ) {
                StringBuilder outText = new StringBuilder();
                foreach(var signal in signals) {
                    if(Filter( signal, currentTimestamp ))
                        outText.Append( MDEIToString( signal ) );
                }
                Send( outText.ToString() );
            }

            protected abstract void Send( string text );

            public string MDEIToString( MyDetectedEntityInfo mdei ) {
                return string.Format( "{0}:{1}:{2:0.00},{3:0.00},{4:0.00};{5:0.00},{6:0.00},{7:0.00}@{8}\n", mdei.EntityId, nameConverter.Replace( mdei.Name, "" ), mdei.BoundingBox.Min.X, mdei.BoundingBox.Min.Y, mdei.BoundingBox.Min.Z, mdei.BoundingBox.Max.X, mdei.BoundingBox.Max.Y, mdei.BoundingBox.Max.Z, mdei.TimeStamp );
            }

            public System.Text.RegularExpressions.Regex nameConverter = new System.Text.RegularExpressions.Regex( @"[^\w\d\s]*" );
        }

        class PanelOutput : FilteredOutput {
            IMyTextPanel panel;
            public PanelOutput( IMyTextPanel panel ) : base( panel.CustomData ) {
                this.panel = panel;
            }
            protected override void Send( string text ) {
                panel.WritePublicText( text );
            }
        }
        class AntennaOutput : FilteredOutput {
            IMyRadioAntenna antenna;
            public AntennaOutput( IMyRadioAntenna antenna ) : base( antenna.CustomData ) {
                this.antenna = antenna;
            }
            protected override void Send( string text ) {
                antenna.TransmitMessage( text );
            }
        }
        class LaserAntennaOutput : FilteredOutput {
            IMyLaserAntenna antenna;
            public LaserAntennaOutput( IMyLaserAntenna antenna ) : base( antenna.CustomData ) {
                this.antenna = antenna;
            }
            protected override void Send( string text ) {
                antenna.TransmitMessage( text );
            }
        }
        class ProgrammableBlockOutput : FilteredOutput {
            IMyProgrammableBlock blk;
            public ProgrammableBlockOutput( IMyProgrammableBlock pb ) : base( pb.CustomData ) {
                this.blk = pb;
            }
            protected override void Send( string text ) {
                blk.TryRun( text );
            }
        }

        #region readTracking

        public System.Text.RegularExpressions.Regex mdeiParser = new System.Text.RegularExpressions.Regex( @"([0-9]*):([\w\d\s]*):(-?[0-9]*\.[0-9]*),(-?[0-9]*\.[0-9]*),(-?[0-9]*\.[0-9]*);(-?[0-9]*\.[0-9]*),(-?[0-9]*\.[0-9]*),(-?[0-9]*\.[0-9]*)@([0-9]*)" );
        public MyDetectedEntityInfo? StringToMDEI( string str ) {
            System.Text.RegularExpressions.Match match = mdeiParser.Match( str );
            if(match.Value != String.Empty) {
                BoundingBoxD bb = new BoundingBoxD();
                bb.Min.X = float.Parse( match.Groups[3].Value );
                bb.Min.Y = float.Parse( match.Groups[4].Value );
                bb.Min.Z = float.Parse( match.Groups[5].Value );
                bb.Max.X = float.Parse( match.Groups[6].Value );
                bb.Max.Y = float.Parse( match.Groups[7].Value );
                bb.Max.Z = float.Parse( match.Groups[8].Value );
                string name = match.Groups[2].Value;
                long entityId = long.Parse( match.Groups[1].Value );
                long timestamp = long.Parse( match.Groups[9].Value );
                return new MyDetectedEntityInfo( entityId, name, Sandbox.ModAPI.Ingame.MyDetectedEntityType.Asteroid, null, MatrixD.Identity, Vector3.Zero, MyRelationsBetweenPlayerAndBlock.Enemies, bb, timestamp );
            }
            return null;
        }
        public IEnumerable<MyDetectedEntityInfo> StringToMDEIs( string str ) {
            List<MyDetectedEntityInfo> list = new List<MyDetectedEntityInfo>();
            foreach(string line in str.Split( new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries )) {
                MyDetectedEntityInfo? mdei = StringToMDEI( line.Trim( '\r', '\n' ) );
                if(mdei.HasValue)
                    list.Add( mdei.Value );
            }
            return list;
        }

        #endregion

        #endregion

    }
}