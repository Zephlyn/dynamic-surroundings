using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;
using Utils;
using Wully.Extensions;
using Wully.Helpers;

namespace DynSurround {
    public class SpellMergeFire : SpellMergeData {
        public static BetterLogger log = BetterLogger.GetLogger(typeof(SpellMergeFire));

        public ItemData fireballItem;
        private EffectData fireballEffect;
        private EffectData fireballDeflectEffect;
        private DamagerData fireballDamager;
        public float fireballSpeed = 50.0f;
        public float fireballDelay = 2.0f;

        public float MaxSpawnAmount = 20f;
        public float MinSpawnAmount = 16f;
        public float ShotMinCharge = 0.9f;

        public Trigger CaptureTrigger;
        public bool BubbleActive;
        public string BubbleEffectId = "SpellFireBubble";
        public List<CollisionHandler> CapturedObjects = new List<CollisionHandler>();
        public EffectData BubbleEffectData;
        public static Vector3[] randomTorqueArray;
        public Vector3 randomTorqueRange = new Vector3(0.2f, 0.2f, 0.2f);
        public static float[] randomHeightArray;
        public float LiftMinForce = 0.3f;
        public float LiftMaxForce = 0.6f;
        public float LiftRagdollForceMultiplier = 2f;
        public float LiftDrag = 1f;
        public float BubbleEffectMaxScale = 15f;
        public float BubbleDuration = 3f;
        public AnimationCurve BubbleScaleCurveOverTime;
        public class CapturedObject {
            public CapturedObject(Rigidbody rigidbody) {
                this.rigidbody = rigidbody;
            }

            public CapturedObject(Item item) {
                rigidbody = item.rb;
                this.item = item;
            }

            public CapturedObject(RagdollPart ragdollPart) {
                rigidbody = ragdollPart.rb;
                this.ragdollPart = ragdollPart;
            }

            public Rigidbody rigidbody;

            public Item item;

            public RagdollPart ragdollPart;
        }


        public override void OnCatalogRefresh() {
            base.OnCatalogRefresh();
            if (BubbleEffectId != null && BubbleEffectId != "") {
                BubbleEffectData = Catalog.GetData<EffectData>(BubbleEffectId, true);
            }
            if (randomTorqueArray == null) {
                randomTorqueArray = new Vector3[5];
                randomTorqueArray[0] = new Vector3(Random.Range(-this.randomTorqueRange.x, this.randomTorqueRange.x), Random.Range(-this.randomTorqueRange.y, this.randomTorqueRange.y), Random.Range(-this.randomTorqueRange.z, this.randomTorqueRange.z));
                randomTorqueArray[1] = new Vector3(Random.Range(-this.randomTorqueRange.x, this.randomTorqueRange.x), Random.Range(-this.randomTorqueRange.y, this.randomTorqueRange.y), Random.Range(-this.randomTorqueRange.z, this.randomTorqueRange.z));
                randomTorqueArray[2] = new Vector3(Random.Range(-this.randomTorqueRange.x, this.randomTorqueRange.x), Random.Range(-this.randomTorqueRange.y, this.randomTorqueRange.y), Random.Range(-this.randomTorqueRange.z, this.randomTorqueRange.z));
                randomTorqueArray[3] = new Vector3(Random.Range(-this.randomTorqueRange.x, this.randomTorqueRange.x), Random.Range(-this.randomTorqueRange.y, this.randomTorqueRange.y), Random.Range(-this.randomTorqueRange.z, this.randomTorqueRange.z));
                randomTorqueArray[4] = new Vector3(Random.Range(-this.randomTorqueRange.x, this.randomTorqueRange.x), Random.Range(-this.randomTorqueRange.y, this.randomTorqueRange.y), Random.Range(-this.randomTorqueRange.z, this.randomTorqueRange.z));
            }
            if (randomHeightArray == null) {
                randomHeightArray = new float[5];
                randomHeightArray[0] = 0f;
                randomHeightArray[1] = 0.03f;
                randomHeightArray[2] = -0.04f;
                randomHeightArray[3] = 0.02f;
                randomHeightArray[4] = -0.05f;
            }
        }

        public override void Load(Mana mana) {
            base.Load(mana);

            fireballItem = Catalog.GetData<ItemData>("DynamicProjectile");
            fireballEffect = Catalog.GetData<EffectData>("SpellFireball");
            fireballDeflectEffect = Catalog.GetData<EffectData>("SpellFireBallHitBlade");
            fireballDamager = Catalog.GetData<DamagerData>("Fireball");

            BubbleScaleCurveOverTime = Catalog.GetData<SpellMergeGravity>("GravityMerge").bubbleScaleCurveOverTime;

            log.Info().Message("Loaded fire merge".Italics());
        }

