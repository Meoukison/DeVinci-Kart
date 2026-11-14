using System.Collections.Generic;
using UnityEngine;

namespace KartLab
{
    public enum ZoneKind { ItemBox, Boost, Slow, Checkpoint, OutOfBounds }
    public class TrackZone : MonoBehaviour
    {
        public ZoneKind kind;
        public ItemKind fixedItem;
        public Transform visual, spawn;
        public float respawnSeconds = 5;
        float availableAt;
        Vector3 visualHome;
        bool visualInitialized;
        readonly Dictionary<KartMotor, float> lastBoost = new();
        readonly HashSet<KartMotor> occupants = new();
        void Update()
        {
            if (kind != ZoneKind.ItemBox || !visual) return;
            if(!visualInitialized){visualHome=visual.localPosition;visualInitialized=true;}
            visual.gameObject.SetActive(Time.time >= availableAt);
            if (visual.gameObject.activeSelf)
            {visual.Rotate(0,Time.deltaTime*65,0);visual.localPosition=visualHome+Vector3.up*Mathf.Sin(Time.time*2.4f+transform.position.z)*.12f;}
        }
        void OnTriggerEnter(Collider other) => Touch(other);
        void OnTriggerStay(Collider other)
        { if (kind == ZoneKind.ItemBox || kind == ZoneKind.Slow) Touch(other); }
        void Touch(Collider other)
        {
            var motor = other.GetComponentInParent<KartMotor>();
            if (!motor || motor.Resetting) return;
            switch (kind)
            {
                case ZoneKind.ItemBox:
                    if (Time.time >= availableAt && motor.TryGetComponent<KartInventory>(out var inventory) && inventory.Pickup(fixedItem))
                        availableAt = Time.time + respawnSeconds;
                    break;
                case ZoneKind.Boost:
                    if (!lastBoost.TryGetValue(motor, out float next) || Time.time >= next)
                    { lastBoost[motor] = Time.time + 1; motor.ApplyBoost(1); }
                    break;
                case ZoneKind.Slow: occupants.Add(motor); motor.SetSurface(this, .6f); break;
                case ZoneKind.Checkpoint:
                    if (spawn) motor.SetCheckpoint(spawn.position, spawn.rotation);
                    break;
                case ZoneKind.OutOfBounds: motor.BeginReset(); break;
            }
        }
        void OnTriggerExit(Collider other)
        {
            var motor = other.GetComponentInParent<KartMotor>();
            if (motor && kind == ZoneKind.Slow) { motor.ClearSurface(this); occupants.Remove(motor); }
        }
        void OnDisable() { foreach (var motor in occupants) if (motor) motor.ClearSurface(this); occupants.Clear(); }
    }
}
