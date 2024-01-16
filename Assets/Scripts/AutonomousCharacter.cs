using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
using Assets.Scripts.GameManager;
using Assets.Scripts.IAJ.Unity.DecisionMaking.ForwardModel;
using Assets.Scripts.IAJ.Unity.DecisionMaking.GOB;
using Assets.Scripts.IAJ.Unity.DecisionMaking.MCTS;
using Assets.Scripts.IAJ.Unity.DecisionMaking.ForwardModel.ForwardModelActions;

namespace Assets.Scripts
{
    public class AutonomousCharacter : MonoBehaviour
    {
        //constants
        public const string SURVIVE_GOAL = "Survive";
        public const string GAIN_LEVEL_GOAL = "GainXP";
        public const string BE_QUICK_GOAL = "BeQuick";
        public const string GET_RICH_GOAL = "GetRich";

        public const float DECISION_MAKING_INTERVAL = 20.0f;
        public const float RESTING_INTERVAL = 5.0f;
        public const int REST_HP_RECOVERY = 2;

        //UI Variables
        private Text SurviveGoalText;
        private Text GainXPGoalText;
        private Text BeQuickGoalText;
        private Text GetRichGoalText;
        private Text DiscontentmentText;
        private Text TotalProcessingTimeText;
        private Text BestDiscontentmentText;
        private Text ProcessedActionsText;
        private Text BestActionText;
        private Text BestActionSequence;
        private Text DiaryText;

        public GameManager.GameManager GameManager { get; private set; }

        [Header("Character Settings")]  
        public bool controlledByPlayer;
        public float controlledSpeed;

        [Header("Decision Algorithm Options")]
        public bool GOBActive;
        public bool GOAPActive;
        public bool MCTSActive;
        public bool MCTSBiasedActive;
        public bool MCTSLimitedActive;

        [Header("Character Info")]
        public bool Resting = false;
        public float StopRestTime;

        public Goal BeQuickGoal { get; private set; }
        public Goal SurviveGoal { get; private set; }
        public Goal GetRichGoal { get; private set; }
        public Goal GainLevelGoal { get; private set; }
        public List<Goal> Goals { get; set; }
        public List<Action> Actions { get; set; }
        public Action CurrentAction { get; private set; }
        public GOBDecisionMaking GOBDecisionMaking { get; set; }
        public DepthLimitedGOAPDecisionMaking GOAPDecisionMaking { get; set; }
        public MCTS MCTSDecisionMaking { get; set; }
        public bool wasAttacked { get; set; }
        //public bool Finished { get; set; }

        //private fields for internal use only
        private NavMeshAgent agent;
        private float nextUpdateTime = 0.0f;
        private float previousGold = 0.0f;
        private int previousLevel = 1;
        private Vector3 previousTarget;
        private int currentActionIndex = 0;

        //This speed is only a pointer to the NavMeshAgent's speed
        public float maxSpeed {get; private set;}
        public TextMesh playerText;
        private GameObject closestObject;


