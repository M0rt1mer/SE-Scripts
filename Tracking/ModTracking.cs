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
    partial class Program {

        public class ModTracking : Module {

            public ModTracking( Program program ) : base( program ) {}

            public override int Priority => 100000;

            List<IMyCameraBlock> fixedCameras = new List<IMyCameraBlock>();
            List<IMySensorBlock> sensors = new List<IMySensorBlock>();

            Vector3 targetedScan;
            double targetedScanRuntime = 1;

            Random rnd = new Random();

            public override void Initialize() {
                program.GridTerminalSystem.GetBlocksOfType<IMyCameraBlock>( fixedCameras, (x => x.CubeGrid == program.Me.CubeGrid) );
                foreach(var cam in fixedCameras)
                    cam.EnableRaycast = true;
                program.GridTerminalSystem.GetBlocksOfType<IMySensorBlock>( sensors, (x => x.CubeGrid == program.Me.CubeGrid) );
            }

            public override void Main( string arguments, UpdateType type ) {
                if((type & (UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100)) == 0)
                    return;

                try {
                    if(targetedScanRuntime < 1) {
                        targetedScanRuntime += program.Runtime.TimeSinceLastRun.TotalSeconds;
                        float range = (float)targetedScanRuntime * 10000;
                        foreach(var cam in fixedCameras) {
                            if(cam.CanScan( range )) {
                                MyDetectedEntityInfo mde = cam.Raycast( range, targetedScan );
                                if(!mde.IsEmpty()) {
                                    program.RegisterNewSignal( mde, true );
                                    targetedScanRuntime = 1;
                                    program.logMessages.Enqueue( string.Format( "Targeted scan hit {0}", mde.Name ) );
                                }
                            }
                        }
                    }

                    // read sensors
                    foreach(var sensor in sensors)
                        if(!sensor.LastDetectedEntity.IsEmpty())
                            program.RegisterNewSignal( sensor.LastDetectedEntity, true );

                    // ----------------------------- PRIORITY CAMERA SCANNING
                    double[] scanPotential = new double[6];
                    foreach(var cam in fixedCameras)
                        scanPotential[(int)cam.Orientation.Forward] += cam.AvailableScanRange;

                    // set up all priority targets
                    foreach(var signal in program.trackedEntities.Values) {

                    }

                    //update
                    foreach(var signal in program.trackedEntities.Values)
                        if(signal.TimeStamp < program.currentTimestamp)
                            foreach(var cam in fixedCameras)
                                if(cam.CanScan( Vector3.Distance( Me.CubeGrid.GridIntegerToWorld( cam.Position ), signal.Position ) ))
                                    program.RegisterNewSignal( cam.Raycast( signal.Position ), true );

                    //--------------------------------------------- FREE SCAN
                    foreach(var cam in fixedCameras)
                        if(cam.CanScan( PASSIVE_ROAM_DISTANCE ))
                            program.RegisterNewSignal( cam.Raycast( PASSIVE_ROAM_DISTANCE, (float)(rnd.NextDouble() * 2 - 1) * cam.RaycastConeLimit, (float)(rnd.NextDouble() * 2 - 1) * cam.RaycastConeLimit ), true );

                    foreach(var panel in program.debugPanels) {
                        Vector3 facing = Me.CubeGrid.GridIntegerToWorld( Me.Position + Base6Directions.GetIntVector( Me.Orientation.Forward ) )
                                - Me.CubeGrid.GridIntegerToWorld( Me.Position );
                        facing.Normalize();
                        panel.WritePublicText( string.Format( "Facing: {0:0.00},{1:0.00},{2:0.00}\nTS dir: {3:0.00},{4:0.00},{5:0.00}\nTS time:{6:0.00}", facing.X, facing.Y, facing.Z, targetedScan.X, targetedScan.Y, targetedScan.Z, targetedScanRuntime ) );
                    }
                } catch(Exception e) { program.logMessages.Enqueue( e.ToString() ); program.Echo( e.ToString() ); }
                //---------------------------------LOG
                while(program.logMessages.Count > 10)
                    program.logMessages.Dequeue();
                StringBuilder bld = new StringBuilder();
                foreach(string str in program.logMessages)
                    bld.AppendFormat( "{0}\n", str );
                foreach(var logPanel in program.logPanels)
                    logPanel.WritePublicText( bld.ToString() );

                foreach(var panel in program.debugPanels) {
                    Vector3 facing = Me.CubeGrid.GridIntegerToWorld( Me.Position + Base6Directions.GetIntVector( Me.Orientation.Forward ) )
                            - Me.CubeGrid.GridIntegerToWorld( Me.Position );
                    facing.Normalize();
                    panel.WritePublicText( string.Format( "Facing: {0:0.00},{1:0.00},{2:0.00}\nTS dir: {3:0.00},{4:0.00},{5:0.00}\nTS time:{6:0.00}", facing.X, facing.Y, facing.Z, targetedScan.X, targetedScan.Y, targetedScan.Z, targetedScanRuntime ) );
                }
            }

            public override void Check() {
                program.Echo( "ModTracking: " );
                program.Echo( String.Format( "  #Cam: {0}", fixedCameras.Count ) );
            }

            private void MainTargetScan() {
                targetedScanRuntime = 0;
                targetedScan = Me.CubeGrid.GridIntegerToWorld( Me.Position + Base6Directions.GetIntVector( Me.Orientation.Forward ) )
                    - Me.CubeGrid.GridIntegerToWorld( Me.Position );
                targetedScan.Normalize();
                program.logMessages.Enqueue( string.Format( "Initiating targeted scan:  {0:0.00},{1:0.00},{2:0.00}", targetedScan.X, targetedScan.Y, targetedScan.Z ) );
            }
        }
    }
}
