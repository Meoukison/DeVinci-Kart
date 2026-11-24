using System.Collections.Generic;
using UnityEngine;

namespace KartLab
{
    // Explicit values preserve existing serialized slot/box values.
    public enum ItemKind { None=0, Mushroom=1, Star=2, GreenShell=3, Banana=4, IceFlower=5 }
    public class KartInventory : MonoBehaviour
    {
        public GameObject boltPrefab, trapPrefab, icePrefab;
        public Texture2D[] icons;
        public ItemKind Held { get; private set; }
        public bool Rolling { get; private set; }
        public float useCooldown = .25f;
        [Tooltip("Durée de la roulette d'objets (s).")]
        public float rollDuration = .65f;
        public float RollStartTime { get; private set; }
        /// <summary>Objet sur lequel la roulette va s'arrêter.</summary>
        public ItemKind Pending => pending;
        public int racePosition=1, racerCount=1;
        public bool useRankWeights;
        public void SetRacePosition(int position,int total){racerCount=Mathf.Max(1,total);racePosition=Mathf.Clamp(position,1,racerCount);useRankWeights=racerCount>1;}
        ItemKind RollItem(){if(!useRankWeights||racerCount<2)return (ItemKind)Random.Range(1,6);float back=(racePosition-1f)/(racerCount-1);float roll=Random.value;return roll<Mathf.Lerp(.1f,.35f,back)?ItemKind.Mushroom:roll<Mathf.Lerp(.15f,.55f,back)?ItemKind.Star:roll<.73f?ItemKind.GreenShell:roll<Mathf.Lerp(.93f,.8f,back)?ItemKind.Banana:ItemKind.IceFlower;}
        public int Charges { get; private set; }
        float nextUse;
        ItemKind pending;
        readonly Queue<GameObject> traps = new();
        KartMotor motor;
        void Awake()
        {
            motor = GetComponent<KartMotor>();
            // Images perso : Resources/ItemIcons/Mushroom.png, Star.png, GreenShell.png, Banana.png, IceFlower.png
            for (int k = 1; k <= 5; k++)
            {
                var t = Resources.Load<Texture2D>("ItemIcons/" + (ItemKind)k);
                if (!t) continue;
                if (icons == null || icons.Length < 6) { var a = new Texture2D[6]; if (icons != null) System.Array.Copy(icons, a, icons.Length); icons = a; }
                icons[k] = t;
            }
        }
        public bool Pickup(ItemKind kind = ItemKind.None)
        {
            if (Held != ItemKind.None || Rolling) return false;
            pending = kind == ItemKind.None ? RollItem() : kind;
            Rolling = true; RollStartTime = Time.time; Invoke(nameof(FinishRoll), rollDuration); motor?.Notify("ITEM COLLECTED"); return true;
        }
        void FinishRoll() { Held = pending; Charges=Held==ItemKind.IceFlower?3:1; Rolling = false; motor?.Notify(Name(Held) + " READY"); }
        public static string Name(ItemKind kind) => kind switch
        { ItemKind.Mushroom=>"DASH MUSHROOM",ItemKind.Star=>"SUPER STAR",ItemKind.GreenShell=>"GREEN SHELL",ItemKind.Banana=>"BANANA",ItemKind.IceFlower=>"ICE FLOWER",_=>"EMPTY" };
        public Texture2D Icon(ItemKind kind) => icons!=null && (int)kind<icons.Length ? icons[(int)kind] : null;
        public bool Use(bool backward=false)
        {
            if (Held == ItemKind.None || Rolling || Time.time < nextUse || !motor || motor.Resetting) return false;
            if (Held == ItemKind.GreenShell && !boltPrefab || Held == ItemKind.Banana && !trapPrefab || Held==ItemKind.IceFlower&&!icePrefab) return false;
            switch (Held)
            {
                case ItemKind.Mushroom: motor.ApplyBoost(2.2f); break;
                case ItemKind.Star: motor.ApplyStar(5); break;
                case ItemKind.GreenShell:
                case ItemKind.IceFlower:
                    var bolt = Instantiate(Held==ItemKind.IceFlower?icePrefab:boltPrefab, transform.position + transform.forward * (backward?-1.5f:1.5f) + Vector3.up * .45f, transform.rotation*Quaternion.Euler(0,backward?180:0,0));
                    bolt.GetComponent<KartProjectile>().Launch(motor); break;
                case ItemKind.Banana:
                    var trap = Instantiate(trapPrefab, transform.position - transform.forward * 1.8f + Vector3.up * .04f, transform.rotation);
                    traps.Enqueue(trap);
                    while (traps.Count > 2) { var old = traps.Dequeue(); if (old) Destroy(old); }
                    break;
            }
            motor.Notify("USED " + Name(Held));
            Charges--; if(Charges<=0)Held = ItemKind.None;
            nextUse = Time.time + useCooldown; return true;
        }
    }
}