        public void Start()
        {
            //This is the actual speed of the agent
            this.agent = this.GetComponent<NavMeshAgent>();
            maxSpeed = this.agent.speed;
            GameManager = GameObject.Find("Manager").GetComponent<GameManager.GameManager>();
            playerText.text = "";

            // Initializing UI Text
            this.BeQuickGoalText = GameObject.Find("BeQuickGoal").GetComponent<Text>();
            this.SurviveGoalText = GameObject.Find("SurviveGoal").GetComponent<Text>();
            this.GainXPGoalText = GameObject.Find("GainXP").GetComponent<Text>();
            this.GetRichGoalText = GameObject.Find("GetRichGoal").GetComponent<Text>();
            this.DiscontentmentText = GameObject.Find("Discontentment").GetComponent<Text>();
            this.TotalProcessingTimeText = GameObject.Find("ProcessTime").GetComponent<Text>();
            this.BestDiscontentmentText = GameObject.Find("BestDicont").GetComponent<Text>();
            this.ProcessedActionsText = GameObject.Find("ProcComb").GetComponent<Text>();
            this.BestActionText = GameObject.Find("BestAction").GetComponent<Text>();
            this.BestActionSequence = GameObject.Find("BestActionSequence").GetComponent<Text>();
            DiaryText = GameObject.Find("DiaryText").GetComponent<Text>();


            //initialization of the GOB decision making
            //let's start by creating 4 main goals

            this.SurviveGoal = new Goal(SURVIVE_GOAL, 1.0f);

            this.GainLevelGoal = new Goal(GAIN_LEVEL_GOAL, 5.0f)
            {
                ChangeRate = 0.1f
            };

            this.GetRichGoal = new Goal(GET_RICH_GOAL, 5.0f)
            {
                InsistenceValue = 5.0f,
                ChangeRate = 0.2f
            };

            this.BeQuickGoal = new Goal(BE_QUICK_GOAL, 0.25f)
            {
                ChangeRate = 0.1f
            };

            this.Goals = new List<Goal>();
            this.Goals.Add(this.SurviveGoal);
            this.Goals.Add(this.BeQuickGoal);
            this.Goals.Add(this.GetRichGoal);
            this.Goals.Add(this.GainLevelGoal);

            //initialize the available actions
            //Uncomment commented actions after you implement them

            this.Actions = new List<Action>();

            this.Actions.Add(new LevelUp(this));
            this.Actions.Add(new ShieldOfFaith(this));

            this.Actions.Add(new Rest(this));
            this.Actions.Add(new Teleport(this));


            foreach (var chest in GameObject.FindGameObjectsWithTag("Chest"))
            {
                this.Actions.Add(new PickUpChest(this, chest));
            }

            foreach (var potion in GameObject.FindGameObjectsWithTag("HealthPotion"))
            {
                this.Actions.Add(new GetHealthPotion(this, potion));
            }

            foreach (var manaPotion in GameObject.FindGameObjectsWithTag("ManaPotion")) {
                this.Actions.Add(new GetManaPotion(this, manaPotion));
            }

            foreach (var enemy in GameObject.FindGameObjectsWithTag("Skeleton"))
            {
                this.Actions.Add(new DivineSmite(this, enemy));
                this.Actions.Add(new SwordAttack(this, enemy));
            }

            foreach (var enemy in GameObject.FindGameObjectsWithTag("Orc"))
            {
                this.Actions.Add(new SwordAttack(this, enemy));
            }

            foreach (var enemy in GameObject.FindGameObjectsWithTag("Dragon"))
            {
                this.Actions.Add(new SwordAttack(this, enemy));
            }

            // Initialization of Decision Making Algorithms
            var worldModel = new CurrentStateWorldModel(GameManager, this.Actions, this.Goals);
            var optimizedWorldModel = new OptimizedCurrentStateWorldModel(GameManager, Actions, Goals);
            this.GOBDecisionMaking = new GOBDecisionMaking(this.Actions, this.Goals);
            this.GOAPDecisionMaking = new DepthLimitedGOAPDecisionMaking(worldModel,this.Actions,this.Goals);
            if (MCTSActive)
                this.MCTSDecisionMaking = new MCTS(worldModel, this.GameManager.StochasticWorld);
            else if (MCTSBiasedActive)
                this.MCTSDecisionMaking = new MCTSBiasedPlayout(worldModel, this.GameManager.StochasticWorld);
            else if (MCTSLimitedActive)
                this.MCTSDecisionMaking = new MCTSLimitedPlayout(worldModel, this.GameManager.StochasticWorld);

            this.Resting = false;
            this.wasAttacked = false;
            //this.Finished = true;

            DiaryText.text += "My Diary \n I awoke. What a wonderful day to kill Monsters! \n";
        }