        public override void Unload() {
            base.Unload();

            StopCapture();

            log.Info().Message("Unloaded fire merge".Italics());
        }

        public override bool CanMerge() {
            return !BubbleActive;
        }

        public override void Merge(bool active) {
            base.Merge(active);
            if(active) {
                log.Info().Message("Attempting to start capture for fire bubble...");
                StartCapture(5f);
            }
            if (!active) {
                Vector3 vector = Player.local.transform.rotation * PlayerControl.GetHand(Side.Left).GetHandVelocity();
                Vector3 vector2 = Player.local.transform.rotation * PlayerControl.GetHand(Side.Right).GetHandVelocity();
                if (vector.magnitude > SpellCaster.throwMinHandVelocity && vector2.magnitude > SpellCaster.throwMinHandVelocity) {
                    if (Vector3.Angle(vector, mana.casterLeft.magicSource.position - mana.mergePoint.position) < 45f || Vector3.Angle(vector2, mana.casterRight.magicSource.position - mana.mergePoint.position) < 45f) {
                        if (currentCharge >= ShotMinCharge) {
                            log.Info().Message("Stopped existing coroutine, started new one");
                            mana.StopCoroutine("BubbleCoroutine");
                            mana.StartCoroutine(BubbleCoroutine(BubbleDuration));
                            return;
                        } else {
                            StopCapture();
                            FireRing(true);
                            return;
                        }
                    }
                }
                StopCapture();
            }
        }

        public override void Update() {
            base.Update();
        }

        private void FireRing(bool HomingEnabled = true, bool DepleteCharge = true) {
            log.Info().Message("Ran fire ring");
            int FireballAmount = Mathf.RoundToInt(Mathf.Clamp(MaxSpawnAmount * currentCharge, MinSpawnAmount, MaxSpawnAmount));
            List<Item> Fireballs = new List<Item>();
            for (int i = 1; i <= FireballAmount; i++) {
                var offset = Quaternion.Euler(
                    Random.value * 360.0f,
                    Random.value * 360.0f,
                    Random.value * 360.0f) * Vector3.forward * 0.2f;
                fireballItem.SpawnAsync(Fireball => {
                    Fireball.transform.position = mana.mergePoint.position;
                    Fireball.transform.rotation = Quaternion.Euler(0f, 360 / FireballAmount * Fireballs.Count, 0f);
                    foreach (Item item in Fireballs) {
                        item.IgnoreObjectCollision(Fireball);
                    }
                    Fireball.IgnoreRagdollCollision(Player.currentCreature.ragdoll);

                    if (HomingEnabled) {
                        if (Util.GetClosestCreature(Fireball.transform) != null) {
                            ThrowFireballAtClosestEnemy(Fireball);
                        } else {
                            Fireball.rb.AddForce(Fireball.transform.forward * 35f, ForceMode.Impulse);
                            Fireball.Throw(1f, Item.FlyDetection.Forced);
                        }
                    } else {
                        Fireball.rb.AddForce(Fireball.transform.forward * 35f, ForceMode.Impulse);
                        Fireball.Throw(1f, Item.FlyDetection.Forced);
                    }

                    FireBallDespawn Despawn = Fireball.gameObject.AddComponent<FireBallDespawn>();
                    Despawn.StartCoroutine(Despawn.DespawnAfter(5f));
                    Fireballs.Add(Fireball);
                });
            }

            if(DepleteCharge)
                currentCharge = 0f;
        }

        public IEnumerator BubbleCoroutine(float duration) {
            log.Info().Message("Bubble coroutine start");
            BubbleActive = true;
            StopCapture();
            EffectInstance bubbleEffect = null;
            if (BubbleEffectData != null) {
                bubbleEffect = BubbleEffectData.Spawn(CaptureTrigger.transform.position, Quaternion.identity, null, null, true, System.Array.Empty<System.Type>());
                bubbleEffect.SetIntensity(0f);
                bubbleEffect.Play(0);
            }
            yield return new WaitForFixedUpdate();
            StartCapture(0f);
            CaptureTrigger.transform.SetParent(null);
            FireRing(false);
            float startTime = Time.time;
            while (Time.time - startTime < duration) {
                if (!CaptureTrigger) {
                    yield break;
                }
                float num = BubbleScaleCurveOverTime.Evaluate((Time.time - startTime) / duration);
                CaptureTrigger.SetRadius(num * BubbleEffectMaxScale * 0.5f);
                if (bubbleEffect != null) {
                    bubbleEffect.SetIntensity(num);
                }
                yield return null;
            }
            if (bubbleEffect != null) {
                bubbleEffect.End(false, -1f);
            }
            StopCapture();
            BubbleActive = false;
            yield break;
        }

