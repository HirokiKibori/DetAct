using System.Collections.Generic;
using System.Threading;

using DetAct.Behaviour;
using DetAct.Behaviour.Base;

namespace DetAct.Data.Model {
    public class DetActCondition : BtmlCondition, ILowLevelDetActBehaviour, IInterruptible {
        private AutoResetEvent receivedAnswer = new(false);

        private DetActCondition() : base("DEFAULT") { }

        public DetActCondition(string name) : base(name) { }

        protected override BehaviourStatus ProcessFunctionCall(Dictionary<string, IEnumerable<string>> functions) {
            if(ParentTree?.Root is not DetActRoot root || root.Session is null)
                return BehaviourStatus.FAILURE;

            var behaviour = new Behaviour(Name, commands: functions) { Type = BehaviourType.CONDITION };

            if(root.Session is not BehaviourTreeSessionComponent session || !session.SendBehaviourMessage(behaviour, this))
                return BehaviourStatus.FAILURE;

            receivedAnswer.WaitOne();

            return Status;
        }

        public void UpdateStatus(BehaviourStatus status) {
            Status = status == BehaviourStatus.SUCCESS ? BehaviourStatus.SUCCESS : BehaviourStatus.FAILURE;
            receivedAnswer.Set();
        }

        public void Interrupt() {
            Status = BehaviourStatus.INTERRUPTED;
            receivedAnswer.Set();
        }
    }
}
