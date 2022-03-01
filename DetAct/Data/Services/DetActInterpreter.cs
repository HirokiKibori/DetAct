using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

using DetAct.Behaviour;
using DetAct.Behaviour.Base;
using DetAct.Data.Model;
using DetAct.Interpreter;

namespace DetAct.Data.Services
{
    public class DetActInterpreter
    {
        private static readonly Lazy<DetActInterpreter> _instance = new(() => new());

        private BtmlDirector director;

        public static DetActInterpreter Instance { get => _instance.Value; }

        public KeyValuePair<string, Type> RootBehaviour { get; private set; }

        /// <summary>
        /// Only HighLevelBehaviours with the exception of Decorators and Composites.
        /// </summary>
        public ImmutableSortedDictionary<string, Type> HighLevelBehaviours;

        public ImmutableSortedDictionary<string, Type> Decorators;

        public ImmutableSortedDictionary<string, Type> Composites;

        /// <summary>
        /// Only LowLevelBehaviours with the exception of Actions and Conditions.
        /// </summary>
        public ImmutableSortedDictionary<string, Type> LowLevelBehaviours;

        public ImmutableSortedDictionary<string, Type> Actions;

        public ImmutableSortedDictionary<string, Type> Conditions;

        private DetActInterpreter()
        {
            director = new BtmlDirector();

            RegisterOwnTypes();
            SortTypes();
        }

        private void RegisterOwnTypes()
        {
            // override types
            director.RegisterType(typeName: "Root", typeof(DetActRoot));
            director.RegisterType(typeName: "Action", typeof(DetActAction));
            director.RegisterType(typeName: "Condition", typeof(DetActCondition));

            // new types
            director.RegisterType(typeName: "CleanBlackBoardsAction", typeof(CleanBlackBoardsAction));
            director.RegisterType(typeName: "RunningAction", typeof(RunningAction));
        }

        private void SortTypes()
        {
            var registeredTypes = director.RegisteredTypes;

            var highLevelBehaviours = new Dictionary<string, Type>();
            var decorators = new Dictionary<string, Type>();
            var composites = new Dictionary<string, Type>();

            var lowLevelBehaviours = new Dictionary<string, Type>();
            var actions = new Dictionary<string, Type>();
            var conditions = new Dictionary<string, Type>();

            foreach(var registeredType in registeredTypes) {
                if(registeredType.Key == "Root") {
                    RootBehaviour = registeredType;
                    continue;
                }

                if(registeredType.Value.IsAssignableTo(typeof(DecoratorBehaviour))) {
                    decorators[registeredType.Key] = registeredType.Value;
                    continue;
                }

                if(registeredType.Value.IsAssignableTo(typeof(CompositeBehaviour))) {
                    composites[registeredType.Key] = registeredType.Value;
                    continue;
                }

                if(registeredType.Value.IsAssignableTo(typeof(IHighLevelBehaviour))) {
                    highLevelBehaviours[registeredType.Key] = registeredType.Value;
                    continue;
                }

                if(registeredType.Value.IsAssignableTo(typeof(ActionBehaviour))) {
                    actions[registeredType.Key] = registeredType.Value;
                    continue;
                }

                if(registeredType.Value.IsAssignableTo(typeof(ConditionBehaviour))) {
                    conditions[registeredType.Key] = registeredType.Value;
                    continue;
                }

                if(registeredType.Value.IsAssignableTo(typeof(ILowLevelBehaviour))) {
                    lowLevelBehaviours[registeredType.Key] = registeredType.Value;
                    continue;
                }
            }

            HighLevelBehaviours = highLevelBehaviours.ToImmutableSortedDictionary();
            Decorators = decorators.ToImmutableSortedDictionary();
            Composites = composites.ToImmutableSortedDictionary();

            LowLevelBehaviours = lowLevelBehaviours.ToImmutableSortedDictionary();
            Actions = actions.ToImmutableSortedDictionary();
            Conditions = conditions.ToImmutableSortedDictionary();
        }

        public InterpreterResult GenrateBehaviourTree(string btmlCode, string name)
        {
            var result = BtmlInterpreter.GenrateBehaviourTree(btmlCode, director);

            if(result.Tree is not null)
                result.Tree.Name = name;

            return result;
        }
    }
}
