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

        static Dictionary<string, Func<Program,Module>> factoryLst = new Dictionary<string, Func<Program,Module>>() {
            { "navigation", (program) => new ModNavigation(program) },
            { "tracking", (program) => new ModTracking(program) },
            { "missile", (program) => new ModMissileFlightControl(program) }
        };

        protected Dictionary<string, Module> installedModules = new Dictionary<string, Module>();
        protected SortedList<int,Module> moduleUpdateOrder = new SortedList<int,Module>();

        void ModuleIntialize( string config ) {
            foreach(string moduleName in config.Split( ',' )) {
                if(factoryLst.ContainsKey( moduleName )) {
                    Module mod = factoryLst[moduleName]( this );
                    installedModules.Add( moduleName, mod  );
                    moduleUpdateOrder.Add( mod.Priority, mod );
                    logMessages.Enqueue( "Initiating module "+mod.GetType().Name );
                }
            }
            foreach(Module module in moduleUpdateOrder.Values)
                module.Initialize();
        }

        void ModuleUpdate( string arguments, UpdateType type ) {
            foreach(Module module in moduleUpdateOrder.Values)
                module.Main( arguments, type );
        }

        void ModuleCheck() {
            foreach(Module module in moduleUpdateOrder.Values)
                module.Check();
        }

        public abstract class Module {
            protected Program program;
            public Module( Program program ) {
                this.program = program;
            }
            public abstract int Priority { get; }
            public abstract void Main( string arguments, UpdateType upType );
            public abstract void Initialize();
            public virtual void Check() {
                program.Echo( this.GetType().Name + " OK" );
            }

            protected IMyProgrammableBlock Me => program.Me;
            protected IMyGridTerminalSystem GridTerminalSystem => program.GridTerminalSystem;
        }

        public abstract class StandardModule : Module {
            public StandardModule( Program program ) : base( program ) {
            }

            public override sealed void Main( string argument, UpdateType type ) {

                if((type & (UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100)) > 0)
                    Update();

                if((type & (UpdateType.Antenna | UpdateType.Script | UpdateType.Terminal)) > 0)
                    Command( argument );
            }

            protected abstract void Update();
            protected abstract void Command( string command );

        }

    }
}