        void Update()
        {
            if (GameManager.gameEnded) return;

            //Every x amount of times we've got to update things
            if (Time.time > this.nextUpdateTime || GameManager.WorldChanged)
            {

                GameManager.WorldChanged = false;
                this.nextUpdateTime = Time.time + DECISION_MAKING_INTERVAL;

                //first step, perceptions
                //update the agent's goals based on the state of the world
                this.SurviveGoal.InsistenceValue = GameManager.characterData.MaxHP - GameManager.characterData.HP;

                this.BeQuickGoal.InsistenceValue += DECISION_MAKING_INTERVAL * this.BeQuickGoal.ChangeRate;
                if(this.BeQuickGoal.InsistenceValue > 10.0f)
                {
                    this.BeQuickGoal.InsistenceValue = 10.0f;
                }

                this.GainLevelGoal.InsistenceValue += this.GainLevelGoal.ChangeRate; //increase in goal over time
                if(GameManager.characterData.Level > this.previousLevel)
                {
                    this.GainLevelGoal.InsistenceValue -= GameManager.characterData.Level - this.previousLevel;
                    this.previousLevel = GameManager.characterData.Level;
                }

                this.GetRichGoal.InsistenceValue += this.GetRichGoal.ChangeRate; //increase in goal over time
                if (this.GetRichGoal.InsistenceValue > 10)
                {
                    this.GetRichGoal.InsistenceValue = 10.0f;
                }

                if (GameManager.characterData.Money > this.previousGold)
                {
                    this.GetRichGoal.InsistenceValue -= GameManager.characterData.Money - this.previousGold;
                    this.previousGold = GameManager.characterData.Money;
                }



                this.SurviveGoalText.text = "Survive: " + this.SurviveGoal.InsistenceValue;
                this.GainXPGoalText.text = "Gain Level: " + this.GainLevelGoal.InsistenceValue.ToString("F1");
                this.BeQuickGoalText.text = "Be Quick: " + this.BeQuickGoal.InsistenceValue.ToString("F1");
                this.GetRichGoalText.text = "GetRich: " + this.GetRichGoal.InsistenceValue.ToString("F1");
                this.DiscontentmentText.text = "Discontentment: " + this.CalculateDiscontentment().ToString("F1");

                if (Resting)
                {
                    Debug.Log("timer : [" + Time.time + "], stopRestTime : [" + StopRestTime + "]");
                    agent.isStopped = true;
                    if (Time.time >= StopRestTime)
                    {
                        Resting = false;
                        agent.isStopped = false;
                        Debug.Log("Stopping rest.");
                        GameManager.WorldChanged = true;
                        this.GameManager.characterData.HP += REST_HP_RECOVERY;
                        if (this.GameManager.characterData.HP > this.GameManager.characterData.MaxHP)
                            this.GameManager.characterData.HP = this.GameManager.characterData.MaxHP;
                        //this.Finished = true;
                    }
                    else
                    {
                        this.nextUpdateTime = Time.time + AutonomousCharacter.RESTING_INTERVAL;
                        return;
                    }
                }
                //To have a new decision lets initialize Decision Making Proccess
                this.CurrentAction = null;
                if (GOAPActive)
                    this.GOAPDecisionMaking.InitializeDecisionMakingProcess();
                else if (GOBActive)
                    this.GOBDecisionMaking.InProgress = true;
                else if (MCTSActive || this.MCTSBiasedActive || this.MCTSLimitedActive)
                    this.MCTSDecisionMaking.InitializeMCTSearch();

            }

            if (this.controlledByPlayer) {
                if (Input.GetKey(KeyCode.W))
                    this.transform.position += new Vector3(0.0f, 0.0f, 1.0f) * this.controlledSpeed;
                if (Input.GetKey(KeyCode.S))
                    this.transform.position += new Vector3(0.0f, 0.0f, -1.0f) * this.controlledSpeed;
                if (Input.GetKey(KeyCode.A))
                    this.transform.position += new Vector3(-1.0f, 0.0f, 0.0f) * this.controlledSpeed;
                if (Input.GetKey(KeyCode.D))
                    this.transform.position += new Vector3(1.0f, 0.0f, 0.0f) * this.controlledSpeed;
                if (Input.GetKey(KeyCode.F))
                    if (closestObject != null) {
                        //Simple way of checking which object is closest to Sir Uthgard
                        var s = playerText.text.ToString();
                        if (s.Contains("Potion"))
                            PickUpPotion();
                        else if (s.Contains("Chest"))
                            PickUpChest();
                        else if (s.Contains("Enemy"))
                            AttackEnemy();
                    }
                if (Input.GetKey(KeyCode.L))
                    this.GameManager.LevelUp();
            }

            else if (this.GOAPActive) {
                this.UpdateDLGOAP();
            }
            else if (this.GOBActive) {
                this.UpdateGOB();
            }
            else if (this.MCTSActive || this.MCTSBiasedActive || this.MCTSLimitedActive) {
                this.UpdateMCTS();
            }

            if (this.CurrentAction != null) {
                if (this.CurrentAction.CanExecute()) {

                    this.CurrentAction.Execute();
                }


            } 
          
        }

