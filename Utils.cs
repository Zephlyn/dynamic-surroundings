using System;
using System.Linq;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;

namespace Utils {
    namespace ExtensionMethods {
        static class TaskExtensions {
            public static Task<TOutput> Then<TInput, TOutput>(this Task<TInput> task, Func<TInput, TOutput> func) {
                return task.ContinueWith((input) => func(input.Result));
            }
            public static Task Then(this Task task, Action<Task> func) {
                return task.ContinueWith(func);
            }
            public static Task Then<TInput>(this Task<TInput> task, Action<TInput> func) {
                return task.ContinueWith((input) => func(input.Result));
            }
        }
    }
    public class Util {
        public static Item SpawnItemSync(string itemId) {
            return SpawnItemSync(Catalog.GetData<ItemData>(itemId));
        }
        public static Item SpawnItemSync(ItemData itemData) {
            return SpawnItem(itemData).GetAwaiter().GetResult();
        }
        public static async Task<Item> SpawnItem(string itemId) {
            return await SpawnItem(Catalog.GetData<ItemData>(itemId));
        }
        public static Task<Item> SpawnItem(ItemData itemData) {
            var promise = new TaskCompletionSource<Item>();
            System.Action action = () => itemData.SpawnAsync(item => promise.SetResult(item));
            Task.Run(action);
            return promise.Task;
        }

        public static void PlayEffectAt(Vector3 Position, string EffectID) {
            EffectInstance inst = Catalog.GetData<EffectData>(EffectID).Spawn(Position, Quaternion.identity);
            inst.Play();
        }

        public static Vector3 GetClosestCreatureHead(Transform item) {
            var nearestCreatures = Creature.list
                    .Where(x => !x.faction.name.Equals("Player") && x.state != Creature.State.Dead);
            if (nearestCreatures.Count() == 0) {
                return item.position + item.position - Player.currentCreature.animator.GetBoneTransform(HumanBodyBones.Chest).transform.position;
            } else {
                return nearestCreatures
                    .Aggregate((a, x) => Vector3.Distance(a.transform.position, item.position) < Vector3.Distance(x.transform.position, item.position) ? a : x)
                    .animator.GetBoneTransform(HumanBodyBones.Head).position;
            }
        }

        public static Creature GetClosestCreature(Transform Source) {
            var nearestCreatures = Creature.list.Where(x => !x.faction.name.Equals("Player") && x.state != Creature.State.Dead);
            if (nearestCreatures.Count() == 0) {
                return null;
            } else {
                return nearestCreatures.Aggregate((a, x) => Vector3.Distance(a.transform.position, Source.position) < Vector3.Distance(x.transform.position, Source.position) ? a : x);
            }
        }
    }
}