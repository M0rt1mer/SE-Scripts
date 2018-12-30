using System;
using System.Collections.Generic;

namespace IngameScript {
    partial class Program {
        public class ModMissileFlightControl : StandardModule {

            private ModNavigation navigation;

            public override int Priority => 20000;

            public ModMissileFlightControl( Program program ) : base( program ) {
            }

            public override void Initialize( IEnumerable<string> arguments ) {
                navigation = program.installedModules["navigation"] as ModNavigation;
                if(navigation == null)
                    throw new ArgumentException( "FlightControl cannot run without Navigation module" );
                //navigation.Command( new ModNavigation.CommHold() );
            }

            protected override void Command( string command ) {
                
            }

            protected override void Update() {
                
            }
        }
    }
}
