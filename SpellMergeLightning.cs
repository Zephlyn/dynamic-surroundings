using System;
using System.Collections;
using ThunderRoad;
using UnityEngine;
using Utils;
using Wully.Helpers;

namespace DynSurround {
    public class SpellMergeLightning : SpellMergeData {
        public static BetterLogger log = BetterLogger.GetLogger(typeof(SpellMergeLightning));

        public bool SequentialLightning = false;

        public EffectInstance[] BoltEffectInstances;
        public int SimultanousBolts = 10;
        public Transform[] BoltTargets;

        public EffectInstance[] _BoltEffectInstances;
        public int _SimultanousBolts = 10;
        public Transform[] _BoltTargets;

        public EffectInstance[] __BoltEffectInstances;
        public int __SimultanousBolts = 10;
        public Transform[] __BoltTargets;

        public string BoltEffectID = "SpellLightningBolt";
        public EffectData BoltEffectData;
        public EffectInstance inst;

        public float ShotMinCharge = 0.4f;

        private bool Active;
        private float LastBoltHitTime;


        public override void OnCatalogRefresh() {
            base.OnCatalogRefresh();
            if (BoltEffectID != null && BoltEffectID != "") {
                BoltEffectData = Catalog.GetData<EffectData>(BoltEffectID, true);
            }
        }

        public override void Load(Mana mana) {
            base.Load(mana);

            BoltEffectInstances = new EffectInstance[SimultanousBolts];
            BoltTargets = new Transform[SimultanousBolts];

            _BoltEffectInstances = new EffectInstance[SimultanousBolts];
            _BoltTargets = new Transform[SimultanousBolts];

            __BoltTargets = new Transform[__SimultanousBolts];
            __BoltEffectInstances = new EffectInstance[__SimultanousBolts];

            for (int i = 0; i < SimultanousBolts; i++) {
                BoltTargets[i] = new GameObject("TargetBolt" + i).transform;
                BoltEffectInstances[i] = BoltEffectData.Spawn(mana.creature.transform, true, Array.Empty<Type>());
                BoltEffectInstances[i].SetTarget(BoltTargets[i]);

                _BoltTargets[i] = new GameObject("TargetBolt" + i).transform;
                _BoltEffectInstances[i] = BoltEffectData.Spawn(mana.creature.transform, true, Array.Empty<Type>());
                _BoltEffectInstances[i].SetTarget(_BoltTargets[i]);
            }

            log.Info().Message("Loaded lightning merge");
        }

        public override void Unload() {
            for (int i = 0; i < SimultanousBolts; i++) {
                UnityEngine.Object.Destroy(BoltTargets[i].gameObject);
                UnityEngine.Object.Destroy(_BoltTargets[i].gameObject);

                BoltEffectInstances[i].End(false, -1f);
                _BoltEffectInstances[i].End(false, -1f);
            }

            log.Info().Message("Unloading lightning merge");
            base.Unload();
        }

        public override void Merge(bool active) {
            base.Merge(active);

            if (active) {
                if (!Active) {
                    Active = true;
                }
            } else {
                Active = false;
            }

            if (!active) {
                Vector3 vector = Player.local.transform.rotation * PlayerControl.GetHand(Side.Left).GetHandVelocity();
                Vector3 vector2 = Player.local.transform.rotation * PlayerControl.GetHand(Side.Right).GetHandVelocity();
                if (vector.magnitude > SpellCaster.throwMinHandVelocity && vector2.magnitude > SpellCaster.throwMinHandVelocity) {
                    if (Vector3.Angle(vector, mana.casterLeft.magicSource.position - mana.mergePoint.position) < 45f || Vector3.Angle(vector2, mana.casterRight.magicSource.position - mana.mergePoint.position) < 45f) {
                        if (currentCharge >= ShotMinCharge) {
                            mana.StopCoroutine(LightningStorm());
                            mana.StartCoroutine(LightningStorm());
                        }
                    }
                }
            }
        }

        public IEnumerator LightningStorm() {
            int i = 0;
            Util.PlayEffectAt(mana.mergePoint.position, "LightningStrike");
            foreach (Creature npc in Creature.list) {
                if (Vector3.Distance(npc.transform.position, mana.mergePoint.position) <= 10 && npc != Player.currentCreature && !npc.isKilled) {
                    GameObject obj = new GameObject();
                    obj.transform.position = new Vector3(npc.animator.GetBoneTransform(HumanBodyBones.Head).position.x, npc.animator.GetBoneTransform(HumanBodyBones.Head).position.y + 15f, npc.animator.GetBoneTransform(HumanBodyBones.Head).position.z);
                    obj.name = $"BoltSourceFor{npc.name}";
                    //log.Info().Message($"Spawned bolt source ({obj.name})");

                    for (int z = 0; z < __SimultanousBolts; z++) {
                        __BoltTargets[z] = new GameObject("TargetBolt" + z).transform;
                        //log.Info().Message("Created bolt target number " + z);
                        __BoltEffectInstances[z] = BoltEffectData.Spawn(mana.creature.transform, true, Array.Empty<Type>());
                        //log.Info().Message("Spawned bolt effect ");
                        __BoltEffectInstances[z].SetTarget(__BoltTargets[z]);
                        //log.Info().Message("Set bolt target to " + __BoltTargets[z].name);

                        __BoltTargets[z].position = npc.animator.GetBoneTransform(HumanBodyBones.Hips).position;
                        //log.Info().Message($"Set  {BoltTargets[z].name}'s position to {npc.creatureId} {z.ToString()}'s position");
                        __BoltEffectInstances[z].SetSource(obj.transform);
                        //log.Info().Message($"Set bolt source to {obj.name}");

                        if(SequentialLightning) {
                            yield return new WaitForSecondsRealtime(0.02f);
                        }

                        __BoltEffectInstances[z].Play();
                        //log.Info().Message("Played bolt effect");

                        ActionShock actionShock = npc.brain.GetAction<ActionShock>();
                        if (actionShock != null) {
                            actionShock.Refresh(1f, 10f);
                        } else {
                            actionShock = new ActionShock(1f, 10f, Catalog.GetData<EffectData>("ImbueLightningRagdoll"));
                            npc.brain.TryAction(actionShock, true);
                        }

                        i++;
                    }

                    npc.ragdoll.SetState(Ragdoll.State.Destabilized);
                    npc.Damage(new CollisionInstance(new DamageStruct(DamageType.Energy, (npc.currentHealth * currentCharge) * 0.6f)));
                }
            }
            currentCharge = 0f;

            yield return new WaitForSecondsRealtime(2f);

            for (int z = 0; z < i; z++) {
                if (__BoltTargets != null) {
                    UnityEngine.Object.Destroy(__BoltTargets[z].gameObject);
                }

                if (__BoltEffectInstances != null) {
                    __BoltEffectInstances[z].End(false, -1f);
                }
            }
        }

        public override void Update() {
            base.Update();

            if (Active) {
                _BoltTargets[0].position = Player.currentCreature.animator.GetBoneTransform(HumanBodyBones.Chest).position;
                _BoltEffectInstances[0].SetSource(mana.mergePoint);
                if (Time.time - LastBoltHitTime > 0.3f) {
                    _BoltEffectInstances[0].Play(0);
                    LastBoltHitTime = Time.time;
                }
            }
        }
    }
}
