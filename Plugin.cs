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
        private const string PluginVersion = "0.0.4";

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
            Log.LogInfo($"region count = {regions.Count()}");

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

        static public void starvation_add_reach(PlayableCard card) { 
            if (card.Info.name == "Starvation") {
                card.AddTemporaryMod(new CardModificationInfo(Ability.Reach));
            }
        }

        static public void grizzly_add_stone(PlayableCard card) { 
            if (card.Info.name == "Grizzly") {
                card.AddTemporaryMod(new CardModificationInfo(Ability.MadeOfStone));
            }
        }

        // static public void grizzly_add_uncutable(PlayableCard card) {
        //     if (card.Info.name == "Grizzly") {
        //         card.Info.traits.Add(Trait.Uncuttable);
        //     }
        // }

        public delegate void CardModify(PlayableCard card);

        static public void Modify(ref PlayableCard card) {
            var info = card.Info;
            API.Log.LogInfo($"{info.name}:  isOpponent: {card.OpponentCard}");
            int level = RunState.Run.regionTier;
            if (level > RunState.Run.regionOrder.Count()) {
                // Final Boss
                API.Log.LogInfo($"meet final boss");
                return;
            }            
            
            var current_node = RunState.Run.map.nodeData.Find((NodeData x) => x.id == RunState.Run.currentNodeId);
            var mod_func = new List<CardModify> {grizzly_add_stone, starvation_add_reach};

            if (current_node is BossBattleNodeData) {
                // Boss Level
                if (card.OpponentCard) {
                    if (info.name == "Grizzly") {
                        API.Log.LogInfo($"modify Grizzly");
                        var mod = new CardModificationInfo(level * 4, level * 6);
                        card.AddTemporaryMod(mod);
                    }
                    if (level >= 3 && level <= 4) {
                        if (info.name == "Starvation" || info.name == "Grizzly") {
                            card.AddTemporaryMod(new CardModificationInfo(Ability.Deathtouch));
                        }
                    }

                    if (level >= 5 && level <= 7) {
                        var mod = mod_func[UnityEngine.Random.Range(0, mod_func.Count())];
                        mod(card);
                    }

                    if (level == 8) {
                        foreach (var mod in mod_func) {
                            mod(card);
                        }
                    }
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

    [HarmonyPatch(typeof(MapGenerator), "CreateNode")]
    class MapGenerator_CreateNode{
        public static void Postfix(ref NodeData __result) {
            API.Log.LogInfo($"Run Create Node");

            if (__result is CardBattleNodeData) {
                 var battle_node = (CardBattleNodeData)__result;
                 if (battle_node.difficulty > 15) {
                    battle_node.difficulty = 15;
                 }
            }
        }

    }

}