        public void StartCapture(float radius) {
            log.Info().Message("Starting capture...".Italics());
            CaptureTrigger = new GameObject("FireTrigger").AddComponent<Trigger>();
            log.Info().Message("Created game object and added trigger component");
            CaptureTrigger.transform.SetParent(mana.mergePoint);
            log.Info().Message("Set parent");
            CaptureTrigger.transform.localPosition = Vector3.zero;
            log.Info().Message("Set position");
            CaptureTrigger.transform.localRotation = Quaternion.identity;
            log.Info().Message("Set rotation");
            CaptureTrigger.SetCallback(new Trigger.CallBack(OnTrigger));
            log.Info().Message("Added callback");
            CaptureTrigger.SetLayer(GameManager.GetLayer(LayerName.MovingObject));
            log.Info().Message("Set layer to MovingObject");
            CaptureTrigger.SetRadius(radius);
            log.Info().Message("Set radius");
            CaptureTrigger.SetActive(true);
            log.Info().Message("Activated");
            log.Info().Message("Started capture!".Italics());
        }

        public void StopCapture() {
            log.Info().Message("Ending capture...".Italics());

            if(CaptureTrigger != null) {
                CaptureTrigger.SetActive(false);
                log.Info().Message("Disabled trigger.");
            } else {
                log.Error().Message("Capture trigger is null. Function cannot continue.");
                return;
            }

            for (int i = CapturedObjects.Count - 1; i >= 0; i--) {
                CapturedObjects[i].RemovePhysicModifier(this);
                log.Info().Message("Removed physic modifier.");

                if (CapturedObjects[i].ragdollPart && CapturedObjects[i].ragdollPart.ragdoll != mana.creature.ragdoll) {
                    CapturedObjects[i].ragdollPart.ragdoll.RemoveNoStandUpModifier(this);
                    log.Info().Message("Removed creature modifier.");
                }

                if (CapturedObjects[i].item) {
                    foreach (Imbue imbue in CapturedObjects[i].item.imbues) {
                        SpellCastCharge magic = Catalog.GetData<SpellCastCharge>("Fire", true);
                        imbue.Transfer(magic, -imbue.maxEnergy);
                    }
                    log.Info().Message("Unimbued object.");
                }

                CapturedObjects.RemoveAt(i);
                log.Info().Message("Removed object from list.");
            }
            Object.Destroy(CaptureTrigger.gameObject);
            log.Info().Message("Destroyed trigger.");
            log.Info().Message("Ended capture!".Italics());
        }

        public void OnTrigger(Collider other, bool enter) {
            if (other.attachedRigidbody && !other.attachedRigidbody.isKinematic) {
                CollisionHandler component = other.attachedRigidbody.GetComponent<CollisionHandler>();
                if (component && (!component.item || component.item.data.type != ItemData.Type.Body)) {
                    if (enter) {
                        if (component.item || (BubbleActive && component.ragdollPart && component.ragdollPart.ragdoll != mana.creature.ragdoll)) {
                            if (component.ragdollPart && (component.ragdollPart.ragdoll.creature.state == Creature.State.Alive || component.ragdollPart.ragdoll.standingUp)) {
                                component.ragdollPart.ragdoll.SetState(Ragdoll.State.Destabilized);
                                component.ragdollPart.ragdoll.AddNoStandUpModifier(this);

                                ActionShock actionShock = component.ragdollPart.ragdoll.creature.brain.GetAction<ActionShock>();
                                if (actionShock != null) {
                                    actionShock.Refresh(40f, 5f);
                                } else {
                                    actionShock = new ActionShock(0.1f, BubbleDuration, Catalog.GetData<EffectData>("ImbueFireRagdoll"));
                                    component.ragdollPart.ragdoll.creature.brain.TryAction(actionShock, true);
                                }

                                fireballItem.SpawnAsync(Fireball => {
                                    Fireball.transform.position = CaptureTrigger.transform.position;
                                    Fireball.IgnoreRagdollCollision(Player.currentCreature.ragdoll);
                                    FireBallDespawn Despawn = Fireball.gameObject.AddComponent<FireBallDespawn>();
                                    Despawn.StartCoroutine(Despawn.DespawnAfter(5f));
                                    ThrowFireball(Fireball, (component.ragdollPart.ragdoll.creature.animator.GetBoneTransform(HumanBodyBones.Chest).position - Fireball.transform.position).normalized * 15.0f);
                                });
                            }
                            component.SetPhysicModifier(this, 2, 0f, 1f, LiftDrag, -1f, null);
                            Vector3 vector = -Physics.gravity.normalized * Mathf.Lerp(LiftMinForce, LiftMaxForce, Random.Range(0f, 1f));
                            if (component.ragdollPart) {
                                vector *= LiftRagdollForceMultiplier;
                            }
                            component.rb.AddForce(vector, ForceMode.VelocityChange);
                            component.rb.AddTorque(randomTorqueArray[Random.Range(0, 5)], ForceMode.VelocityChange);

                            if(BubbleActive) {
                                if(component.item && !component.item.IsHanded()) {
                                    foreach (Imbue imbue in component.item.imbues) {
                                        SpellCastCharge magic = Catalog.GetData<SpellCastCharge>("Fire", true);
                                        imbue.Transfer(magic, imbue.maxEnergy);
                                    }
                                }
                            } else {
                                if (component.item && !component.item.IsHanded()) {
                                    foreach (Imbue imbue in component.item.imbues) {
                                        SpellCastCharge magic = Catalog.GetData<SpellCastCharge>("Fire", true);
                                        imbue.Transfer(magic, imbue.maxEnergy * 0.25f);
                                    }
                                }
                            }

                            CapturedObjects.Add(component);

                            return;
                        }
                    } else {
                        component.RemovePhysicModifier(this);
                        if (component.ragdollPart && component.ragdollPart.ragdoll != mana.creature.ragdoll) {
                            component.ragdollPart.ragdoll.RemoveNoStandUpModifier(this);
                        }

                        if(component.item) {
                            foreach (Imbue imbue in component.item.imbues) {
                                SpellCastCharge magic = Catalog.GetData<SpellCastCharge>("Fire", true);
                                imbue.Transfer(magic, -imbue.maxEnergy);
                            }
                        }

                        CapturedObjects.Remove(component);
                    }
                }
            }
        }

