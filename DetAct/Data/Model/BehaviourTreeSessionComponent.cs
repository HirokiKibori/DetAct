using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

using DetAct.Behaviour;
using DetAct.Data.Services;

namespace DetAct.Data.Model
{
    public class BehaviourTreeSessionComponent : SessionComponent
    {
        private bool ticking = false;

        //(DetActMessage.GetHashCode, Action/Condition)
        private ConcurrentDictionary<int, ILowLevelDetActBehaviour> ProcessingLLBehavioursCache = new();

        private DirectoryWatcherService _directoryWatcherService;

        public bool Running { get; protected set; } = false;

        public BehaviourTree Tree { get; private set; } = BehaviourTree.Default;

        public BehaviourTreeSessionComponent(DetActSession parent, DirectoryWatcherService directoryWatcherService) : base(parent)
            => _directoryWatcherService = directoryWatcherService;

        private bool CheckRunning(DetActMessage message)
        {
            if(!Running) {
                SendMessage?.Invoke(
                    new DetActMessage(new Error(
                        name: "TreeNotRunningException",
                        message: $"No running tree in current session. Ignored message:{Environment.NewLine}{message}")
                    )
                    { ID = message.ID });

                return false;
            }

            return true;
        }

        public override async void ReceiveMessage(DetActMessage message)
        {
            DetActMessage answer = null;

            switch(message.Type) {
                case MessageType.BLACKBOARD:
                    if(CheckRunning(message))
                        answer = HandleBlackboardMessage(message);
                    break;

                case MessageType.CONTROL:
                    HandleControlMessage(message);
                    break;

                case MessageType.BEHAVIOUR:
                    if(CheckRunning(message))
                        HandleBehaviourMessage(message);
                    break;

                case MessageType.ERROR:
                    break;

                default:
                    break;
            }

            HasChanged();

            if(answer is not null)
                await SendMessage?.Invoke(answer);
        }

        public void LoadBTMLFile(string btmlFileContent, string name)
        {
            Tree = DetActInterpreter.Instance.GenrateBehaviourTree(btmlFileContent, name).Tree;

            var root = Tree.Root as DetActRoot;
            if(root is not null) {
                root.Session = this;
                root.TerminationAction = status => FinishFullTick(status);
            }
        }

        private void HasChanged()
            => ComponentChanged?.Invoke();

        public void RunTree(bool state)
        {
            Running = Tree is not null && Tree != BehaviourTree.Default && state;

            var control = new Control(name: "TreeState", items: new Dictionary<string, string> { { "running", Running.ToString().ToLower() } });
            SendMessage?.Invoke(new DetActMessage(control));

            HasChanged();
        }

        public override void SessionClosed()
        {
            RunTree(false);
            ResetTree();
        }

        private DetActMessage HandleBlackboardMessage(DetActMessage message)
        {
            var content = message.Content as Blackboard;

            if(Tree.GetBoard(content.Name, out var board)) {
                foreach(var item in content.Items) {
                    var value = string.IsNullOrWhiteSpace(item.Value) ? "null" : item.Value;

                    if(string.IsNullOrWhiteSpace(item.Key) || item.Key == "null")
                        continue;

                    board[item.Key] = value;
                }

                return null;
            }

            return new DetActMessage(new Error(name: "IndefinedBoardException", message: $"No board with name '{content.Name}' defined for tree.")) { ID = message.ID };
        }

        public bool SendBehaviourMessage(Behaviour behaviour, ILowLevelDetActBehaviour instance)
        {
            if(SendMessage is null)
                return false;

            var message = new DetActMessage(content: behaviour);
            ProcessingLLBehavioursCache[message.GetHashCode()] = instance;
            SendMessage?.Invoke(message);

            return true;
        }

        private async void HandleControlMessage(DetActMessage message)
        {
            var content = message.Content as Control;

            if(content.Name == "BT-Control") {
                if(content.Items.TryGetValue("load", out string load) && !string.IsNullOrWhiteSpace(load)) {

                    try {
                        using var fileContent = new StreamReader(_directoryWatcherService.GetFile(load));

                        if(Running) {
                            ResetTree();
                            RunTree(false);
                        }

                        LoadBTMLFile(btmlFileContent: await fileContent.ReadToEndAsync(), load);

                        RunTree(true);
                    }
                    catch(Exception e) {
                        var error = new Error(name: e.GetType().Name, message: e.Message);
                        SendMessage?.Invoke(new DetActMessage(error));
                    }

                    return;
                }

                //TODO: find a better way (Why should there be a possibility to set a tick to false? -> sarcastic question)
                if(CheckRunning(message) && content.Items.TryGetValue("tick", out string tick) && bool.TryParse(tick, out bool result) && result) {
                    if(ticking) {
                        var error = new Error(name: "CurrentlyTickingException", message: "Tree is currently ticking.");
                        SendMessage?.Invoke(new DetActMessage(error));

                        return;
                    }

                    ticking = true;

                    ResetTree();
                    Tree.Root.Tick();

                    return;
                }
            }
        }

        public void FinishFullTick(BehaviourStatus status)
        {
            ticking = false;

            var control = new Control(name: "RootResult", items: new Dictionary<string, string> { { "status", status.ToString() } });
            SendMessage?.Invoke(new DetActMessage(control));
        }

        private void HandleBehaviourMessage(DetActMessage message)
        {
            if(ProcessingLLBehavioursCache.TryRemove(key: message.GetHashCode(), value: out ILowLevelDetActBehaviour behaviour)) {
                var content = message.Content as Behaviour;
                behaviour.UpdateStatus(content.Status);
            }
        }

        public void ResetTree()
        {
            Tree.Interrupt();
            ProcessingLLBehavioursCache.Clear();
            SetBehaviourToIndefined(Tree.Root);

            HasChanged();
        }

        private void SetBehaviourToIndefined(IBehaviour behaviour)
        {
            if(behaviour is IHighLevelBehaviour hlBehaviour)
                foreach(var child in hlBehaviour.Childs)
                    SetBehaviourToIndefined(child);

            behaviour.ResetStatus();
        }

        protected override void ManagedDisposeHook()
        {
            RunTree(false);
            ResetTree();
        }
    }
}
