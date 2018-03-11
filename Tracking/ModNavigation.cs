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

            INavigationCommand command;

            List<IMyGyro> gyros = new List<IMyGyro>();
            List<IMyThrust> thrusters = new List<IMyThrust>();
            List<IMyThrust>[] directionalThrusters = new List<IMyThrust>[6];

            public override void Initialize() {
                GridTerminalSystem.GetBlocksOfType<IMyGyro>( gyros, (x) => x.CubeGrid == Me.CubeGrid );
                List<IMyThrust> thrusters = new List<IMyThrust>();
                GridTerminalSystem.GetBlocksOfType<IMyThrust>( thrusters, ( x ) => x.CubeGrid == Me.CubeGrid );
                for(int i = 0; i < 6; i++)
                    directionalThrusters[i] = new List<IMyThrust>();
                foreach( IMyThrust thruster in thrusters ) {
                    directionalThrusters[(int)thruster.Orientation.Forward].Add(thruster);
                }
            }

            protected override void Command( string argument ) {
                if(argument.StartsWith( "Navigate" )) {
                    String type = argument.Substring( 12 );
                    String args = type.Substring( argument.IndexOf( ' ' ) );
                    if(commandFactoryList.ContainsKey( type )) {
                        command = commandFactoryList[type]( args );
                    } else {
                        program.logMessages.Enqueue( "Invalid command for Navigation module: "+argument );
                    }
                }
            }

            public void Command( INavigationCommand newCommand ) {
                this.command = newCommand;
            }

            //public void Command( NavigationMode mode,  )

            protected override void Update() {

                Vector3 facing = Me.CubeGrid.GridIntegerToWorld( Me.Position + Base6Directions.GetIntVector( Me.Orientation.Forward ) ) - Me.CubeGrid.GridIntegerToWorld( Me.Position );
                facing.Normalize();
                Vector3 facingLeft = Me.CubeGrid.GridIntegerToWorld( Me.Position + Base6Directions.GetIntVector( Me.Orientation.Left ) ) - Me.CubeGrid.GridIntegerToWorld( Me.Position );
                facingLeft.Normalize();
                Vector3 facingUp = Me.CubeGrid.GridIntegerToWorld( Me.Position + Base6Directions.GetIntVector( Me.Orientation.Up ) ) - Me.CubeGrid.GridIntegerToWorld( Me.Position );
                facingUp.Normalize();

                Vector3 desiredSpeed;

                if( command == null ) {
                    desiredSpeed = Vector3.Zero;
                } else {
                    var navigationData = command.GetNavData( program );
                    //Vector3 targetDirection = chosenTarget.Position - Me.CubeGrid.GridIntegerToWorld( Me.Position );
                    //targetDirection.Normalize();

                    desiredSpeed = facing.Cross( navigationData.desiredFacing.Value ); //get direction of desired rotation
                    desiredSpeed.Normalize();
                    //Echo("Dd: " + desiredSpeed);
                    desiredSpeed.Multiply( 0.5f - facing.Dot( navigationData.desiredFacing.Value ) / 2 ); //get magnitude of desired rotation
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

            public struct NavigationData{
                public Vector3? desiredFacing { get; set; }
                public Vector3? desiredSpeed { get; set; }
            }

            Dictionary<string, Func<string, INavigationCommand>> commandFactoryList = new Dictionary<string, Func<string, INavigationCommand>>() {
                { "HIT", CommHitTarget.Create }
            };

            public interface INavigationCommand {
                NavigationData GetNavData( Program program );
            }

            public class CommHitTarget : INavigationCommand {

                long targetEntity;

                public CommHitTarget( long targetEntity ) {
                    this.targetEntity = targetEntity;
                }

                public static CommHitTarget Create( string argument ) {
                    long id;
                    if( long.TryParse( argument, out id ) )
                        return new CommHitTarget( id );
                    return null;
                }

                public NavigationData GetNavData( Program program ) {
                    MyDetectedEntityInfo targetInfo;
                    if(program.trackedEntities.TryGetValue( targetEntity, out targetInfo )) {
                        Vector3 direction = (program.Me.CubeGrid.GetPosition() - targetInfo.Position);
                        float distance = direction.Normalize();
                        return new NavigationData() { desiredSpeed = direction * 100, desiredFacing = direction };
                    }
                    return new NavigationData() { desiredSpeed = Vector3.Zero };
                }
            }

             
            
        }

        
    }
}
