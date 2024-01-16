using Assets.Scripts.IAJ.Unity.DecisionMaking.ForwardModel.ForwardModelActions;
using Assets.Scripts.IAJ.Unity.DecisionMaking.ForwardModel;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.GameManager {
    public class OptimizedFutureStateWorldModel : OptimizedWorldModel {
        protected GameManager GameManager { get; set; }
        protected int NextPlayer { get; set; }
        protected Action NextEnemyAction { get; set; }
        protected Action[] NextEnemyActions { get; set; }

        public OptimizedFutureStateWorldModel(GameManager gameManager, List<Action> actions) : base(actions) {
            this.GameManager = gameManager;
            this.NextPlayer = 0;

            foreach (var chest in gameManager.chests) {
                this.SetDisposable(chest.name, new Disposable(chest.name, true, chest.transform.position));
            }

            foreach (var potion in gameManager.potions) {
                this.SetDisposable(potion.name, new Disposable(potion.name, true, potion.transform.position));
            }

            foreach (var enemy in gameManager.enemies) {
                this.SetDisposable(enemy.name, new Disposable(enemy.name, true, enemy.transform.position));
            }

            this.InitializeStats(gameManager);
            this.position = gameManager.initialPosition;
            this.time = gameManager.characterData.Time;
        }

        public OptimizedFutureStateWorldModel(OptimizedFutureStateWorldModel parent) : base(parent) {
            this.GameManager = parent.GameManager;
        }

        public override OptimizedWorldModel GenerateChildWorldModel() {
            return new OptimizedFutureStateWorldModel(this);
        }

        public override bool IsTerminal() {
            Stat HP = (Stat)this.GetProperty(Properties.HP);
            Stat money = (Stat) this.GetProperty(Properties.MONEY);

            return HP.Value <= 0 || time >= 200 || (this.NextPlayer == 0 && money.Value == 25);
        }

        public override float GetScore() {
            Stat money = (Stat) this.GetProperty(Properties.MONEY);
            Stat HP = (Stat) this.GetProperty(Properties.HP);

            if (HP.Value <= 0)
                return 0.0f;
            else if (money.Value == 25) {
                return 1.0f;
            }
            else
                return 0.0f;
        }

        public override int GetNextPlayer() {
            return this.NextPlayer;
        }

        public override void CalculateNextPlayer() {
            bool enemyEnabled;

            //basically if the character is close enough to an enemy, the next player will be the enemy.
            foreach (var enemy in this.GameManager.enemies) {
                Disposable enemyEnabledProp = (Disposable) this.GetProperty(enemy.name);
                enemyEnabled = enemyEnabledProp.Enabled;
                if (enemyEnabled && (enemy.transform.position - position).sqrMagnitude <= 100) {
                    this.NextPlayer = 1;
                    this.NextEnemyAction = new SwordAttack(this.GameManager.autonomousCharacter, enemy);
                    this.NextEnemyActions = new Action[] { this.NextEnemyAction };
                    return;
                }
            }
            this.NextPlayer = 0;
            //if not, then the next player will be player 0
        }

        public override Action GetNextAction() {
            Action action;
            if (this.NextPlayer == 1) {
                action = this.NextEnemyAction;
                this.NextEnemyAction = null;
                return action;
            }
            else
                return base.GetNextAction();
        }

        public override Action[] GetExecutableActions() {
            if (this.NextPlayer == 1) {
                return this.NextEnemyActions;
            }
            else
                return base.GetExecutableActions();
        }
    }
}
