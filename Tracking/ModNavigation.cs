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
        public class ModNavigation : StandardModule {

            public ModNavigation( Program program ) : base( program ) { }

            public override int Priority => 10000;

            long selectedEntityId = -1;
            MyDetectedEntityInfo chosenTarget;
            NavigationMode mode;

            List<IMyGyro> gyros = new List<IMyGyro>();

            public override void Initialize() {
                GridTerminalSystem.GetBlocksOfType<IMyGyro>( gyros );
            }

            protected override void Command( string argument ) {
                if(argument.StartsWith( "Navigate" )) {
                    String[] args = argument.Substring( 12 ).Split( ' ' );
                    selectedEntityId = long.Parse( args[0] );
                    Enum.TryParse<NavigationMode>( args[1], out mode );
                }
            }

            protected override void Update() {
                bool hasTarget = selectedEntityId >= 0 && program.trackedEntities.TryGetValue( selectedEntityId, out chosenTarget );

                Vector3 facing = Me.CubeGrid.GridIntegerToWorld( Me.Position + Base6Directions.GetIntVector( Me.Orientation.Forward ) ) - Me.CubeGrid.GridIntegerToWorld( Me.Position );
                facing.Normalize();
                Vector3 facingLeft = Me.CubeGrid.GridIntegerToWorld( Me.Position + Base6Directions.GetIntVector( Me.Orientation.Left ) ) - Me.CubeGrid.GridIntegerToWorld( Me.Position );
                facingLeft.Normalize();
                Vector3 facingUp = Me.CubeGrid.GridIntegerToWorld( Me.Position + Base6Directions.GetIntVector( Me.Orientation.Up ) ) - Me.CubeGrid.GridIntegerToWorld( Me.Position );
                facingUp.Normalize();

                Vector3 desiredSpeed;

                if(!hasTarget) {
                    desiredSpeed = Vector3.Zero;
                } else {
                    Vector3 targetDirection = chosenTarget.Position - Me.CubeGrid.GridIntegerToWorld( Me.Position );
                    targetDirection.Normalize();

                    desiredSpeed = facing.Cross( targetDirection ); //get direction of desired rotation
                    desiredSpeed.Normalize();
                    //Echo("Dd: " + desiredSpeed);
                    desiredSpeed.Multiply( 0.5f - facing.Dot( targetDirection ) / 2 ); //get magnitude of desired rotation
                                                                                       //Echo( "Desired speed: " + desiredSpeed.Length() );
                                                                                       //desiredSpeed = desiredDir - rotSpeed;
                                                                                       //Echo( string.Format( "Facing: {0} TargetDir:{1}", facing, targetDirection ) );
                }

                Vector3 targetRotation = new Vector3( facingLeft.Dot( desiredSpeed ), facingUp.Dot( desiredSpeed ), facing.Dot( desiredSpeed ) );

                //targetRotation = new Vector3( 0,targetRotation.Y,0 ); 

                program.Echo( "TR:   " + targetRotation );

                foreach(var gyro in gyros) {
                    try {
                        gyro.SetValueBool( "Override", true );

                        gyro.SetValueFloat( "Pitch", -targetRotation.Dot( Base6Directions.GetVector( gyro.Orientation.Left ) ) );
                        gyro.SetValueFloat( "Yaw", targetRotation.Dot( Base6Directions.GetVector( gyro.Orientation.Up ) ) );
                        gyro.SetValueFloat( "Roll", -targetRotation.Dot( Base6Directions.GetVector( gyro.Orientation.Forward ) ) );

                    } catch(Exception e) { program.Echo( e.StackTrace ); }
                }

            }

            private float VecAbsDot( Vector3 a, Vector3 b ) {
                return a.X * Math.Abs( b.X ) + a.Y * Math.Abs( b.Y ) + a.Z * Math.Abs( b.Z );
            }

            private float FncLogistic( float x ) {
                if(x > 0.015f || x < 0.015f)
                    return 2 / (1 + (float)Math.Exp( -x )) - 1;
                else
                    return 0;
            }

            public enum NavigationMode {
                HIT
            }

            protected class NavigationCommand {

                public Vector3 desiredSpeed;
                public Vector3 desiredFacing;

            }

             
            
        }

        
    }
}
