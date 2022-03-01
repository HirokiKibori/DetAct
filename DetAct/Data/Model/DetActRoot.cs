using System;

using DetAct.Behaviour;
using DetAct.Behaviour.Concrete;

namespace DetAct.Data.Model
{
    public class DetActRoot : Root, IInitializable
    {
        public SessionComponent Session { get; set; }

        public Action<BehaviourStatus> TerminationAction { get; set; }

        public void OnInitialize() { }

        public void OnTerminate(BehaviourStatus status)
            => TerminationAction?.Invoke(status);
    }
}
