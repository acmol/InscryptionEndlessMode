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
        private const string PluginGuid = "acmol.kcm.harder";
        private const string PluginName = "Harder Kaycee's Mod";
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

        private void Harder() {
            OnCardEmergeOnBoardEvent += GrizzlyModifier.Modify;
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
}
