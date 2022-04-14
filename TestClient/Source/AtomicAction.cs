using System;

namespace TestClient.Source
{
    public class AtomicAction
    {
        public bool AcceptNull { get; set; } = false;

        public Action<string> Print { get; set; }

        public Action<DetActMessage> Send { get; set; }

        public static BehaviourStatus GreatherThen(string currentPos, string value)
        {
            if (int.TryParse(currentPos, out var pos) && int.TryParse(value, out var val) && pos > val)
                return BehaviourStatus.SUCCESS;

            return BehaviourStatus.FAILURE;
        }

        public BehaviourStatus WriteMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return BehaviourStatus.FAILURE;

            Print?.Invoke(message);

            return BehaviourStatus.SUCCESS;
        }

        public BehaviourStatus CheckPush(string push)
        {
            if (bool.TryParse(push, out var result) && result)
                return BehaviourStatus.SUCCESS;

            return BehaviourStatus.FAILURE;
        }

        public BehaviourStatus GetItem(string itemToPop)
        {
            Print?.Invoke(itemToPop);

            return BehaviourStatus.SUCCESS;
        }

        public BehaviourStatus CheckItemToPush(string itemToPush)
        {
            if (!(string.IsNullOrWhiteSpace(itemToPush) || itemToPush.ToLower() == "null") || AcceptNull)
                return BehaviourStatus.SUCCESS;

            return BehaviourStatus.FAILURE;
        }

        public BehaviourStatus IncrementPos_(string currentPos, string keyForPos)
        {
            if (int.TryParse(currentPos, out var result))
            {
                result++;

                var updatePos = new Blackboard(name: "Default", new() { { keyForPos, result.ToString() } });
                Send?.Invoke(new DetActMessage(updatePos));

                return BehaviourStatus.SUCCESS;
            }

            return BehaviourStatus.FAILURE;
        }

        public BehaviourStatus DecrementPos_(string currentPos, string keyForPos)
        {
            if (int.TryParse(currentPos, out var result) && result > 0)
            {
                result--;

                var updatePos = new Blackboard(name: "Default", new() { { keyForPos, result.ToString() } });
                Send?.Invoke(new DetActMessage(updatePos));

                return BehaviourStatus.SUCCESS;
            }

            return BehaviourStatus.FAILURE;
        }

        public BehaviourStatus SaveItem_(string posToPush, string itemToPush, string boardName)
        {
            var pushItem = new Blackboard(name: boardName, new() { { posToPush, itemToPush } });
            Send?.Invoke(new DetActMessage(pushItem));

            return BehaviourStatus.SUCCESS;
        }
    }
}