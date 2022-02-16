using BepInEx;
using BepInEx.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;
using DiskCardGame;
using HarmonyLib;
using UnityEngine;

namespace HarderKCM
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class API : BaseUnityPlugin
    {
        private const string PluginGuid = "acmol.kcm.endless";
        private const string PluginName = "Kaycee's Endless Mode";
        private const string PluginVersion = "0.0.1";

        private void Awake()
        {
            Log = Logger;
            Logger.LogInfo($"Loaded {PluginName}!");
            foreach (var card in ScriptableObjectLoader<CardInfo>.AllData) {
                var name = card.name;
                Logger.LogInfo($"Load card {name} !");
                if (card.name == "Squirrel") {
                    // speedup for testing 
                    // set_card_info(card, 15, 15);
                }
            }
            Harmony harmony = new Harmony(PluginGuid);
            harmony.PatchAll();
            Harder();
        }
        static public ManualLogSource Log = null;

        static public void set_card_info(CardInfo card, int base_attack, int base_health) {
            var baseTraverse = Traverse.Create(card);
				    baseTraverse.Field("baseAttack").SetValue(base_attack);
				    baseTraverse.Field("baseHealth").SetValue(base_health);
            var a = baseTraverse.Field("baseAttack").GetValue<int>();
            var h = baseTraverse.Field("baseHealth").GetValue<int>();
            Log.LogInfo($"set card {card.name} base attack and heath {a} {h}");
        }
        
        static public HashSet<PlayableCard> cardsAlreadyEmergedOnBoard = new HashSet<PlayableCard>();

        static public void OnCardEmergeOnBoard(ref PlayableCard card) {
            if (!cardsAlreadyEmergedOnBoard.Contains(card)) {
                cardsAlreadyEmergedOnBoard.Add(card);
                OnCardEmergeOnBoardEvent(ref card);
            }
        }
        public delegate void PlayableCardCallback(ref PlayableCard card);

        // This method is called when all property has been set 
        // Every card will be called exactly once.
        public static event PlayableCardCallback OnCardEmergeOnBoardEvent;

        public static int MaxDifficulty = 100;
        public static int RepeatLevel = 3;
        private void Harder() {
            // Grizzly is stronger now
            OnCardEmergeOnBoardEvent += GrizzlyModifier.Modify;

            // support for endless levels
            var regions = RegionProgression.Instance.regions;
            foreach (var region in regions) {
                foreach (var encounter in region.encounters) {
                    encounter.maxDifficulty = MaxDifficulty;
                    foreach (var turn in encounter.turns) {
                        foreach(var card_blueprint in turn) {
                            var min_difficulty = card_blueprint.DifficultyRange.x;
                            var max_difficulty = card_blueprint.DifficultyRange.y;
                            card_blueprint.DifficultyRange = new Vector2(min_difficulty, MaxDifficulty);
                        }
                    }
                }
            }

            List<RegionData> to_insert = regions.GetRange(0, 3);
            for (int i = 0 ; i < RepeatLevel - 1; ++i) {
                regions.InsertRange(3, to_insert);
            }
            Log.LogInfo($"region count = {regions.Count()}");
        }
    }

    [HarmonyPatch(typeof (BoardManager), "ResolveCardOnBoard")]
    class BoardManager_AssignCardToSlot {

        static public void Postfix(ref PlayableCard card) {
            API.OnCardEmergeOnBoard(ref card);
        }
    }

    [HarmonyPatch(typeof (BoardManager), "QueueCardForSlot")]
    class BoardManager_CreateCardInSlot {

        static public void Postfix(ref PlayableCard card) {
            API.OnCardEmergeOnBoard(ref card);
        }
    }

    class GrizzlyModifier {
        static public void Modify(ref PlayableCard card) {
            var info = card.Info;
            API.Log.LogInfo($"{info.name}:  isOpponent: {card.OpponentCard}");

            if (RunState.Run.regionTier > RunState.Run.regionOrder.Count()) {
                // Final Boss
                API.Log.LogInfo($"meet final boss");
                return;
            }

            var current_node = RunState.Run.map.nodeData.Find((NodeData x) => x.id == RunState.Run.currentNodeId);
            if (current_node is BossBattleNodeData) {
                // Boss Level
                var n = RunState.CurrentRegionTier;
                if (info.name == "Grizzly" && card.OpponentCard) {
                    API.Log.LogInfo($"modify Grizzly");
                    var mod = new CardModificationInfo(n * 4, n * 6);
                    card.AddTemporaryMod(mod);
                }

            } else {
                var type = current_node.GetType();
                API.Log.LogInfo($"{type}");
            }
            API.Log.LogInfo($"MODIFY END");
        }
    }

    [HarmonyPatch(typeof(AscensionSaveData), "RollCurrentRunRegionOrder")]
    class AscensionSaveData_RollCurrentRunRegionOrder{

        private static List<int> GenRandomOrder(int n) {
            List<int> source = new List<int>
			{
				n * 3,
				n * 3 + 1,
				n * 3 + 2
			};
            return new List<int>(from a in source
                orderby UnityEngine.Random.Range(0, 100)
                select a);
        }
        public static void Postfix(AscensionSaveData __instance) {
            API.Log.LogInfo($"Roll Region Order");
            List<int> order = new List<int>();
            for (int i = 0; i != API.RepeatLevel; ++i) {
                order.AddRange(GenRandomOrder(i));
            }
            __instance.currentRun.regionOrder = order.ToArray();
            API.Log.LogInfo($"Roll Region Order {__instance.currentRun.regionOrder.Count()}");
        }
    }

}