        public void ThrowFireball(Item fireball, Vector3 velocity) {
            fireball.rb.isKinematic = false;

            foreach (CollisionHandler collisionHandler in fireball.collisionHandlers) {
                collisionHandler.SetPhysicModifier(this, 0, 0.0f);
                foreach (Damager damager in collisionHandler.damagers) {
                    damager.Load(fireballDamager, collisionHandler);
                }
            }
            ItemMagicProjectile component = fireball.GetComponent<ItemMagicProjectile>();
            if ((bool)(Object)component) {
                component.guided = false;
                component.speed = fireballSpeed;
                component.item.lastHandler = Player.currentCreature.handRight;
                component.allowDeflect = true;
                component.deflectEffectData = fireballDeflectEffect;
                component.imbueBladeEnergyTransfered = 50.0f;
                component.imbueSpellCastCharge = (SpellCastCharge)Player.currentCreature.mana.spells.Find(spell => spell.id == "Fire");
                component.Fire(velocity, fireballEffect, shooterRagdoll: Player.currentCreature.ragdoll);
            } else {
                fireball.rb.AddForce(velocity, ForceMode.Impulse);
                fireball.Throw(flyDetection: Item.FlyDetection.Forced);
            }
        }

        public void ThrowFireballAtClosestEnemy(Item fireball) {
            ThrowFireball(fireball, (Util.GetClosestCreatureHead(fireball.transform) - fireball.transform.position).normalized * 30.0f);
        }

        public void SpawnFireball(Transform merge, Vector3 velocity) {
            var offset = Quaternion.Euler(
                Random.value * 360.0f,
                Random.value * 360.0f,
                Random.value * 360.0f) * Vector3.forward * 0.2f;
            fireballItem.SpawnAsync(fireball => {
                fireball.transform.position = merge.position + offset;
                fireball.transform.localScale = Vector3.zero;
                fireball.rb.isKinematic = true;
                ThrowFireball(fireball, velocity);
            });
        }

        public void SpawnFireball(Transform merge, Collider[] ignoredColliders) {
            var offset = Quaternion.Euler(
                Random.value * 360.0f,
                Random.value * 360.0f,
                Random.value * 360.0f) * Vector3.forward * 0.2f;
            fireballItem.SpawnAsync(fireball => {
                fireball.transform.position = merge.position + offset;
                fireball.transform.localScale = Vector3.one;
                fireball.rb.isKinematic = true;
                foreach (Collider collider in ignoredColliders) {
                    foreach (Collider fireballCollider in fireball.GetComponentsInChildren<Collider>()) {
                        Physics.IgnoreCollision(collider, fireballCollider);
                    }
                }
                Task.Delay((int)fireballDelay).Wait();
                ThrowFireballAtClosestEnemy(fireball);
            });
        }
    }

    class FireBallDespawn : MonoBehaviour {
        protected Item item;
        public void Awake() {
            item = GetComponent<Item>();
        }

        public IEnumerator DespawnAfter(float Seconds) {
            yield return new WaitForSeconds(Seconds);
            item.Despawn();
        }
    }
}