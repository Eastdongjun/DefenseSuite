using CloudDefender.Core;

namespace CloudDefender.Modules
{
    public class WebTrapModule : ModuleBase
    {
        public override string Name { get { return "WebTrap"; } }
        public override string Description { get { return "Web scanner path detection + ban"; } }
        public override string ScriptFileName { get { return "web_trap_watcher.ps1"; } }
        public override string ScheduleArgs { get { return "/sc onstart"; } }
        public override string[] EmbeddedChunks { get { return new[] { Resources.WEB_TRAP }; } }
    }
}
