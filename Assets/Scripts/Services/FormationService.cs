using System;
using System.Collections.Generic;
using SanguoTowerDefense.Domain;

namespace SanguoTowerDefense.Services
{
    /// <summary>Manages fixed defensive slots and supports manual hero switching.</summary>
    public sealed class FormationService
    {
        private readonly HeroRoster roster;
        private readonly string[] slots;

        public FormationService(HeroRoster roster, int slotCount = 5)
        {
            this.roster = roster ?? throw new ArgumentNullException(nameof(roster));
            if (slotCount < 1) throw new ArgumentOutOfRangeException(nameof(slotCount));
            slots = new string[slotCount];
        }

        public int SlotCount => slots.Length;

        public string GetHeroInstanceId(int slotIndex)
        {
            ValidateSlot(slotIndex);
            return slots[slotIndex];
        }

        public bool TrySetHero(int slotIndex, string instanceId, out string error)
        {
            error = null;
            ValidateSlot(slotIndex);
            if (string.IsNullOrWhiteSpace(instanceId)) { slots[slotIndex] = null; return true; }
            if (!roster.Contains(instanceId)) { error = "该武将不在当前收藏中。"; return false; }

            for (var i = 0; i < slots.Length; i++)
                if (i != slotIndex && slots[i] == instanceId)
                { error = "同一个武将副本不能同时部署到多个点位。"; return false; }

            slots[slotIndex] = instanceId;
            return true;
        }

        public bool TrySwitchHero(int slotIndex, string replacementInstanceId, out string error) =>
            TrySetHero(slotIndex, replacementInstanceId, out error);

        public IReadOnlyList<string> GetSnapshot() => Array.AsReadOnly((string[])slots.Clone());

        private void ValidateSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }
    }
}
