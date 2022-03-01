using DetAct.Behaviour;
using DetAct.Behaviour.Base;

namespace DetAct.Data.Model
{
    public class CleanBlackBoardsAction : ActionBehaviour
    {
        protected CleanBlackBoardsAction() : base(null) { }

        protected override BehaviourStatus OnUpdate()
        {
            foreach(var board in ParentTree?.Boards)
                board.Value.Clear();

            return BehaviourStatus.SUCCESS;
        }
    }
}