        public void AddToDiary(string s)
        {
            DiaryText.text += Time.time + s + "\n";

            //If the diary gets too large we cut it. Plain and simple
            if (DiaryText.text.Length > 600)
                DiaryText.text = DiaryText.text.Substring(500);
        }

        private void UpdateGOB()
        {
            
            bool newDecision = false;
            if (this.GOBDecisionMaking.InProgress)
            {
                //choose an action using the GOB Decision Making process
                var action = this.GOBDecisionMaking.ChooseAction();
                if (action != null && action != this.CurrentAction)
                {
                    this.CurrentAction = action;
                    newDecision = true;
                    if (newDecision)
                    {
                        AddToDiary(" I decided to " + action.Name);
                        this.BestActionText.text = "Best Action: " + action.Name + "\n";
                    }

                }

            }

        }

        private void UpdateDLGOAP()
        {
            bool newDecision = false;
            if (this.GOAPDecisionMaking.InProgress)
            {
                var action = this.GOAPDecisionMaking.ChooseAction();
                if (action != null && action != this.CurrentAction)
                {
                    this.CurrentAction = action;
                    newDecision = true;
                    //this.Finished = false;
                }
            }

            this.TotalProcessingTimeText.text = "Process. Time: " + this.GOAPDecisionMaking.TotalProcessingTime.ToString("F");
            this.BestDiscontentmentText.text = "Best Discontentment: " + this.GOAPDecisionMaking.BestDiscontentmentValue.ToString("F");
            this.ProcessedActionsText.text = "Act. comb. processed: " + this.GOAPDecisionMaking.TotalActionCombinationsProcessed;

            if (this.GOAPDecisionMaking.BestAction != null)
            {
                if (newDecision)
                {
                    AddToDiary(" I decided to " + GOAPDecisionMaking.BestAction.Name);
                }
                var actionText = "";
                foreach (var action in this.GOAPDecisionMaking.BestActionSequence)
                {
                    actionText += "\n" + action.Name;
                }
                this.BestActionSequence.text = "Best Action Sequence: " + actionText;
                this.BestActionText.text = "Best Action: " + GOAPDecisionMaking.BestAction.Name;
            }
            else
            {
                this.BestActionSequence.text = "Best Action Sequence:\nNone";
                this.BestActionText.text = "Best Action: \n Node";
            }
        }

