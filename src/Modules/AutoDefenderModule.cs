using CloudDefender.Core;

namespace CloudDefender.Modules
{
    public class AutoDefenderModule : ModuleBase
    {
        public override string Name { get { return "AutoDefender"; } }
        public override string Description { get { return "Failed login monitor + 3-tier auto-ban"; } }
        public override string ScriptFileName { get { return "auto_defender.ps1"; } }
        public override string ScheduleArgs { get { return "/sc minute /mo 3"; } }
        public override string[] EmbeddedChunks { get { return new[] { Resources.AUTO_DEFENDER }; } }
    }
}
