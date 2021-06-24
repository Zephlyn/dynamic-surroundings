using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ThunderRoad;
using UnityEngine;
using Utils;
using Wully.Events;
using Wully.Extensions;
using Wully.Helpers;
using HarmonyLib;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DynSurround {
    public class LevelController : LevelModule {
        public static BetterLogger log = BetterLogger.GetLogger(typeof(LevelController));

        // Configurables
        public bool EnableExtraSpellEffects = true;
        public bool EnableImbueWhileHeld = true;
        public bool EnableLightningPunch = true;
        public bool EnableGravityPunch = true;
        public float HeldShockDamagePerSecond = 5f;
        public float PunchDelay = 2f;
        public float LightningPunchDamage = 5f;
        public float MagicPunchKnockback = 20f;
        public float GravityPunchDuration = 5f;
        public bool SectoryIntegration = false;

        private EffectInstance inst;
        EffectInstance inst1;
        private bool IsCreatureGrabbed = false;
        private Creature GrabbedCreature = null;
        private Side side;
        private Dictionary<Creature, float> ShockingCreatures;
        private string CurrentSpellRight;
        private string CurrentSpellLeft;

        public EffectInstance[] BoltEffectInstances;
        public int SimultanousBolts = 10;
        public Transform[] BoltTargets;

        public string BoltEffectId = "SpellLightningBolt";
        public EffectData BoltEffectData;

        private float lastPunchTimeLeft;
        private float lastPunchTimeRight;

        private DamagerData FireDamagerData;
        private string FireDamagerId = "Fireball";

        private Harmony harmony;

        public string GetCurrentSpellForSide(Side side) {
            switch (side) {
                case Side.Left:
                    return CurrentSpellLeft;
                case Side.Right:
                    return CurrentSpellRight;
            }
            return null;
        }


        public override IEnumerator OnLoadCoroutine(Level level) {
            if (System.IO.Directory.Exists(Application.streamingAssetsPath + "\\Mods\\!BetterMods")) {
                log.Info().Message("<color=cyan>Better mods detected!</color> Loading Dynamic Surroundings...".Italics());
                if (EnableImbueWhileHeld) {
                    BetterEvents.OnPlayerHandGrabRagdollPartHandle += BetterEvents_OnPlayerHandGrabRagdollPartHandle;
                    log.Info().Message("Set grab event");
                    BetterEvents.OnPlayerHandUnGrabRagdollPartHandle += BetterEvents_OnPlayerHandUnGrabRagdollPartHandle;
                    log.Info().Message("Set ungrab event");
                }

                EventManager.onLevelLoad += EventManager_onLevelLoad;
                log.Info().Message("Set level load event");
                EventManager.onPossess += EventManager_onPossess;
                log.Info().Message("Set possesion event");
                EventManager.onUnpossess += EventManager_onUnpossess;
                log.Info().Message("Set un-possess event");

                //BetterEvents.OnCreatureHitByPlayer += BetterEvents_OnCreatureHitByPlayer;

                if (!string.IsNullOrEmpty(BoltEffectId)) {
                    BoltEffectData = Catalog.GetData<EffectData>(BoltEffectId, true);
                    log.Info().Message("Set lightning effect data");
                }
                FireDamagerData = Catalog.GetData<DamagerData>(FireDamagerId, true);
                log.Info().Message("Set fire damager data");

                ShockingCreatures = new Dictionary<Creature, float>();
                log.Info().Message("Created dictionary");

                try {
                    harmony = new Harmony("DynSurround");
                    harmony.PatchAll(Assembly.GetExecutingAssembly());
                    log.Info().Message("<color=cyan>Harmony successfully loaded!</color>");
                } catch {
                    log.Error().Message("<color=red>Harmony did not load correctly. You will probably get lots of errors.</color>");
                }

                log.Info().Message("Loaded Dynamic Surroundings".Italics());
            }

            if (System.IO.Directory.Exists(Application.streamingAssetsPath + "\\Mods\\Sectory") && SectoryIntegration) {
                log.Info().Message("Dynamic Surroundings has sectory integration! Loading...");



                log.Info().Message("<color=cyan>Loaded sectory integration!</color>".Italics());
            }
                return base.OnLoadCoroutine(level);
        }

        #region Events
        /*private void BetterEvents_OnCreatureHitByPlayer(BetterHit betterHit) {
            if (!betterHit.creature.isKilled) return;
            if (betterHit.damageType != DamageType.Pierce) return;
            if (betterHit.collisionInstance.sourceColliderGroup.collisionHandler.item.imbues[0].spellCastBase.id != "Lightning") return;

            log.Info().Message("Stabbed dead creature with lightning imbued weapon");
            betterHit.creature.SetColor(Color.cyan, Creature.ColorModifier.EyesIris, true);
            betterHit.creature.SetColor(Color.cyan, Creature.ColorModifier.EyesIris, true);
            betterHit.creature.Resurrect(100f, Player.currentCreature);
            betterHit.creature.faction = Player.currentCreature.faction;
        }*/

        private void EventManager_onUnpossess(Creature creature, EventTime eventTime) {
            if(EnableGravityPunch || EnableLightningPunch) {
                Player.currentCreature.handRight.colliderGroup.collisionHandler.OnCollisionStartEvent -=
                    (CollisionInstance collision) => PunchHandler(Player.currentCreature.handRight, collision);

                Player.currentCreature.handLeft.colliderGroup.collisionHandler.OnCollisionStartEvent -=
                    (CollisionInstance collision) => PunchHandler(Player.currentCreature.handRight, collision);
            }
        }

        private void EventManager_onPossess(Creature creature, EventTime eventTime) {
            creature.StartCoroutine(DelayEvents(6f));
        }

        private IEnumerator DelayEvents(float Duration) {
            yield return new WaitForSecondsRealtime(Duration);

            if(EnableGravityPunch || EnableLightningPunch) {
                Player.currentCreature.handRight.colliderGroup.collisionHandler.OnCollisionStartEvent += (CollisionInstance collision) => PunchHandler(Player.currentCreature.handRight, collision);
                Player.currentCreature.handLeft.colliderGroup.collisionHandler.OnCollisionStartEvent += (CollisionInstance collision) => PunchHandler(Player.currentCreature.handLeft, collision);
            }
        }

        private void EventManager_onLevelLoad(LevelData levelData, EventTime eventTime) {
            switch (levelData.id) {
                case "Canyon":
                    Util.PlayEffectAt(Player.local.head.transform.position + new Vector3(0, 5, 0), "CanyonAmbience");
                    break;
                case "Arena":
                    Util.PlayEffectAt(Player.local.head.transform.position + new Vector3(0, 5, 0), "ArenaAmbience");
                    break;
                case "Market":
                    Util.PlayEffectAt(Player.local.head.transform.position + new Vector3(0, 5, 0), "MarketAmbience");
                    break;
            }
        }

        private void BetterEvents_OnPlayerHandUnGrabRagdollPartHandle(Side side, Handle handle, bool throwing) {
            IsCreatureGrabbed = false;

            if(ShockingCreatures.ContainsKey(GrabbedCreature)) {
                ShockingCreatures.Remove(GrabbedCreature);
                log.Info().Message("Removed grabbed creature from shocking dictionary");
            }
            GrabbedCreature = null;

            log.Info().Message("Released ragdoll");
        }

        private void BetterEvents_OnPlayerHandGrabRagdollPartHandle(Side side, Handle handle, float axisPosition, HandleOrientation orientation) {
            if (handle is HandleRagdoll handleRagdoll) {
                GrabbedCreature = handleRagdoll.ragdollPart.ragdoll.creature;
            }

            log.Info().Message("Grabbed ragdoll");
            if (Player.currentCreature.GetHand(side).caster.spellInstance.id == "Lightning" || Player.currentCreature.GetHand(side).caster.spellInstance.id == "Gravity" || Player.currentCreature.GetHand(side).caster.spellInstance.id == "Fire") {
                log.Info().Message("Hand has compatible spell equipped");
                IsCreatureGrabbed = true;
                this.side = side;
            }
        }

        public void PunchHandler(RagdollHand hand, CollisionInstance collision) {
            var collisionHandler = collision?.targetColliderGroup?.collisionHandler;
            if (collisionHandler && collisionHandler.isRagdollPart && collision.impactVelocity.magnitude > 3) {
                var creature = collision.targetColliderGroup.collisionHandler.ragdollPart.ragdoll?.creature;
                if (creature && creature != Player.currentCreature && Time.time - (hand.side == Side.Left ? lastPunchTimeLeft : lastPunchTimeRight) > PunchDelay) {
                    if (hand.side == Side.Left)
                        lastPunchTimeLeft = Time.time;
                    if (hand.side == Side.Right)
                        lastPunchTimeRight = Time.time;

                    var damageStruct = collision.damageStruct;
                    log.Info().Message("Creature was punched by player");
                    var punch = damageStruct.damager.collisionHandler;
                    log.Info().Message("Blunt item set");
                    var spell = Player.currentCreature.GetHand(hand.side).caster.spellInstance;
                    log.Info().Message("Spell var set");

                    if (spell is SpellCastLightning && EnableLightningPunch) {
                        log.Info().Message("Player had lightning equipped");
                        creature.ragdoll.SetState(Ragdoll.State.Destabilized);

                        BoltEffectInstances = new EffectInstance[SimultanousBolts];
                        BoltTargets = new Transform[SimultanousBolts];

                        for (int i = 0; i < SimultanousBolts; i++) {
                            BoltTargets[i] = new GameObject("TargetBolt" + i).transform;
                            BoltEffectInstances[i] = BoltEffectData.Spawn(Player.currentCreature.transform, true, Array.Empty<Type>());
                            BoltEffectInstances[i].SetTarget(BoltTargets[i]);

                            BoltTargets[i].position = creature.animator.GetBoneTransform(HumanBodyBones.UpperChest).position;
                            BoltEffectInstances[i].SetSource(Player.currentCreature.GetHand(hand.side).transform);
                            BoltEffectInstances[i].Play(0);
                        }

                        // Convert damage to shock, add a bit of bonus damage
                        damageStruct.damageType = DamageType.Energy;
                        damageStruct.damage += LightningPunchDamage;
                        damageStruct.knockOutDuration = 2f;
                        creature.Damage(new CollisionInstance(damageStruct, null, null));

                        ActionShock actionShock = creature.brain.GetAction<ActionShock>();
                        if (actionShock != null) {
                            actionShock.Refresh(0.1f, 5f);
                        } else {
                            actionShock = new ActionShock(0.1f, 5f, Catalog.GetData<EffectData>("ImbueLightningRagdoll"));
                            creature.brain.TryAction(actionShock, true);
                        }

                        log.Info().Message("Punch damager set to lightning");

                        // Apply bonus knockback
                        var direction = punch.rb.velocity.normalized;
                        direction *= MagicPunchKnockback;
                        foreach (RagdollPart ragdollPart in creature.ragdoll.parts) {
                            ragdollPart.rb.AddForce(direction, ForceMode.Impulse);
                        }

                        log.Info().Message("Added some extra force");
                    } else if (spell is SpellCastGravity && EnableGravityPunch) {
                        log.Info().Message("Player had gravity equipped");
                        creature.ragdoll.SetState(Ragdoll.State.Destabilized);

                        // Apply bonus knockback
                        var direction = punch.rb.velocity.normalized;
                        direction *= MagicPunchKnockback;
                        foreach (RagdollPart ragdollPart in creature.ragdoll.parts) {
                            ragdollPart.rb.AddForce(direction, ForceMode.Impulse);
                        }

                        creature.StopCoroutine("NoGravity");
                        creature.StartCoroutine(NoGravity(creature.ragdoll, GravityPunchDuration));

                        ActionShock actionShock = creature.brain.GetAction<ActionShock>();
                        if (actionShock != null) {
                            actionShock.Refresh(0.1f, GravityPunchDuration);
                        } else {
                            actionShock = new ActionShock(0.1f, GravityPunchDuration, Catalog.GetData<EffectData>("ImbueGravityRagdoll"));
                            creature.brain.TryAction(actionShock, true);
                        }
                        log.Info().Message("Added some extra force");
                    }
                }
            }
        }
        #endregion

        public override void Update(Level level) {
            base.Update(level);
            if (Player.currentCreature != null) {
                CurrentSpellRight = Player.currentCreature?.handRight?.caster?.spellInstance?.id;
                CurrentSpellLeft = Player.currentCreature?.handLeft?.caster?.spellInstance?.id;
            }

            #region Spell improvements
            // Gravity
            if (Player.local.creature != null && EnableExtraSpellEffects == true) {
                if (CurrentSpellLeft == "Gravity" && Player.local.creature.handLeft.caster != null && Player.local.creature.handLeft.caster.isFiring) {
                    if (inst == null) {
                        inst = Catalog.GetData<EffectData>("ImbueGravityRagdoll").Spawn(Player.currentCreature.ragdoll.rootPart.transform, true, Array.Empty<Type>());
                        inst.SetRenderer(Player.currentCreature.GetRendererForVFX(), false);
                        inst.SetIntensity(1f);
                        inst.Play(0);
                    }
                } else if (CurrentSpellRight == "Gravity" && Player.local.creature.handRight.caster != null && Player.local.creature.handRight.caster.isFiring) {
                    if (inst == null) {
                        inst = Catalog.GetData<EffectData>("ImbueGravityRagdoll").Spawn(Player.currentCreature.ragdoll.rootPart.transform, true, Array.Empty<Type>());
                        inst.SetRenderer(Player.currentCreature.GetRendererForVFX(), false);
                        inst.SetIntensity(1f);
                        inst.Play(0);
                    }
                } else if (CurrentSpellLeft == "Lightning" && Player.local.creature.handLeft.caster != null && Player.local.creature.handLeft.caster.isFiring) {
                    if (inst == null) {
                        inst = Catalog.GetData<EffectData>("ImbueLightningRagdoll").Spawn(Player.currentCreature.ragdoll.rootPart.transform, true, Array.Empty<Type>());
                        inst.SetRenderer(Player.currentCreature.GetRendererForVFX(), false);
                        inst.SetIntensity(1f);
                        inst.Play(0);
                    }
                } else if (CurrentSpellRight == "Lightning" && Player.local.creature.handRight.caster != null && Player.local.creature.handRight.caster.isFiring) {
                    if (inst == null) {
                        inst = Catalog.GetData<EffectData>("ImbueLightningRagdoll").Spawn(Player.currentCreature.ragdoll.rootPart.transform, true, Array.Empty<Type>());
                        inst.SetRenderer(Player.currentCreature.GetRendererForVFX(), false);
                        inst.SetIntensity(1f);
                        inst.Play(0);
                    }
                } else {
                    inst?.End(false, -1);
                    inst = null;
                }
            }

            if (IsCreatureGrabbed == true) {
                if (GetCurrentSpellForSide(side) == "Gravity" || GetCurrentSpellForSide(side) == "Fire" || GetCurrentSpellForSide(side) == "Lightning") {
                    if (PlayerControl.GetHand(side).usePressed) {
                        if (GetCurrentSpellForSide(side) == "Gravity") {
                            level.StopCoroutine("NoGravity");
                            level.StartCoroutine(NoGravity(GrabbedCreature.ragdoll, 5f));

                            ActionShock actionShock = GrabbedCreature.brain.GetAction<ActionShock>();
                            if (actionShock != null) {
                                actionShock.Refresh(40f, 5f);
                            } else {
                                actionShock = new ActionShock(0.1f, 5f, Catalog.GetData<EffectData>("ImbueGravityRagdoll"));
                                GrabbedCreature.brain.TryAction(actionShock, true);
                            }
                        } else {
                            ActionShock actionShock = GrabbedCreature.brain.GetAction<ActionShock>();
                            if (actionShock != null) {
                                actionShock.Refresh(40f, 2f);
                            } else {
                                actionShock = new ActionShock(40f, 2f, Catalog.GetData<EffectData>($"Imbue{GetCurrentSpellForSide(side)}Ragdoll"));
                                GrabbedCreature.brain.TryAction(actionShock, true);
                            }
                        }

                        if (!ShockingCreatures.ContainsKey(GrabbedCreature) && GetCurrentSpellForSide(side) != "Gravity" && !GrabbedCreature.isKilled) {
                            ShockingCreatures.Add(GrabbedCreature, 0f);
                        }

                        if (inst1 == null && EnableExtraSpellEffects == true) {
                            if (!(Player.currentCreature is null)) {
                                inst1 = Catalog.GetData<EffectData>($"Imbue{GetCurrentSpellForSide(side)}Ragdoll")
                                    .Spawn(Player.currentCreature.ragdoll.rootPart.transform, true,
                                        Array.Empty<Type>());
                                inst1?.SetRenderer(Player.currentCreature.GetRendererForVFX(), false);
                            }

                            inst1?.SetIntensity(1f);
                            inst1?.Play(0);
                        }
                    } else {
                        inst1?.End(false, -1);
                        inst1 = null;
                    }
                }
            } else {
                inst1?.End(false, -1);
                inst1 = null;
            }
            #endregion

            foreach (Creature npc in ShockingCreatures.Keys.ToList()) {
                ShockingCreatures[npc] += HeldShockDamagePerSecond * Time.deltaTime;
                if (ShockingCreatures[npc] >= npc.currentHealth) {
                    StopShocking(npc);
                }
            }
        }

        internal void DamageCreature(Creature creature, float amount) {
            var collisionInstance = new CollisionInstance(
                new DamageStruct(DamageType.Blunt, amount), null, null) {
                casterHand = Player.local?.creature?.mana?.casterLeft
            };
            if (!creature.isKilled) { creature.Damage(collisionInstance); }
        }

        private void StopShocking(Creature creature)
        {
            if (!ShockingCreatures.ContainsKey(creature)) return;
            DamageCreature(creature, ShockingCreatures[creature]);
            ShockingCreatures.Remove(creature);
        }

        private IEnumerator NoGravity(Ragdoll ragdoll, float duration) {
            ragdoll.SetPhysicModifier(this, 1, 0.1f, 0.5f, -1f, -1f);
            ragdoll.SetState(Ragdoll.State.Destabilized);
            yield return new WaitForSeconds(duration);
            ragdoll.RemovePhysicModifier(this);
            yield break;
        }

        #region NotHarmony
        // Alt use
        [HarmonyPatch]
        private class LightningStaffPatch {
            [HarmonyReversePatch]
            [HarmonyPatch(typeof(SpellCastCharge), nameof(SpellCastCharge.OnCrystalUse))]
            [MethodImpl(MethodImplOptions.NoInlining)]
            static void Dummy(SpellCastLightning instance) { return; }

            [HarmonyPatch(typeof(SpellCastCharge), nameof(SpellCastCharge.OnCrystalUse))]
            private static void Prefix(SpellCastLightning __instance, Side side, bool active) {
                Dummy(__instance);
                log.Info().Message("Spell use with lightning spell");
            }
        }

        // Ground slam
        [HarmonyPatch]
        private class LightningSlamPatch {
            [HarmonyReversePatch]
            [HarmonyPatch(typeof(SpellCastCharge), nameof(SpellCastCharge.OnImbueCollisionStart))]
            [MethodImpl(MethodImplOptions.NoInlining)]
            static void Dummy(SpellCastLightning instance) { return; }

            [HarmonyPatch(typeof(SpellCastCharge), nameof(SpellCastCharge.OnImbueCollisionStart))]
            private static void Postfix(SpellCastLightning __instance, CollisionInstance collisionInstance) {
                Dummy(__instance);
                log.Info().Message("Ground slam with lightning spell");
            }
        }
        #endregion
    }
}