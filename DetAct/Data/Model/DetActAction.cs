using System.Collections.Generic;

using DetAct.Behaviour;
using DetAct.Behaviour.Base;

namespace DetAct.Data.Model
{
    public class DetActAction : BtmlAction, ILowLevelDetActBehaviour, IInitializable
    {
        private bool finished = false;

        private Behaviour behaviour;

        private DetActAction() : base("DEFAULT") { }

        public DetActAction(string name) : base(name) { }

        protected override BehaviourStatus ProcessFunctionCall(Dictionary<string, IEnumerable<string>> functions)
        {
            if(!finished && Status is not BehaviourStatus.RUNNING) {
                behaviour = new Behaviour(Name, commands: functions) { Type = BehaviourType.ACTION };

                return BehaviourStatus.RUNNING;
            }

            return Status;
        }

        protected override void TaskAfterUpdate()
        {
            if(behaviour is null)
                return;

            if(ParentTree?.Root is not DetActRoot root
                || root.Session is null
                || root.Session is not BehaviourTreeSessionComponent session
                || !session.SendBehaviourMessage(behaviour, this)) {
                Status = BehaviourStatus.FAILURE;
                finished = true;
            }

            behaviour = null;
        }

        public void UpdateStatus(BehaviourStatus status)
        {
            Status = status;
            finished = true;

            ParentTree?.Tick();
        }

        public void OnInitialize() { }

        public void OnTerminate(BehaviourStatus status)
            => finished = false;
    }
}
