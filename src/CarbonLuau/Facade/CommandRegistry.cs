using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        public sealed class ScriptCommand
        { public string Id, Name, Permission, Description; }
        // Publish must prepare without effects, then atomically replace only
        // CarbonLuau's chat entries. A thrown failure must leave Previous intact.
        public interface ICommandRegistrar
        {
            void Publish(FacadeSession Previous, FacadeSession Next);
        }
    }
}
