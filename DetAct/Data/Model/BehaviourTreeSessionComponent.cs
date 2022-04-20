using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using DetAct.Behaviour;
using DetAct.Data.Services;
using DetAct.Interpreter;

namespace DetAct.Data.Model {
    public class BehaviourTreeSessionComponent : SessionComponent {
        private bool ticking = false;

        //(DetActMessage.GetHashCode, Action/Condition)
        private ConcurrentDictionary<int, ILowLevelDetActBehaviour> ProcessingLLBehavioursCache = new();

        private DirectoryWatcherService _directoryWatcherService;

        public bool Running { get; protected set; } = false;

        public BehaviourTree Tree { get; private set; } = BehaviourTree.Default;

        public BehaviourTreeSessionComponent(DetActSession parent, DirectoryWatcherService directoryWatcherService) : base(parent)
            => _directoryWatcherService = directoryWatcherService;

        private bool CheckRunning(DetActMessage message) {
            if(!Running) {
                SendMessage?.Invoke(
                    new DetActMessage(new Error(
                        name: "TreeNotRunningException",
                        message: $"No running tree in current session. Ignored message:{Environment.NewLine}{message}")
                    ) { ID = message.ID });

                return false;
            }

            return true;
        }

        public override async void ReceiveMessage(DetActMessage message) {
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

        public ImmutableList<InterpreterMessage> LoadBehaviourModel(string btmlFileContent, string name) {
            var result = DetActInterpreter.Instance.GenrateBehaviourTree(btmlFileContent, name);

            if(!result.IsValid) {
                throw new InvalidDataException(
                    string.Join(Environment.NewLine,
                        result.Messages.Select(m => $"{m.MessageType}: {m.Message}"))
                    );
            }

            Tree = result.Tree;

            var root = Tree.Root as DetActRoot;
            if(root is not null) {
                root.Session = this;
                root.TerminationAction = status => FinishFullTick(status);
            }

            return result.Messages;
        }

        private void HasChanged()
            => ComponentChanged?.Invoke();

        public void RunTree(bool state) {
            Running = Tree is not null && Tree != BehaviourTree.Default && state;

            var control = new Control(name: "TreeState", items: new Dictionary<string, string> { { "running", Running.ToString().ToLower() } });
            SendMessage?.Invoke(new DetActMessage(control));

            HasChanged();
        }

        public override void SessionClosed() {
            RunTree(false);
            ResetTree();
        }

        private DetActMessage HandleBlackboardMessage(DetActMessage message) {
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

        public bool SendBehaviourMessage(Behaviour behaviour, ILowLevelDetActBehaviour instance) {
            if(SendMessage is null)
                return false;

            var message = new DetActMessage(content: behaviour);
            ProcessingLLBehavioursCache[message.GetHashCode()] = instance;
            SendMessage?.Invoke(message);

            return true;
        }

        private async void HandleControlMessage(DetActMessage message) {
            var content = message.Content as Control;

            if(content.Name == "BT-Control") {
                if(content.Items.Keys.Any(key => key.Equals("load_code") || key.Equals("load"))) {
                    try {
                        string treeModel = "";
                        string name = "externCode";

                        if(content.Items.TryGetValue("load_code", out string code)) {
                            treeModel = code;
                        }

                        if(content.Items.TryGetValue("load", out string fileName)) {
                            using var fileContent = new StreamReader(_directoryWatcherService.GetFile(fileName));
                            treeModel = await fileContent.ReadToEndAsync();

                            name = fileName;
                        }

                        if(!string.IsNullOrWhiteSpace(treeModel)) {
                            if(Running) {
                                ResetTree();
                                RunTree(false);
                            }

                            var messages = LoadBehaviourModel(btmlFileContent: treeModel, name);
                            if(!messages.IsEmpty) {
                                var error = new Error(name: "InterpreterWarning",
                                    message: string.Join(Environment.NewLine,
                                        messages.Select(m => $"{m.MessageType}: {m.Message}"))
                                    );
                                SendMessage?.Invoke(new DetActMessage(error));
                            }

                            RunTree(true);
                        }
                    }
                    catch(Exception e) {
                        var error = new Error(name: e.GetType().Name, message: e.Message);
                        SendMessage?.Invoke(new DetActMessage(error));
                    }

                    return;
                }

                // only tick, if there is currently no tick
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

        public void FinishFullTick(BehaviourStatus status) {
            ticking = false;

            var control = new Control(name: "RootResult", items: new Dictionary<string, string> { { "status", status.ToString() } });
            SendMessage?.Invoke(new DetActMessage(control));
        }

        private void HandleBehaviourMessage(DetActMessage message) {
            if(ProcessingLLBehavioursCache.TryRemove(key: message.GetHashCode(), value: out ILowLevelDetActBehaviour behaviour)) {
                var content = message.Content as Behaviour;
                behaviour.UpdateStatus(content.Status);
            }
        }

        public void ResetTree() {
            Tree.Interrupt();
            ProcessingLLBehavioursCache.Clear();
            SetBehaviourToIndefined(Tree.Root);
            ticking = false;

            HasChanged();
        }

        private void SetBehaviourToIndefined(IBehaviour behaviour) {
            if(behaviour is IHighLevelBehaviour hlBehaviour)
                foreach(var child in hlBehaviour.Childs)
                    SetBehaviourToIndefined(child);

            behaviour.ResetStatus();
        }

        protected override void ManagedDisposeHook() {
            RunTree(false);
            ResetTree();
        }
    }
}