        private void UpdateMCTS() {
            bool newDecision = false;
            if (this.MCTSDecisionMaking.InProgress)
            {
                var action = this.MCTSDecisionMaking.Run();
                if (action != null && action != this.CurrentAction)
                {
                    this.CurrentAction = action;
                    newDecision = true;
                    //this.Finished = false;
                }
            }

            this.TotalProcessingTimeText.text = "Process. Time: " + this.MCTSDecisionMaking.TotalProcessingTime.ToString("F");

            this.ProcessedActionsText.text = "Iterations Performed: " + this.MCTSDecisionMaking.CurrentIterations.ToString("F");

            if(this.MCTSDecisionMaking.Plays != 0)
                this.BestDiscontentmentText.text = "WinRate: " + (this.MCTSDecisionMaking.Wins / this.MCTSDecisionMaking.Plays).ToString("F");

            if (this.MCTSDecisionMaking.BestFirstChild != null)
            {
                if (newDecision)
                {
                    AddToDiary(" I decided to " + MCTSDecisionMaking.BestFirstChild.Action.Name);
                }
                var actionText = "";
                foreach (var action in this.MCTSDecisionMaking.BestActionSequence)
                {
                    actionText += "\n" + action.Name;
                }
                this.BestActionSequence.text = "Best Action Sequence: " + actionText;
                this.BestActionText.text = "Best Action: " + MCTSDecisionMaking.BestFirstChild.Action.Name;
            }
            else
            {
                this.BestActionSequence.text = "Best Action Sequence:\nNone";
                this.BestActionText.text = "Best Action: \n Node";
            }

      
        }

        public void StartPathfinding(Vector3 targetPosition)
        {
            //if the targetPosition received is the same as a previous target, then this a request for the same target
            //no need to redo the pathfinding search
            if(!this.previousTarget.Equals(targetPosition))
            {
                this.previousTarget = targetPosition;
                agent.SetDestination(targetPosition);
                
            }
        }

        // Simple way of calculating distance left to target using Unity's navmesh
        public float GetDistanceToTarget(Vector3 originalPosition, Vector3 targetPosition)
        {
            var distance = 0.0f;

            NavMeshPath result = new NavMeshPath();
            var r = agent.CalculatePath(targetPosition, result);
            if (r == true)
            {
                var currentPosition = originalPosition;
                foreach (var c in result.corners)
                {
                    //Rough estimate, it does not account for shortcuts so we have to multiply it
                    distance += Vector3.Distance(currentPosition, c) * 0.65f;
                    currentPosition = c;
                }
                return distance;
            }

            //Default value
            return 100;
        }

		

        public float CalculateDiscontentment()
        {
            var discontentment = 0.0f;

            foreach (var goal in this.Goals)
            {
                discontentment += goal.GetDiscontentment();
            }
            return discontentment;
        }

        //Functions designed for when the Player has control of the character
        void OnTriggerEnter(Collider col)
        {
            if (this.controlledByPlayer)
            {
                Debug.Log("Controlled By player");
                if (col.gameObject.tag.ToString().Contains("Potion"))
                {
                    playerText.text = "Pickup Potion";
                    closestObject = col.gameObject;
                }
                else if (col.gameObject.tag.ToString().Contains("Chest"))
                {
                    playerText.text = "Pickup Chest";
                    closestObject = col.gameObject;
                }
                else if (col.gameObject.tag.ToString().Contains("Orc") || col.gameObject.tag.ToString().Contains("Skeleton"))
                {
                    playerText.text = "Attack Enemy";
                    closestObject = col.gameObject;
                }
            }
        }

        void OnTriggerExit(Collider col)
        {
            if (col.gameObject.tag.ToString() != "")
                playerText.text = "";
        }


        //Functions designed for when the Player has control of the character
        void PickUpPotion()
        {
            if (closestObject != null)
                if (GameManager.InPotionRange(closestObject))
                {
                    GameManager.GetHealthPotion(closestObject);
                    closestObject = null;
                    playerText.text = "";
                }
        }

        void PickUpChest()
        {
            if (closestObject != null)
                if (GameManager.InChestRange(closestObject))
                {
                    GameManager.PickUpChest(closestObject);
                    closestObject = null;
                    playerText.text = "";
                }
        }


        void AttackEnemy()
        {
            if (closestObject != null)
                if (GameManager.InMeleeRange(closestObject))
                {
                    GameManager.SwordAttack(closestObject);
                    closestObject = null;
                    playerText.text = "";
                }
        }


    }
}
