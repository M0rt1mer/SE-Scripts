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

        #region Navigation

        long selectedEntityId = -1;
        MyDetectedEntityInfo chosenTarget;

        //calculated statistics
        //Vector3 lastFacing = Vector3.Zero;

        // gyroscopes
        List<IMyGyro> gyros = new List<IMyGyro>();
        /// <summary>
        /// Should be ran with Tracking output as argument (= just put [TR] in name, will be updated automatically)
        /// </summary>
        /// <param name="argument"></param>
        public void Main( string argument ) {

            if(argument.StartsWith( "SelectTarget" )) {
                selectedEntityId = long.Parse( argument.Substring( 12 ) );
            }
            // ELSE ----- tracking input
            foreach(var mdei in StringToMDEIs( argument )) {
                if(mdei.EntityId == selectedEntityId)
                    chosenTarget = mdei;
            }

            Vector3 facing = Me.CubeGrid.GridIntegerToWorld( Me.Position + Base6Directions.GetIntVector( Me.Orientation.Forward ) ) - Me.CubeGrid.GridIntegerToWorld( Me.Position );
            facing.Normalize();
            Vector3 facingLeft = Me.CubeGrid.GridIntegerToWorld( Me.Position + Base6Directions.GetIntVector( Me.Orientation.Left ) ) - Me.CubeGrid.GridIntegerToWorld( Me.Position );
            facingLeft.Normalize();
            Vector3 facingUp = Me.CubeGrid.GridIntegerToWorld( Me.Position + Base6Directions.GetIntVector( Me.Orientation.Up ) ) - Me.CubeGrid.GridIntegerToWorld( Me.Position );
            facingUp.Normalize();
            /*if(lastFacing == Vector3.Zero)
                lastFacing = facing;
            Vector3 rotSpeed = lastFacing.Cross( facing );*/

            Vector3 desiredSpeed;

            if(chosenTarget.IsEmpty()) {
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

            Echo( "TR:   " + targetRotation );

            //Echo( string.Format( "Pb Diff: R{0:0.00} Y{1:0.00} P{2:0.00}", (targetRotation*Base6Directions.GetIntVector( Me.Orientation.Forward )).Length(), (targetRotation*Base6Directions.GetIntVector( Me.Orientation.Up ) ).Length(), (targetRotation*Base6Directions.GetIntVector( Me.Orientation.Left ) ).Length() ) );

            //Echo( string.Format( "Fwd:{0} Up:{1} Left:{2}", Me.Orientation.Forward, Me.Orientation.Up, Me.Orientation.Left ) );
            GridTerminalSystem.GetBlocksOfType<IMyGyro>( gyros );
            foreach(var gyro in gyros) {
                try {
                    //gyro.ApplyAction( "Override" );
                    gyro.SetValueBool( "Override", true );
                    //gyro.SetValueFloat
                    /*Vector3 gyroPos = Me.CubeGrid.GridIntegerToWorld( gyro.Position );
                    // forward vector
                    Vector3 forward = Me.CubeGrid.GridIntegerToWorld( gyro.Position + Base6Directions.GetIntVector( gyro.Orientation.Forward ) ) - gyroPos;
                    forward.Normalize();
                    Vector3 left =    Me.CubeGrid.GridIntegerToWorld( gyro.Position + Base6Directions.GetIntVector( gyro.Orientation.Left    ) ) - gyroPos;
                    left.Normalize();
                    Vector3 up =      Me.CubeGrid.GridIntegerToWorld( gyro.Position + Base6Directions.GetIntVector( gyro.Orientation.Up      ) ) - gyroPos;
                    up.Normalize();
                    gyro.SetValueFloat( "Roll", FncLogistic( forward.Dot( desiredSpeed ) ) );
                    gyro.SetValueFloat( "Yaw", FncLogistic( left.Dot( desiredSpeed ) ) );
                    gyro.SetValueFloat( "Pitch", FncLogistic( up.Dot( desiredSpeed ) ) );
                    Echo( string.Format( "PB FWD: F{0:0.00} L{1:0.00} U{2:0.00}", forward.Dot( facing ), up.Dot( facing ), left.Dot( facing ) ) );
                    Echo( string.Format( "PB LFT: F{0:0.00} L{1:0.00} U{2:0.00}", forward.Dot( facingLeft ), up.Dot( facingLeft ), left.Dot( facingLeft ) ) );
                    Echo( string.Format( "PB UPP: F{0:0.00} L{1:0.00} U{2:0.00}", forward.Dot( facingUp ), up.Dot( facingUp ), left.Dot( facingUp ) ) );
                    Echo( string.Format( "Fwd:{0} Up:{1} Left:{2}", gyro.Orientation.Forward, gyro.Orientation.Up, gyro.Orientation.Left ) );
                    Echo( string.Format( "Diff: R{0:0.00} Y{1:0.00} P{2:0.00}", forward.Dot( desiredSpeed ), left.Dot( desiredSpeed ), up.Dot( desiredSpeed ) ) );*/
                    Vector3 left = Base6Directions.GetVector( gyro.Orientation.Left );
                    Vector3 Up = Base6Directions.GetVector( gyro.Orientation.Up );
                    Vector3 forw = Base6Directions.GetVector( gyro.Orientation.Forward );

                    // gyro.SetValueFloat( "Pitch",  targetRotation.Dot( new Vector3I( left.X, Up.X, forw.X ) ) );
                    // gyro.SetValueFloat( "Yaw",    targetRotation.Dot( new Vector3I( left.Y, Up.Y, forw.Y ) ) );
                    // gyro.SetValueFloat( "Roll",   targetRotation.Dot( new Vector3I( left.Z, Up.Z, forw.Z ) ) );

                    /*Vector3 gyroOverrideVelocity = new Vector3( 
                        -targetRotation.Dot( new Vector3I( left.X, Up.X, forw.X ) ),
                         targetRotation.Dot( new Vector3I( left.Y, Up.Y, forw.Y ) ), 
                        -targetRotation.Dot( new Vector3I( left.Z, Up.Z, forw.Z ) ) );*/
                    //Vector3 gyroOverrideVelocity = new Vector3( -VecAbsDot( targetRotation, left ), VecAbsDot(targetRotation,Up), -VecAbsDot(targetRotation,forw) );
                    Vector3 gyroOverrideVelocity = new Vector3( -targetRotation.Dot( left ), targetRotation.Dot( Up ), -targetRotation.Dot( forw ) );

                    gyro.SetValueFloat( "Pitch", gyroOverrideVelocity.X );
                    gyro.SetValueFloat( "Yaw", gyroOverrideVelocity.Y );
                    gyro.SetValueFloat( "Roll", gyroOverrideVelocity.Z );
                    //check
                    /*Echo( string.Format( "Fwd:{0} Up:{1} Left:{2}", Base6Directions.GetVector( gyro.Orientation.Forward ),
                        Base6Directions.GetVector( gyro.Orientation.Up ), Base6Directions.GetVector( gyro.Orientation.Left ) ) );
                    Echo( "G TR:   " + Vector3.TransformNormal( gyroOverrideVelocity, gyro.Orientation ) );*/

                    /*Echo( "CC: " + (
                     -(-VecAbsDot( targetRotation, left )) * left
                     + VecAbsDot( targetRotation, Up ) * Up
                     -(-VecAbsDot( targetRotation, forw )) * forw) );
                     */

                    /*gyro.SetValueFloat( "Roll", FncLogistic( (targetRotation.Dot(Base6Directions.GetIntVector( gyro.Orientation.Left ) )*3 ) ) );
                    gyro.SetValueFloat( "Yaw",  -FncLogistic( (targetRotation.Dot(Base6Directions.GetIntVector( gyro.Orientation.Up      ) )*3 ) ) );
                    gyro.SetValueFloat( "Pitch",FncLogistic( (targetRotation.Dot(Base6Directions.GetIntVector( gyro.Orientation.Forward ) )*3 ) ) );
                    List<IMyBeacon> bcn = new List<IMyBeacon>();
                    GridTerminalSystem.GetBlocksOfType( bcn );
                    bcn[0].SetCustomName( string.Format( "R{0:0.00} Y{1:0.00} P{2:0.00}", targetRotation.Dot( Base6Directions.GetIntVector( gyro.Orientation.Left ) ), targetRotation.Dot( Base6Directions.GetIntVector( gyro.Orientation.Up ) )
                        , targetRotation.Dot( Base6Directions.GetIntVector( gyro.Orientation.Forward ) ) ) );*/
                } catch(Exception e) { Echo( e.StackTrace ); }
            }


            /*Echo("Target: "+selectedEntityId);
            if(!chosenTarget.IsEmpty())
                Echo( "Position: " + chosenTarget.Position );*/

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