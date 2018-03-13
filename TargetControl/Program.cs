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
            Echo( argument );
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
                    if( pbs.Count > 0 )
                        pbs.First().TryRun( "SelectTarget " + selectedEntityId );
                }
            } else if(argument.StartsWith( "Broadcast" )) {
                List<IMyRadioAntenna> antennae = new List<IMyRadioAntenna>();
                GridTerminalSystem.GetBlocksOfType<IMyRadioAntenna>( antennae, x => x.CubeGrid == Me.CubeGrid );
                if(antennae.Count > 0)
                    antennae[0].TransmitMessage( "Navigate HIT " + selectedEntityId, MyTransmitTarget.Ally | MyTransmitTarget.Owned );
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

        #endregion

    }
}