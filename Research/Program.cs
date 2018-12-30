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
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;

namespace IngameScript {
    partial class Program : MyGridProgram {

        public Program() {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        Quaternion previousQuat = Quaternion.Identity;

        public void Main( string argument, UpdateType updateSource ) {

            List<IMyGyro> gyros = new List<IMyGyro>();
            GridTerminalSystem.GetBlocksOfType<IMyGyro>( gyros );

            if(Me.CustomData.Length > 0) {

                string[] strs = Me.CustomData.Split( ' ' );

                Vector3 desiredAngleSpeed = new Vector3( float.Parse( strs[0] ), float.Parse( strs[1] ), float.Parse( strs[2] ) );

                foreach(var gyro in gyros) {
                    try {

                        gyro.Pitch = -desiredAngleSpeed.Dot( Base6Directions.GetVector( gyro.Orientation.Left ) ) * 60;
                        gyro.Yaw = desiredAngleSpeed.Dot( Base6Directions.GetVector( gyro.Orientation.Up ) ) * 60;
                        gyro.Roll = -desiredAngleSpeed.Dot( Base6Directions.GetVector( gyro.Orientation.Forward ) ) * 60;

                    } catch(Exception e) { Echo( e.StackTrace ); }
                }

            } else {
                foreach(var gyro in gyros) {
                    if(gyro.GyroOverride) {
                        Echo( gyro.CustomName );
                        Vector3I forward = Base6Directions.GetIntVector( gyro.Orientation.Forward );
                        Echo( string.Format( "FW: {0:0.0} {1:0.0} {2:0.0}", forward.X, forward.Y, forward.Z ) );
                        Vector3I up = Base6Directions.GetIntVector( gyro.Orientation.Up );
                        Echo( string.Format( "UP: {0:0.0} {1:0.0} {2:0.0}", up.X, up.Y, up.Z ) );
                        Vector3I left = Base6Directions.GetIntVector( gyro.Orientation.Left );
                        Echo( string.Format( "LF: {0:0.0} {1:0.0} {2:0.0}", left.X, left.Y, left.Z ) );
                    }
                }
            }

            List<IMyShipController> controllers = new List<IMyShipController>();
            GridTerminalSystem.GetBlocksOfType( controllers );

            foreach(var controller in controllers) {
                Vector3 speeds = controller.GetShipVelocities().AngularVelocity;
                Echo( controller.CustomName );
                Echo( string.Format( "SPDS: {0:0.0} {1:0.0} {2:0.0}", speeds.X, speeds.Y, speeds.Z ) );
                /*Quaternion quat = Quaternion.CreateFromYawPitchRoll( speeds.X, speeds.Y, speeds.Z );
                Echo( string.Format( "QUAT: {0:0.0} {1:0.0} {2:0.0} {3:0.0}", quat.X, quat.Y, quat.Z, quat.W ) );*/
            }

            /*Quaternion thisQuat = Quaternion.CreateFromRotationMatrix( Me.CubeGrid.WorldMatrix );

            var quatDiff = Quaternion.Divide( thisQuat, previousQuat );

            previousQuat = thisQuat;

            Echo("Calcualted: ");
            Echo( string.Format( "QUAT: {0:0.00} {1:0.00} {2:0.00} {3:0.00}", thisQuat.X, thisQuat.Y, thisQuat.Z, thisQuat.W ) );
            Echo( string.Format( "DIFF: {0:0.00} {1:0.00} {2:0.00} {3:0.00}", quatDiff.X, quatDiff.Y, quatDiff.Z, quatDiff.W ) );

    */

        }

    }
}