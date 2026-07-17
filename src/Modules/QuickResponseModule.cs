using CloudDefender.Core;

namespace CloudDefender.Modules
{
    public class QuickResponseModule : ModuleBase
    {
        public override string Name { get { return "QuickResponse"; } }
        public override string Description { get { return "Real-time event-triggered blocking"; } }
        public override string ScriptFileName { get { return "quick_response.ps1"; } }
        public override string ScheduleArgs { get { return "/sc onstart"; } }
        public override string[] EmbeddedChunks { get { return new[] { Resources.QUICK_RESPONSE }; } }
    }
}
