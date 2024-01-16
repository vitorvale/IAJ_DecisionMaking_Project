using Assets.Scripts.IAJ.Unity.DecisionMaking.ForwardModel.ForwardModelActions;
using Assets.Scripts.IAJ.Unity.DecisionMaking.ForwardModel;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.GameManager {
    public class OptimizedCurrentStateWorldModel : OptimizedFutureStateWorldModel {

        private Dictionary<string, Goal> Goals { get; set; }

        public OptimizedCurrentStateWorldModel(GameManager gameManager, List<Action> actions, List<Goal> goals) : base(gameManager, actions) {
            this.Parent = null;
            this.Goals = new Dictionary<string, Goal>();

            foreach (var goal in goals) {
                this.Goals.Add(goal.Name, goal);
            }
        }

        public void Initialize() {
            this.ActionEnumerator.Reset();
            this.InitializeStats(this.GameManager);
        }

        public override void SetProperty(string propertyName, Property value) {
            //this method does nothing, because you should not directly set a property of the CurrentStateWorldModel
        }

        public override int GetNextPlayer() {
            //in the current state, the next player is always player 0
            return 0;
        }
    }
}
