using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameScript {
    partial class Program {

        static readonly Dictionary<string, Func<Program,Module>> factoryLst = new Dictionary<string, Func<Program,Module>>() {
            { "navigation", (program) => new ModNavigation(program) },
            { "tracking", (program) => new ModTracking(program) },
            { "missile", (program) => new ModMissileFlightControl(program) },
            { "turret", (program) => new ModTurret(program) }
        };

        readonly Dictionary<string, Module> installedModules = new Dictionary<string, Module>();
        readonly SortedList<int,Module> moduleUpdateOrder = new SortedList<int,Module>();

        void ModuleIntialize( string config ) {
            Dictionary<Module, IEnumerable<string>> initArguments = new Dictionary<Module, IEnumerable<string>>();
            foreach(string moduleInit in config.Split( new char[] { ';' ,',' }, StringSplitOptions.RemoveEmptyEntries )) {
                string[] tokens = moduleInit.Split( new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries );
                string moduleName = tokens[0];
                if(factoryLst.ContainsKey( moduleName )) {
                    Module mod = factoryLst[moduleName]( this );
                    initArguments.Add( mod, tokens.Skip( 1 ) );
                    installedModules.Add( moduleName, mod );
                    moduleUpdateOrder.Add( mod.Priority, mod );
                    logMessages.Enqueue( "Installing module "+mod.GetType().Name + " params: '" + string.Join("', '", tokens) + "'");
                }
            }
            foreach(Module module in moduleUpdateOrder.Values)
                module.Initialize( initArguments[module] );
        }

        void ModuleUpdate( string arguments, UpdateType type ) {
            foreach(Module module in moduleUpdateOrder.Values)
                module.Main( arguments, type );
        }

        void ModuleCheck() {
            foreach(Module module in moduleUpdateOrder.Values)
                module.Check();
        }

        void DebugPrintModules() {
            Echo( string.Format("M: {0}", string.Join( ", ", moduleUpdateOrder.Values.Select( x=> x.GetType().Name ) ) ) );
        }

        public abstract class Module {
            protected Program program;
            public Module( Program program ) {
                this.program = program;
            }
            public abstract int Priority { get; }
            public abstract void Main( string arguments, UpdateType upType );
            public abstract void Initialize( IEnumerable<string> arguments);
            public virtual void Check() {
                program.Echo( this.GetType().Name + " OK" );
            }

            protected IMyProgrammableBlock Me => program.Me;
            protected IMyGridTerminalSystem GridTerminalSystem => program.GridTerminalSystem;
        }

        public abstract class StandardModule : Module {
            public StandardModule( Program program ) : base( program ) {}

            public sealed override void Main( string argument, UpdateType type ) {

                if((type & (UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100)) > 0)
                    Update();

                if((type & (UpdateType.IGC | UpdateType.Script | UpdateType.Terminal)) > 0)
                    Command( argument );
            }

            protected abstract void Update();
            protected abstract void Command( string command );

        }

    }
}
