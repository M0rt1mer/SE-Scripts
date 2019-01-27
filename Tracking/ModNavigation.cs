using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Generic;
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
            //Matrix3x3 directionAdjustMatrix;

            bool useControlBlock = false;

            public override void Initialize( IEnumerable<string> arguments ) {
                GridTerminalSystem.GetBlocksOfType<IMyGyro>( gyros, (x) => x.CubeGrid == Me.CubeGrid );
                List<IMyThrust> thrusters = new List<IMyThrust>();
                GridTerminalSystem.GetBlocksOfType<IMyThrust>( thrusters, ( x ) => x.CubeGrid == Me.CubeGrid );
                for(int i = 0; i < 6; i++)
                    directionalThrusters[i] = new List<IMyThrust>();
                foreach( IMyThrust thruster in thrusters ) {
                    directionalThrusters[(int)thruster.Orientation.Forward].Add(thruster);
                }
                foreach(string argument in arguments) {
                    if(argument.Equals( "remoteControl" )) {
                        useControlBlock = true;
                    }
                }
                    /*  directionAdjustMatrix = Matrix3x3.Identity; //basic matrix created from this block
                      foreach(string argument in arguments) {
                          Base6Directions.Direction newForward = Base6Directions.Direction.Forward;
                          if( Enum.TryParse<Base6Directions.Direction>( argument, out newForward ) )
                              directionAdjustMatrix = Matrix3x3.CreateFromQuaternion( Quaternion.CreateFromTwoVectors( Base6Directions.GetVector( newForward ), Vector3.Forward ) );
                      }*/
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


            private void HandleFacing( TempData tempData ) {
                Vector3 desiredRotSpeed;

                if(command == null) {
                    desiredRotSpeed = Vector3.Zero;
                } else {
                    var navigationData = command.GetNavData( program );
                    //Vector3 targetDirection = chosenTarget.Position - Me.CubeGrid.GridIntegerToWorld( Me.Position );
                    //targetDirection.Normalize();
                    Vector3 des = navigationData.desiredForward.Value;
                    //VrageMath doesn't have Vector3 * Matrix3x3 multiplication
                    //navigationData.desiredFacing = new Vector3( Vector3.Dot(des, directionAdjustMatrix.Col0), Vector3.Dot( des, directionAdjustMatrix.Col1 ), Vector3.Dot( des, directionAdjustMatrix.Col2 ) );

                    List<IMyRadioAntenna> antenna = new List<IMyRadioAntenna>();
                    GridTerminalSystem.GetBlocksOfType<IMyRadioAntenna>( antenna );
                    antenna[0].CustomName = "Desired Facing: " + navigationData.desiredForward;

                    desiredRotSpeed = tempData.facing.Cross( navigationData.desiredForward.Value ); //get direction of desired rotation
                    desiredRotSpeed.Normalize();
                    //Echo("Dd: " + desiredSpeed);
                    desiredRotSpeed.Multiply( 0.5f - tempData.facing.Dot( navigationData.desiredForward.Value ) / 2 ); //get magnitude of desired rotation
                                                                                                                      //Echo( "Desired speed: " + desiredSpeed.Length() );
                                                                                                                      //desiredSpeed = desiredDir - rotSpeed;
                                                                                                                      //Echo( string.Format( "Facing: {0} TargetDir:{1}", facing, targetDirection ) );
                }

                Vector3 targetRotation = new Vector3( tempData.facingLeft.Dot( desiredRotSpeed ), tempData.facingUp.Dot( desiredRotSpeed ), tempData.facing.Dot( desiredRotSpeed ) );


                //targetRotation = new Vector3( 0,targetRotation.Y,0 ); 

                //program.Echo( "TR:   " + targetRotation );

                if(useControlBlock) {
                    List<IMyRemoteControl> control = new List<IMyRemoteControl>();
                    GridTerminalSystem.GetBlocksOfType<IMyRemoteControl>( control );
                    if(shouldSetWaypoint == 0) {
                        if(control.Count > 0) {
                            IMyRemoteControl cntrl = control[0];
                            cntrl.ControlThrusters = false;
                            cntrl.ControlWheels = true;
                            cntrl.ClearWaypoints();
                            var desiredFacing = command.GetNavData( program ).desiredForward.Value;
                            cntrl.AddWaypoint( Me.CubeGrid.GridIntegerToWorld( cntrl.Position ) + desiredFacing * 10000, "dummy rotation target" );
                            cntrl.AddWaypoint( new MyWaypointInfo() );
                            cntrl.SetAutoPilotEnabled( true );
                            Vector3 target = Me.CubeGrid.GridIntegerToWorld( cntrl.Position ) + (desiredFacing * 1000);
                            program.Echo( string.Format( "RC: {0:0.0} {1:0.0} {2:0.0}", target.X, target.Y, target.Z ) );
                        } else
                            program.Echo( "Can't find any control blocks" );
                        shouldSetWaypoint = 50;
                    } else
                        shouldSetWaypoint--;
                } else {
                    foreach(var gyro in gyros) {
                        try {
                            gyro.SetValueBool( "Override", true );

                            gyro.Pitch = -targetRotation.Dot( Base6Directions.GetVector( gyro.Orientation.Left ) ) * 60;
                            gyro.Yaw = targetRotation.Dot( Base6Directions.GetVector( gyro.Orientation.Up ) ) * 60;
                            gyro.Roll = -targetRotation.Dot( Base6Directions.GetVector( gyro.Orientation.Forward ) ) * 60;

                        } catch(Exception e) { program.Echo( e.StackTrace ); }
                    }
                }
            }

            //public void Command( NavigationMode mode,  )
            int shouldSetWaypoint = 0;
            protected override void Update() {

                TempData tempData = new TempData();

                Vector3 meWorldPosition = Me.CubeGrid.GridIntegerToWorld( Me.Position );
                tempData.facing = Me.CubeGrid.GridIntegerToWorld( Me.Position + Base6Directions.GetIntVector( Me.Orientation.Forward ) ) - meWorldPosition;
                tempData.facing.Normalize();
                tempData.facingLeft = Me.CubeGrid.GridIntegerToWorld( Me.Position + Base6Directions.GetIntVector( Me.Orientation.Left ) ) - meWorldPosition;
                tempData.facingLeft.Normalize();
                tempData.facingUp = Me.CubeGrid.GridIntegerToWorld( Me.Position + Base6Directions.GetIntVector( Me.Orientation.Up ) ) - meWorldPosition;
                tempData.facingUp.Normalize();

                if(command != null) {
                    tempData.navData = command.GetNavData( program );
                    program.Echo( string.Format("Nav command: {0}", command.GetType().Name) );
                }

                /*Vector3 optimalThrustSpeed = HandleThrust( tempData );
                if(!tempData.navData.desiredFacing.HasValue)
                    tempData.navData.desiredFacing = optimalThrustSpeed;*/
                HandleFacingInLocalCoords( tempData );

            }


            private void HandleFacingWithQuat( TempData tempData ) {
                Quaternion targetRotation = Quaternion.Identity;
                
                if(tempData.navData.desiredForward.HasValue) {

                    if(tempData.navData.desiredUp.HasValue) { //forward and up

                        //inverse quaternion is used to transform world direction to local direction
                        Quaternion Quat_Two = Quaternion.CreateFromForwardUp( tempData.facing, tempData.facingUp );
                        //var InvQuat = Quaternion.Inverse( Quat_Two );
                        var InvQuat = Quaternion.Inverse( Quat_Two );

                        //program.DebugWithAntenna( string.Format( "FW: {0:0.00} {1:0.00} {2:0.00}", tempData.navData.desiredForward.Value.X, tempData.navData.desiredForward.Value.Y, tempData.navData.desiredForward.Value.Z ) );
                        //program.DebugWithAntenna( string.Format( "UP: {0:0.00} {1:0.00} {2:0.00}", tempData.navData.desiredUp.Value.X, tempData.navData.desiredUp.Value.Y, tempData.navData.desiredUp.Value.Z ) );

                        //transform to local coordinate system
                        Vector3 desiredForward = Vector3.Transform( tempData.navData.desiredForward.Value, InvQuat );
                        Vector3 desiredUp = Vector3.Transform( tempData.navData.desiredUp.Value, InvQuat );

                        //program.DebugWithAntenna( string.Format( "L FW: {0:0.00} {1:0.00} {2:0.00}", desiredForward.X, desiredForward.Y, desiredForward.Z ) );
                        //program.DebugWithAntenna( string.Format( "L UP: {0:0.00} {1:0.00} {2:0.00}", desiredUp.X, desiredUp.Y, desiredUp.Z ) );

                        targetRotation = Quaternion.CreateFromForwardUp( desiredForward, desiredUp );

                    } else { //just forward
                        targetRotation = Quaternion.CreateFromTwoVectors( tempData.facing, tempData.navData.desiredForward.Value );
                    }

                } else return;

                //program.DebugWithAntenna( string.Format( "Quat: {0:0.00} {1:0.00} {2:0.00} {3:0.00}", targetRotation.X, targetRotation.Y, targetRotation.Z, targetRotation.W ) );

                Vector3 turningAxis;
                float angle;
                targetRotation.GetAxisAngle( out turningAxis, out angle );

                turningAxis = ToEulerAngles( targetRotation );

                //program.DebugWithAntenna( string.Format( "Angle: {0:0.00}", angle ) );
                program.DebugWithAntenna( string.Format( "Angles: {0:0.00} {1:0.00} {2:0.00}", turningAxis.X, turningAxis.Y, turningAxis.Z) );

                foreach(var gyro in gyros) {
                    try {
                        gyro.SetValueBool( "Override", true );

                        gyro.Pitch = -turningAxis.Dot( Base6Directions.GetVector( gyro.Orientation.Left ) ) * angle;
                        gyro.Yaw = turningAxis.Dot( Base6Directions.GetVector( gyro.Orientation.Up ) ) * angle;
                        gyro.Roll = -turningAxis.Dot( Base6Directions.GetVector( gyro.Orientation.Forward ) ) * angle;

                    } catch(Exception e) { program.Echo( e.StackTrace ); }
                }


                //program.Echo( string.Format( "DRS: {0:0.00} {1:0.00} {2:0.00}", desiredRotSpeed.X, desiredRotSpeed.Y, desiredRotSpeed.Z ) );
                //program.Echo( string.Format( "Quat: {0:0.00} {1:0.00} {2:0.00}", targetRotation.X/targetRotation.W, targetRotation.Y / targetRotation.W, targetRotation.Z / targetRotation.W ) );

                //Vector3 adjustedQuaternion = new Vector3( targetRotation.X, targetRotation.Y, targetRotation.Z ) * ( 0.5f - targetRotation.W/2 );

                //Vector3 targetRotation = new Vector3( tempData.facingLeft.Dot( adjustedQuaternion ), tempData.facingUp.Dot( adjustedQuaternion ), tempData.facing.Dot( adjustedQuaternion ) );




                //targetRotation = new Vector3( 0,targetRotation.Y,0 ); 

                //program.Echo( "TR:   " + targetRotation );


                /*foreach(var gyro in gyros) {
                    try {
                        gyro.SetValueBool( "Override", true );

                        gyro.Pitch = -targetRotation.Dot( Base6Directions.GetVector( gyro.Orientation.Left ) ) * 60;
                        gyro.Yaw = targetRotation.Dot( Base6Directions.GetVector( gyro.Orientation.Up ) ) * 60;
                        gyro.Roll = -targetRotation.Dot( Base6Directions.GetVector( gyro.Orientation.Forward ) ) * 60;

                    } catch(Exception e) { program.Echo( e.StackTrace ); }
                }*/

            }

            private void HandleFacingInLocalCoords( TempData tempData ) {

                Matrix3x3 worldToLocal = Me.CubeGrid.WorldMatrix.Rotation;
                worldToLocal.Transpose();

                Vector3 targetForward;
                Vector3 targetUp;

                Quaternion targetRotation = Quaternion.Identity;

                if(tempData.navData.desiredForward.HasValue) {
                  
                    targetForward = worldToLocal.Transform( tempData.navData.desiredForward.Value );

                    if(tempData.navData.desiredUp.HasValue) { //forward and up

                        targetUp = worldToLocal.Transform( tempData.navData.desiredUp.Value );
                        targetRotation = Quaternion.CreateFromForwardUp( targetForward, targetUp );

                    } else { //just forward
                        targetRotation = Quaternion.CreateFromTwoVectors( Vector3.Forward, targetForward );
                    }

                } else return;
                
                //program.DebugWithAntenna( string.Format( "Quat: {0:0.00} {1:0.00} {2:0.00} {3:0.00}", targetRotation.X, targetRotation.Y, targetRotation.Z, targetRotation.W ) );

                Vector3 turningAxis = ToEulerAngles( targetRotation );

                //program.DebugWithAntenna( string.Format( "Angle: {0:0.00}", angle ) );
                program.DebugWithAntenna( string.Format( "Angles: {0:0.00} {1:0.00} {2:0.00}", turningAxis.X, turningAxis.Y, turningAxis.Z ) );

                //turningAxis.X = 0;
                turningAxis.Z = -turningAxis.Z;
                turningAxis.Y = -turningAxis.Y;
                turningAxis.X = -turningAxis.X;

                foreach(var gyro in gyros) {
                    try {
                        gyro.SetValueBool( "Override", true );

                        gyro.Pitch = turningAxis.Dot( Base6Directions.GetVector( gyro.Orientation.Left ) ) * 10;
                        gyro.Yaw = turningAxis.Dot( Base6Directions.GetVector( gyro.Orientation.Up ) ) * 10;
                        gyro.Roll = turningAxis.Dot( Base6Directions.GetVector( gyro.Orientation.Forward ) ) * 10;

                    } catch(Exception e) { program.Echo( e.StackTrace ); }
                }

                
                //program.Echo( string.Format( "DRS: {0:0.00} {1:0.00} {2:0.00}", desiredRotSpeed.X, desiredRotSpeed.Y, desiredRotSpeed.Z ) );
                //program.Echo( string.Format( "Quat: {0:0.00} {1:0.00} {2:0.00}", targetRotation.X/targetRotation.W, targetRotation.Y / targetRotation.W, targetRotation.Z / targetRotation.W ) );

                //Vector3 adjustedQuaternion = new Vector3( targetRotation.X, targetRotation.Y, targetRotation.Z ) * ( 0.5f - targetRotation.W/2 );

                //Vector3 targetRotation = new Vector3( tempData.facingLeft.Dot( adjustedQuaternion ), tempData.facingUp.Dot( adjustedQuaternion ), tempData.facing.Dot( adjustedQuaternion ) );




                //targetRotation = new Vector3( 0,targetRotation.Y,0 ); 

                //program.Echo( "TR:   " + targetRotation );


                /*foreach(var gyro in gyros) {
                    try {
                        gyro.SetValueBool( "Override", true );

                        gyro.Pitch = -targetRotation.Dot( Base6Directions.GetVector( gyro.Orientation.Left ) ) * 60;
                        gyro.Yaw = targetRotation.Dot( Base6Directions.GetVector( gyro.Orientation.Up ) ) * 60;
                        gyro.Roll = -targetRotation.Dot( Base6Directions.GetVector( gyro.Orientation.Forward ) ) * 60;

                    } catch(Exception e) { program.Echo( e.StackTrace ); }
                }*/

            }

            public static Vector3 ToEulerAngles( Quaternion q ) {
                // Store the Euler angles in radians
                Vector3 pitchYawRoll = new Vector3();

                double sqw = q.W * q.W;
                double sqx = q.X * q.X;
                double sqy = q.Y * q.Y;
                double sqz = q.Z * q.Z;

                // If quaternion is normalised the unit is one, otherwise it is the correction factor
                double unit = sqx + sqy + sqz + sqw;
                double test = q.X * q.Y + q.Z * q.W;

                if(test > 0.4999f * unit)                              // 0.4999f OR 0.5f - EPSILON
                {
                    // Singularity at north pole
                    pitchYawRoll.Y = 2f * (float)Math.Atan2( q.X, q.W );  // Yaw
                    pitchYawRoll.X = (float)Math.PI * 0.5f;              // Pitch
                    pitchYawRoll.Z = 0f;                                // Roll
                    return pitchYawRoll;
                } else if(test < -0.4999f * unit)                        // -0.4999f OR -0.5f + EPSILON
                  {
                    // Singularity at south pole
                    pitchYawRoll.Y = -2f * (float)Math.Atan2( q.X, q.W ); // Yaw
                    pitchYawRoll.X = -(float)Math.PI * 0.5f;             // Pitch
                    pitchYawRoll.Z = 0f;                                // Roll
                    return pitchYawRoll;
                } else {
                    pitchYawRoll.Y = (float)Math.Atan2( 2f * q.Y * q.W - 2f * q.X * q.Z, sqx - sqy - sqz + sqw );       // Yaw
                    pitchYawRoll.X = (float)Math.Asin( 2f * test / unit );                                             // Pitch
                    pitchYawRoll.Z = (float)Math.Atan2( 2f * q.X * q.W - 2f * q.Y * q.Z, -sqx + sqy - sqz + sqw );      // Roll
                }

                return pitchYawRoll;
            }

            /// <summary>
            /// Controls all available thrusters.
            /// </summary>
            /// <returns>Ideal facing, under which ship can accelerate fastest</returns>
            private Vector3 HandleThrust( TempData tempData ) {

                Vector3 desiredSpeed = Vector3.Zero;

                if(command != null)
                    desiredSpeed = command.GetNavData(program).desiredSpeed.GetValueOrDefault( Vector3.Zero );

                //TODO: use speed capping to reach desired speed (idea: if desired speed == max speed, use "infinite" magnitude -> will cause desiredSpeedChange to align with desiredSpeed)
                Vector3 desiredSpeedChange = desiredSpeed - program.trackedEntities[Me.EntityId].Velocity; //desired velocity change

                Vector3[] base6ToWorld = new Vector3[] { tempData.facing, -tempData.facing, tempData.facingLeft, -tempData.facingLeft, tempData.facingUp, -tempData.facingUp };
                float[] potentialPerDirection = new float[6];
                //int activeAxesCount = 0; 
                List<int> activeAxes = new List<int>(); //axes along which we can accelerate
                for(int i = 0; i < 6; i++) {
                    foreach(IMyThrust thruster in directionalThrusters[i])
                        potentialPerDirection[i] += thruster.MaxEffectiveThrust;
                    // some potential in axis AND its correct half-space
                    if(potentialPerDirection[i] > 0 && desiredSpeedChange.Dot( base6ToWorld[i] ) > 0) {
                        activeAxes.Add( i );
                    }
                }

                //TODO: make this parameter, or depend on distance - can cause orbiting if target is too close and arc is too wide
                float minCosine = 0.8f; //defines the acceptible arc of inpression for 1- or 2-axis acceleration

                Vector3 desiredSpeedChangeDirection = desiredSpeedChange;
                desiredSpeedChangeDirection.Normalize();

                float[] powerPerAxis = new float[6];
                switch(activeAxes.Count) {
                    case 0: {
                            program.Echo("No engine available");
                            break;
                        }
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
                    case 3: //three axes - any acceleration direction is possible
                    default: { //or more than three
                            float[] maxPower = new float[3];
                            for(int i = 0; i < 3; i++)
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

                //optimal thrust velocity calculation
                Vector3 optimalThrustVelocity = Vector3.Zero;
                //per main axis, choose the direction with higher potential

                //TODO - if they are equal, choose the one closer to current facing
                if(potentialPerDirection[0] >= potentialPerDirection[1])
                    optimalThrustVelocity += base6ToWorld[0];
                else if(potentialPerDirection[1] > 0)
                    optimalThrustVelocity += base6ToWorld[1];

                if(potentialPerDirection[2] >= potentialPerDirection[3])
                    optimalThrustVelocity += base6ToWorld[2];
                else if(potentialPerDirection[3] > 0)
                    optimalThrustVelocity += base6ToWorld[3];

                if(potentialPerDirection[4] > potentialPerDirection[5])
                    optimalThrustVelocity += base6ToWorld[4];
                else if(potentialPerDirection[5] > 0)
                    optimalThrustVelocity += base6ToWorld[5];

                program.Echo( string.Format("OTD: {0:0.00} {1:0.00} {2:0.00}", optimalThrustVelocity.X, optimalThrustVelocity.Y, optimalThrustVelocity.Z) );
                return optimalThrustVelocity;

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

            private class TempData {
                public Vector3 facing;
                public Vector3 facingLeft;
                public Vector3 facingUp;
                public NavigationData navData;
            }

            public struct NavigationData{
                public Vector3? desiredForward;
                public Vector3? desiredUp;
                public Vector3? desiredSpeed;
                public NavigationData( Vector3? spd, Vector3? forward, Vector3? up) {
                    desiredForward = forward;
                    desiredSpeed = spd;
                    desiredUp = up;
                }
            }

            Dictionary<string, Func<string, INavigationCommand>> commandFactoryList = new Dictionary<string, Func<string, INavigationCommand>>() {
                { "HIT", CommHitTarget.Create },
                { "MIR", CommMirrorTarget.Create }
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
                        Vector3 direction = (targetInfo.Position - program.myCenterOfMassWorld);
                        program.Echo( "POS: " + targetInfo.Position.ToString() );
                        float distance = direction.Normalize();
                        return new NavigationData() { desiredSpeed = direction * 100, desiredForward = direction };
                    }
                    return new NavigationData( Vector3.Zero, null, null );
                }
            }

            public class CommMirrorTarget : INavigationCommand {

                long targetEntity;

                public CommMirrorTarget( long targetEntity ) {
                    this.targetEntity = targetEntity;
                }

                public static CommMirrorTarget Create( string argument ) {
                    long id;
                    if(long.TryParse( argument, out id ))
                        return new CommMirrorTarget( id );
                    return null;
                }

                public NavigationData GetNavData( Program program ) {
                    MyDetectedEntityInfo targetInfo;
                    if(program.trackedEntities.TryGetValue( targetEntity, out targetInfo )) {
                        return new NavigationData( Vector3.Zero, targetInfo.Orientation.Forward, /*targetInfo.Orientation.Up*/ null );
                    }
                    return new NavigationData( Vector3.Zero, null, null );
                }
            }

            public class CommHold : INavigationCommand {
                public NavigationData GetNavData( Program program ) {
                    return new NavigationData( Vector3.Zero, null, null );
                }
            }


        }

        
    }
}
