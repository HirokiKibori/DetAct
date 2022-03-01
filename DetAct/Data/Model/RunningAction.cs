using DetAct.Behaviour;
using DetAct.Behaviour.Base;

namespace DetAct.Data.Model
{
    public class RunningAction : ActionBehaviour
    {
        protected RunningAction() : base("StillRunning") { }

        protected override BehaviourStatus OnUpdate()
            => BehaviourStatus.RUNNING;
    }
}
