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

    }
}
