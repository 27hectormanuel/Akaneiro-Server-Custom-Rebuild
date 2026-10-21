using System;
using System.Collections.Generic;
using System.Linq;
using Akaneiro.Server.Data;

namespace Akaneiro.Server.Game
{
    /// <summary>
    /// Damage rules. The original server's formulas are lost; these are rebuilt from the client's
    /// data tables so the numbers at least come from the game's own balancing:
    ///   weapon hit  = random(DamageRange) * DamageModifier(item level)   [WeaponAttribute.weapon/.stat]
    ///   ability hit = BaseVal + AbilityBonus * attribute + AttackCoefficient * weapon hit  [Ability.Ability]
    /// </summary>
    public static class Combat
    {
        public static int WeaponDamage(CharacterData character, GameData data, Random rng)
        {
            ItemData weapon = character.Equipment.FirstOrDefault(e => e.Slot == 6);
            if (weapon == null || !data.Weapons.TryGetValue(weapon.ItemId, out GameData.WeaponRow row))
                return 1;

            float modifier = data.WeaponDamageModifier.TryGetValue(weapon.Level, out float m) ? m : 0.05f;
            int roll = rng.Next(row.DamageMin, Math.Max(row.DamageMin, row.DamageMax) + 1);
            return Math.Max(1, (int)Math.Round(roll * modifier));
        }

        /// <summary>
        /// Armor of the worn armor and accessories as the tooltips show it (minDef * level
        /// modifier), plus 5 per point of DEF (GameConfig.xml DisciplineMultiplier Defense="5").
        /// </summary>
        public static int Armor(CharacterData character, GameData data, PlayerStats stats = null)
        {
            float armor = (stats?.Defense ?? Views.BaseStats(character).Defense) * DefenseMultiplier;
            foreach (ItemData item in character.Equipment)
            {
                if (!data.ItemArmor.TryGetValue(item.ItemId, out float baseArmor))
                    continue;
                bool accessory = data.Items.TryGetValue(item.ItemId, out GameData.ItemProps props)
                                 && props.Type is GameData.ItemType.Neck or GameData.ItemType.Ring;
                var modifiers = accessory ? data.AccessoryModifier : data.ArmorModifier;
                armor += baseArmor * (modifiers.TryGetValue(item.Level, out float m) ? m : 0.05f);
            }
            return (int)armor;
        }

        private const int DefenseMultiplier = 5;

        /// <summary>
        /// How much of a monster's hit armor takes away: armor / (armor + 2000) (GameConfig.xml
        /// CharAttr ArmorBase="2000"). The original formula is lost; this is the usual reading of
        /// that constant and leaves about 9 % on level 1 and grows with gear and DEF.
        /// </summary>
        public static float DamageReduction(CharacterData character, GameData data, PlayerStats stats = null)
        {
            int armor = Armor(character, data, stats);
            float reduction = armor <= 0 ? 0f : armor / (armor + ArmorBase);
            // Fortified enchantments add their percent on top.
            reduction += (stats?.DamageReductionPct ?? 0f) / 100f;
            return Math.Clamp(reduction, 0f, MaxDamageReduction);
        }

        /// <summary>Nobody becomes untouchable (assumed cap).</summary>
        private const float MaxDamageReduction = 0.75f;

        /// <summary>
        /// A player hit after the bonuses: damage percent (POW, Raging enchantments) and the
        /// critical roll. Returns the damage and whether it was critical.
        /// </summary>
        public static (int Damage, bool Critical) Boost(int damage, PlayerStats stats, Random rng)
        {
            if (stats == null)
                return (damage, false);
            double boosted = damage * (1 + stats.DamagePct / 100.0);
            bool critical = rng.NextDouble() * 100 < stats.CritChance;
            if (critical)
                boosted *= stats.CritDamage / 100.0;
            return (Math.Max(1, (int)Math.Round(boosted)), critical);
        }

        private const float ArmorBase = 2000f;

        public static int AbilityDamage(GameData.AbilityRow ability, CharacterData character, GameData data,
            IReadOnlyDictionary<int, int> attributes, Random rng)
        {
            int weaponHit = WeaponDamage(character, data, rng);
            int attributeValue = attributes.TryGetValue(ability.AttrType, out int v) ? v : 0;
            double damage = ability.BaseVal
                            + ability.AbilityBonus * attributeValue
                            + ability.AttackCoefficient * weaponHit;
            return Math.Max(1, (int)Math.Round(damage));
        }
    }
}
