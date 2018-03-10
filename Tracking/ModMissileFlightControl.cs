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
        public class ModMissileFlightControl : StandardModule {

            private ModNavigation navigation;

            public override int Priority => 20000;

            public ModMissileFlightControl( Program program ) : base( program ) {
            }

            public override void Initialize() {
                navigation = program.installedModules["navigation"] as ModNavigation;
                if(navigation == null)
                    throw new ArgumentException( "FlightControl cannot run without Navigation module" );
            }

            protected override void Command( string command ) {
                
            }

            protected override void Update() {
                
            }
        }
    }
}
