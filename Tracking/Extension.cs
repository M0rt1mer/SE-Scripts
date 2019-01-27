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
    
    static class Extension {

        public static Vector3 Transform( this Matrix3x3 matrix, Vector3 vector ) {
            return new Vector3(matrix.Col0.Dot(vector), matrix.Col1.Dot(vector), matrix.Col2.Dot(vector));
        }

    }
}
