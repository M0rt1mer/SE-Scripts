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
            //List<IMyThrust> thrusters = new List<IMyThrust>();
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
                    String type = argument.Substring( 9 );
                    int split = type.IndexOf( ' ' );
                    String args = type.Substring( split );
                    type = type.Substring( 0, split );
                    if(commandFactoryList.ContainsKey( type ) ) {
                        command = commandFactoryList[type]( args );
                        program.logMessages.Enqueue( "New nav command received" );
                    } else {
                        program.logMessages.Enqueue( "Invalid command for Navigation module: " + type + " | " + args  );
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

                //program.Echo( "TR:   " + targetRotation );

                foreach(var gyro in gyros) {
                    try {
                        gyro.SetValueBool( "Override", true );

                        gyro.SetValueFloat( "Pitch", -targetRotation.Dot( Base6Directions.GetVector( gyro.Orientation.Left ) ) );
                        gyro.SetValueFloat( "Yaw", targetRotation.Dot( Base6Directions.GetVector( gyro.Orientation.Up ) ) );
                        gyro.SetValueFloat( "Roll", -targetRotation.Dot( Base6Directions.GetVector( gyro.Orientation.Forward ) ) );

                    } catch(Exception e) { program.Echo( e.StackTrace ); }
                }

                //TODO: use speed capping to reach desired speed (idea: if desired speed == max speed, use "infinite" magnitude -> will cause desiredSpeedChange to align with desiredSpeed)
                Vector3 desiredSpeedChange = desiredSpeed - program.trackedEntities[Me.EntityId].Velocity; //desired velocity change

                Vector3[] base6ToWorld = new Vector3[] { facing, -facing, facingLeft, -facingLeft, facingUp, -facingUp }; //make use of previous calculations
                float[] potentialPerDirection = new float[6];
                //int activeAxesCount = 0; 
                List<int> activeAxes = new List<int>(); //axes along which we can accelerate
                for(int i = 0; i < 6; i++)
                    if(desiredSpeedChange.Dot( base6ToWorld[i] ) > 0) { // the correct half-space (the one towards the desired target)
                        foreach(IMyThrust thruster in directionalThrusters[i])
                            potentialPerDirection[i] += thruster.MaxEffectiveThrust;
                        if(potentialPerDirection[i] > 0)
                            activeAxes.Add( i );
                    }

                //TODO: make this parameter, or depend on distance - can cause orbiting if target is too close and arc is too wide
                float minCosine = 0.8f; //defines the acceptible arc of inpression for 1- or 2-axis acceleration

                Vector3 desiredSpeedChangeDirection = desiredSpeedChange;
                desiredSpeedChangeDirection.Normalize();

                float[] powerPerAxis = new float[6];
                switch(activeAxes.Count) {
                    case 1: { //only one axis - check angle between axis and desired direction
                            float cosine = desiredSpeedChangeDirection.Dot( base6ToWorld[activeAxes[0]] );
                            if(cosine > minCosine) { //possible
                                powerPerAxis[0] = 1;
                            }
                            break;
                        }
                    case 2: { //two axes - check angle between plane of acceleration (defined by two acceleration axes) and desired direction
                            float sine = desiredSpeedChangeDirection.Dot( base6ToWorld[activeAxes[0]].Cross( base6ToWorld[activeAxes[1]] ) ); //it's sine, because it's Dot with normal
                            float cosine = (float)Math.Sqrt( 1 - (sine * sine) ); // sin^2 + cos^2 = 1
                            if(cosine > minCosine) {

                                float maxPower0 = RightAngleProjection( desiredSpeedChangeDirection, base6ToWorld[activeAxes[0]] * potentialPerDirection[activeAxes[0]] );
                                float maxPower1 = RightAngleProjection( desiredSpeedChangeDirection, base6ToWorld[activeAxes[1]] * potentialPerDirection[activeAxes[1]] );
                                float usablePower = Math.Min( maxPower0, maxPower1 );

                                powerPerAxis[0] = usablePower / maxPower0;
                                powerPerAxis[1] = usablePower / maxPower1;

                            }
                            break;
                        }
                    case 3: { //three axes - any acceleration direction is possible
                            float[] maxPower = new float[3];
                            for(int i = 0; i<3;i++)
                                maxPower[i] = RightAngleProjection( desiredSpeedChangeDirection, base6ToWorld[activeAxes[i]] * potentialPerDirection[activeAxes[i]] );
                            
                            float usablePower = Math.Min( Math.Min( maxPower[0], maxPower[1] ), maxPower[2] );

                            for(int i = 0; i < 3; i++)
                                powerPerAxis[i] = usablePower / maxPower[i];
                            break;
                        }
                }

                for(int i = 0; i < 6; i++) {
                    foreach(IMyThrust thruster in directionalThrusters[i])
                        thruster.ThrustOverridePercentage = powerPerAxis[i];
                }

            }

            /// <summary>
            /// Calculates right-angle projection of arbitrary long thrust vector onto unit-sized direction vector
            ///  |projection| / sin (β) = |thrust| / sin( α )
            ///  where β = 90° and α = 90 - γ (γ beeing angle between thrust and direction, which can be calculated using vector projection)
            ///  |projection| / 1 = |thrust| / sin( 90-γ )
            ///  where sin( 90-γ ) conveniently is cos(γ)
            ///  |projection| = |thrust| / ( direction.thrust / |direction|*|thrust| )
            ///  where |direction| is 1, as it is expected to be unit vector
            ///  |projection| = |thrust| / (direction.thrust / |thrust|)
            ///  |projection| = |thrust|^2 / direction.thrust
            ///  both of which are very cheap to calculate
            /// </summary>
            /// <param name="direction"></param>
            /// <param name="thrust"></param>
            /// <returns></returns>
            private float RightAngleProjection( Vector3 direction, Vector3 thrust ) {
                return thrust.LengthSquared() / direction.Dot( thrust );
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
                    return new NavigationData() { desiredSpeed = Vector3.Zero, desiredFacing = Vector3.Zero };
                }
            }

             
            
        }

        
    }
}
