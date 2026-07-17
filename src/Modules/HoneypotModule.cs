using CloudDefender.Core;

namespace CloudDefender.Modules
{
    public class HoneypotModule : ModuleBase
    {
        public override string Name { get { return "Honeypot"; } }
        public override string Description { get { return "12 TCP trap ports, touch-to-ban /24 subnet"; } }
        public override string ScriptFileName { get { return "honeypot.ps1"; } }
        public override string ScheduleArgs { get { return "/sc onstart"; } }
        public override string[] EmbeddedChunks { get { return new[] { Resources.HONEYPOT }; } }
    }
}
