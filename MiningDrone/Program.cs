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


        IMyRemoteControl control;
        IMyTimerBlock timer;

        bool started;
        Vector3 target;
        Vector3 approach;
        int mode;
        Vector2I column;

        //coordinate system
        Vector3 forward;
        Vector3 left;
        Vector3 up;

        //position
        Vector3 lastPos;
        Vector3 speed;


        List<List<IMyThrust>> thrusters = new List<List<IMyThrust>>();

        public void SwitchFlyMode( bool thrusterMode ) {

            if(control.ControlThrusters != thrusterMode)
                control.ApplyAction( "ControlThrusters" );

        }

        //returns a single block of given type
        public T GetBlockOfType<T>() where T : class, IMyTerminalBlock {
            List<T> blks = new List<T>();
            GridTerminalSystem.GetBlocksOfType<T>( blks );
            foreach(var i in blks)
                if(i.CubeGrid == Me.CubeGrid)
                    return i;
            return default( T );
        }

        public void InitThrusters() {
            List<IMyThrust> thrBlks = new List<IMyThrust>();
            GridTerminalSystem.GetBlocksOfType<IMyThrust>( thrBlks );
            for(int i = 0; i < 6; i++)
                thrusters.Add( new List<IMyThrust>() );
            foreach(var thr in thrBlks)
                thrusters[(int)thr.Orientation.Forward].Add( thr );
        }

        public void CreateCoordSystem() {
            forward = target - approach;
            forward.Normalize(); // forward is given
            if(forward.Dot( Vector3.Up ) < 0.01) // if too simillar to Up vector
                up = forward.Cross( Vector3.Left );
            else
                up = forward.Cross( Vector3.Up );
            left = forward.Cross( up );
        }

        public bool TurnForward() {
            control.ClearWaypoints();
            Vector3 controlWorldPos = Me.CubeGrid.GridIntegerToWorld( control.Position );
            control.AddWaypoint( controlWorldPos + forward * 100, "[AUTO]Forward" );
            SwitchFlyMode( false );
            if(!control.IsAutoPilotEnabled)
                control.SetAutoPilotEnabled( true );
            return Vector3.Dot( Me.CubeGrid.GridIntegerToWorld( control.Position + Base6Directions.GetIntVector( control.Orientation.Forward ) ) - controlWorldPos, forward ) < 0.1f;
        }

        public void AdjustDirectionalSpeed( float distDiff, float currentSpeed, int positiveThrusters, int negativeThrusters ) {
            float desiredSpeedDiff = Math.Min( Math.Max( distDiff / 4, -10 ), 10 );
            float desiredThrusterPower = Math.Min( Math.Max( distDiff, -100 ), 100 ); //made up, needs adjusting
            foreach(IMyThrust thr in thrusters[positiveThrusters])
                thr.SetValue<float>( "ThrusterOverride", Math.Max( desiredThrusterPower, 0 ) );
            foreach(IMyThrust thr in thrusters[negativeThrusters])
                thr.SetValue<float>( "ThrusterOverride", Math.Max( -desiredThrusterPower, 0 ) );
        }

        public bool MoveTo( Vector3 target ) {
            Vector3 position = Me.CubeGrid.GridIntegerToWorld( control.Position );
            AdjustDirectionalSpeed( Vector3.Dot( target - position, up ), Vector3.Dot( speed, up ), 4, 5 );
            AdjustDirectionalSpeed( Vector3.Dot( target - position, left ), Vector3.Dot( speed, left ), 2, 3 );
            AdjustDirectionalSpeed( Vector3.Dot( target - position, forward ), Vector3.Dot( speed, forward ), 0, 1 );
            return (position - target).Length() < 0.1f;
        }

        public void Main( string argument ) {

            if(!started) {
                //initialize 
                control = GetBlockOfType<IMyRemoteControl>();
                timer = GetBlockOfType<IMyTimerBlock>();

                string[] parse = argument.Split( ':' ); // two GPS points 

                target = new Vector3( float.Parse( parse[2] ), float.Parse( parse[3] ), float.Parse( parse[4] ) );
                approach = new Vector3( float.Parse( parse[7] ), float.Parse( parse[8] ), float.Parse( parse[9] ) );
                CreateCoordSystem();
                started = true;

                mode = 0; //approach
                lastPos = Me.CubeGrid.GridIntegerToWorld( control.Position );
                speed = new Vector3( 0, 0, 0 );
            } else {
                Vector3 pos = Me.CubeGrid.GridIntegerToWorld( control.Position );
                speed = ((pos - lastPos) / Runtime.TimeSinceLastRun.Milliseconds) * 1000;
            }

            if(mode == 0) { //approach mode
                Echo( "approach" );
                if(Vector3.Distance( Me.CubeGrid.GridIntegerToWorld( control.Position ), approach ) > 0.1) {
                    List<MyWaypointInfo> waypoints = new List<MyWaypointInfo>();
                    control.GetWaypointInfo( waypoints );
                    if(waypoints.Count != 1 || !waypoints[0].Coords.Equals( approach )) {
                        Echo( "clearing approach " + approach );
                        control.ClearWaypoints();
                        control.AddWaypoint( approach, "[AUTO]approach" );
                    }
                    SwitchFlyMode( true );
                    if(!control.IsAutoPilotEnabled)
                        control.SetAutoPilotEnabled( true );
                } else mode = 1;
            }

            if(mode == 1) { //find column
                Random rnd = new Random();
                column = new Vector2I( rnd.Next( -5, 5 ), rnd.Next( -5, 5 ) );
                mode = 2;
            }
            if(mode == 2) { //initalize mining
                if(TurnForward()) {
                    if(MoveTo( approach + column.X * left + column.Y * up )) {
                        mode = 3;
                    }
                }
            }
            if(mode == 3) { //run the column
                if(TurnForward()) {
                    Vector3 currentPos = Me.CubeGrid.GridIntegerToWorld( control.Position );
                    float progressAlongLine = Vector3.Dot( currentPos - approach, forward );
                    if(progressAlongLine < 200)
                        MoveTo( approach + column.X * left + column.Y * up + (progressAlongLine + 2) * forward );
                    else
                        mode = 4;
                }

            }

            if(mode == 4) { //back out
                Vector3 currentPos = Me.CubeGrid.GridIntegerToWorld( control.Position );
                float progressAlongLine = Vector3.Dot( currentPos - approach, forward );
                if(progressAlongLine > 0)
                    MoveTo( approach + column.X * left + column.Y * up + (progressAlongLine - 2) * forward );
                else
                    mode = 5;
            }

            if(mode == 5) { //return home

            }

            timer.ApplyAction( "Start" );
        }




    }
}