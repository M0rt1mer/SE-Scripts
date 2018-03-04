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

        long selectedEntityId = -1;
        Dictionary<long, MyDetectedEntityInfo> mdeis = new Dictionary<long, MyDetectedEntityInfo>();

        /// <summary>
        /// Should be ran with Tracking output as argument
        /// </summary>
        /// <param name="argument"></param>
        public void Main( string argument ) {

            if(argument.StartsWith( "SwitchTarget" )) {
                if(!mdeis.ContainsKey( selectedEntityId )) {
                    if(mdeis.Count > 0)
                        selectedEntityId = mdeis.First().Key;
                } else {
                    bool found = false;
                    foreach(var entId in mdeis.Keys)
                        if(found) {
                            selectedEntityId = entId;
                            found = false;
                            break;
                        } else if(entId == selectedEntityId) {
                            found = true;
                        }
                    if(found)
                        if(mdeis.Count > 0)
                            selectedEntityId = mdeis.First().Key;
                }
            } else if(argument.StartsWith( "Lock" )) {
                if(mdeis.ContainsKey( selectedEntityId )) {
                    string weaponClass = argument.Substring( 5 );
                    List<IMyProgrammableBlock> pbs = new List<IMyProgrammableBlock>();
                    GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>( pbs, x => x.CustomName.Contains( weaponClass ) );
                    pbs.First().TryRun( "SelectTarget " + selectedEntityId );
                }
            } else {

                //read all inputs
                foreach(var mdei in StringToMDEIs( argument )) {
                    if(!mdeis.ContainsKey( mdei.EntityId ) || mdeis[mdei.EntityId].TimeStamp < mdei.TimeStamp)
                        mdeis[mdei.EntityId] = mdei;
                }

                StringBuilder outString = new StringBuilder();
                foreach(long entID in mdeis.Keys)
                    outString.AppendFormat( "{0} {1,-15} {2,6:N1}\n", entID == selectedEntityId ? "X" : " ", mdeis[entID].Name, Vector3.Distance( Me.CubeGrid.GridIntegerToWorld( Me.Position ), mdeis[entID].Position ) );

                string resultStr = outString.ToString();

                List<IMyTextPanel> outPanels = new List<IMyTextPanel>();
                GridTerminalSystem.GetBlocksOfType<IMyTextPanel>( outPanels, x => { return x.CustomName.Contains( "[TC]" ); } );
                foreach(var panel in outPanels)
                    panel.WritePublicText( resultStr );
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