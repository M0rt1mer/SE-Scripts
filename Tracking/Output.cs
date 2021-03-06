﻿using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game;

namespace IngameScript {
    partial class Program {

        abstract class FilteredOutput {

            public int relationshipFlags = 0;
            public int entityTypeFlags = 0;
            public bool fresh = false;

            public FilteredOutput( string str ) {
                string[] filters = str.Split( ',' );
                foreach(string flt in filters) {
                    try {
                        MyDetectedEntityType entType = (MyDetectedEntityType)Enum.Parse( typeof( MyDetectedEntityType ), flt );
                        entityTypeFlags |= 1 << (int)entType;
                    } catch(ArgumentException e) { }
                    try {
                        MyRelationsBetweenPlayerAndBlock entType = (MyRelationsBetweenPlayerAndBlock)Enum.Parse( typeof( MyRelationsBetweenPlayerAndBlock ), flt );
                        relationshipFlags |= 1 << (int)entType;
                    } catch(ArgumentException e) { }
                    if(flt.Equals( "Fresh" ))
                        fresh = true;
                }
            }

            public bool Filter( MyDetectedEntityInfo mdei, long currentTimestamp ) {
                if(relationshipFlags > 0 && (relationshipFlags & (1 << (int)mdei.Relationship)) == 0)
                    return false;
                if(entityTypeFlags > 0 && (entityTypeFlags & (1 << (int)mdei.Type)) == 0)
                    return false;
                if(fresh && currentTimestamp != mdei.TimeStamp)
                    return false;
                return true;
            }

            public void Output( IEnumerable<MyDetectedEntityInfo> signals, long currentTimestamp ) {
                StringBuilder outText = new StringBuilder();
                foreach(var signal in signals) {
                    if(Filter( signal, currentTimestamp ))
                        outText.Append( MDEIToString( signal ) );
                }
                Send( outText.ToString() );
            }

            protected abstract void Send( string text );

            public string MDEIToString( MyDetectedEntityInfo mdei ) =>
              $"{mdei.EntityId}:{ConvertName(mdei.Name)}:{mdei.BoundingBox.Min.X:0.00},{mdei.BoundingBox.Min.Y:0.00},{mdei.BoundingBox.Min.Z:0.00};{mdei.BoundingBox.Max.X:0.00},{mdei.BoundingBox.Max.Y:0.00},{mdei.BoundingBox.Max.Z:0.00}@{mdei.TimeStamp}\n";

            public string ConvertName(string name) => name != null ? nameConverter.Replace(name, "") : "NONAME";
            public System.Text.RegularExpressions.Regex nameConverter = new System.Text.RegularExpressions.Regex( @"[^\w\d\s]*" );
        }

        class PanelOutput : FilteredOutput {
            IMyTextPanel panel;
            public PanelOutput( IMyTextPanel panel ) : base( panel.CustomData ) {
                this.panel = panel;
            }
            protected override void Send( string text ) {
                panel.WritePublicText( text );
            }
        }
        class AntennaOutput : FilteredOutput
        {
            private Program program;
            public AntennaOutput(Program program) : base("")
            {
              this.program = program;
            }
            protected override void Send( string text ) {
                program.IGC.SendBroadcastMessage("data", text, TransmissionDistance.AntennaRelay);
            }
        }
        class ProgrammableBlockOutput : FilteredOutput {
            IMyProgrammableBlock blk;
            public ProgrammableBlockOutput( IMyProgrammableBlock pb ) : base( pb.CustomData ) {
                this.blk = pb;
            }
            protected override void Send( string text ) {
                blk.TryRun( text );
            }
        }

    }
}
