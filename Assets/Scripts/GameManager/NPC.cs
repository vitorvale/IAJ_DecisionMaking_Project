using UnityEngine;
using System.Collections;
using System;
using Assets.Scripts.IAJ.Unity.Utils;
using UnityEngine.AI;
using Assets.Scripts.IAJ.Unity.DecisionMaking.BehaviorTree;
using Assets.Scripts.IAJ.Unity.DecisionMaking.BehaviorTree.BehaviourTrees;

namespace Assets.Scripts.GameManager
{

    public class NPC : MonoBehaviour
    {

        public string Name { get; private set; }
        public string Type { get; private set; }
        // Stats
        public int XPvalue { get; private set; }
        public int HP { get; private set; }
        public int AC { get; private set; }
        public int simpleDamage { get; private set; }
        public int weaponRange { get; private set; }

        //how do you like lambda's in c#?
        public Func<int> dmgRoll;
        public float awakeDistance { get; private set; }
        public GameObject player { get; private set; }
        public GameManager manager { get; private set; }

        public GameObject[] patrol_targets { get; private set; }
        public ParticleSystem shoutEffect;
        public bool playerSpotted { get; set; }
        public bool chasing { get; set; }
        public bool spotter { get; set; }

        public bool usingBehaviourTree;
        public float decisionRate = 2.0f;

        private NavMeshAgent agent;

        public AudioSource shoutNoise;

        
        //The Behavior Tree
        private Task behaviourTree;
     
        void Start()
        {
            this.Name = this.transform.gameObject.name;
            this.Type = this.transform.gameObject.tag;
            agent = this.GetComponent<NavMeshAgent>();
            manager = GameObject.FindObjectOfType<GameManager>();
            player = GameObject.FindGameObjectWithTag("Player");
            playerSpotted = false;
            chasing = false;
            spotter = false;
            
            shoutNoise = GetComponent<AudioSource>();
            patrol_targets = new GameObject[10];
            patrol_targets.SetValue(GameObject.Find("Sphere1"), 0);
            patrol_targets.SetValue(GameObject.Find("Sphere2"), 1);
            patrol_targets.SetValue(GameObject.Find("Sphere3"), 2);
            patrol_targets.SetValue(GameObject.Find("Sphere4"), 3);
            patrol_targets.SetValue(GameObject.Find("Sphere5"), 4);
            patrol_targets.SetValue(GameObject.Find("Sphere6"), 5);



            switch (this.Type)
            {
                case "Skeleton":
                    this.XPvalue = 3;
                    this.AC = 10;
                    this.HP = 5;
                    this.dmgRoll = () => RandomHelper.RollD6();
                    this.simpleDamage = 2;
                    this.awakeDistance = 10;
                    this.weaponRange = 2;
                    break;
                case "Orc":
                    this.XPvalue = 10;
                    this.AC = 14;
                    this.HP = 15;
                    this.dmgRoll = () => RandomHelper.RollD10() +2;
                    this.simpleDamage = 5;
                    this.awakeDistance = 13;
                    this.weaponRange = 3;
                    shoutEffect = GetComponent<ParticleSystem>();
                    
                break;
                case "Dragon":
                    this.XPvalue = 20;
                    this.AC = 16;
                    this.HP = 30;
                    this.dmgRoll = () => RandomHelper.RollD12() + RandomHelper.RollD12();
                    this.simpleDamage = 10;
                    this.awakeDistance = 15;
                    this.weaponRange = 5;
                    break;
                default:
                    this.XPvalue = 3;
                    this.AC = 10;
                    this.HP = 5;
                    this.dmgRoll = () => RandomHelper.RollD6();
                    break;
            }

            // If we want the NPCs to use behavior trees
            if (manager.BehaviourTreeNPCs)
            {
                this.usingBehaviourTree = true;
                if (this.Type.Equals("Orc")) {
                    behaviourTree = new OrcTree(this, player, patrol_targets);

                }

                else
                    behaviourTree = new BasicTree(this, player);
            }

            // If the NPCs are wake we call this function every 1 secons
            if (!usingBehaviourTree && !manager.SleepingNPCs)
                Invoke("CheckPlayerPosition", 1.0f);


        }


        void FixedUpdate()
        {
            if (usingBehaviourTree)
                    this.behaviourTree.Run();
        }

        // Very basic Enemy AI
        void CheckPlayerPosition()
        {
            if (Vector3.Distance(this.transform.position, player.transform.position) < awakeDistance)
            {

                if (Vector3.Distance(this.transform.position, player.transform.position) <= weaponRange)
                {
                    AttackPlayer();
                }

                else
                {
                    Debug.Log("Pursuing Player");
                    PursuePlayer();
                    Invoke("CheckPlayerPosition", 0.5f);
                }
            }
            else
            {

                Invoke("CheckPlayerPosition", 3.0f);
            }
        }


        //These are the 3 basic actions the NPCs can make
        public void PursuePlayer()
        {
            if(agent != null)
                this.agent.SetDestination(player.transform.position);
        }

        public void AttackPlayer()
        {
            manager.EnemyAttack(this.gameObject);
        }

        public void MoveTo(Vector3 targetPosition)
        {
            if (agent != null)
                this.agent.SetDestination(targetPosition);
        }

       

     }
}
