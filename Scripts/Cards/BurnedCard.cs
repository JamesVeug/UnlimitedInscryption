using System.Collections.Generic;
using APIPlugin;
using DiskCardGame;
using UnlimitedInscryption.Scripts.Backgrounds;
using UnlimitedInscryption.Scripts.Sigils;

namespace UnlimitedInscryption.Scripts.Cards
{
    public static class BurnedCard
    {
        private const string ID = "BurnedCard";

        private const int BaseAttack = 0;
        private const int BaseHealth = 1;
        private const int BloodCost = 0;
        private const int BoneCost = 0;

        public static void Initialize()
        {
            List<CardMetaCategory> metaCategories = new List<CardMetaCategory>();
            List<CardAppearanceBehaviour.Appearance> appearanceBehaviour = new List<CardAppearanceBehaviour.Appearance>();
            appearanceBehaviour.Add(BurnedBackground.CustomAppearance);
            
            List<Ability> abilities = new List<Ability>{ };
            abilities.Add(DeadAbility.ability);

            NewCard.Add(name: ID,
                displayedName: "",
                baseAttack: BaseAttack,
                baseHealth: BaseHealth,
                metaCategories: metaCategories,
                cardComplexity: CardComplexity.Simple,
                temple: CardTemple.Nature,
                description: "",
                bloodCost: BloodCost,
                bonesCost: BoneCost,
                abilities: abilities,
                appearanceBehaviour: appearanceBehaviour);
        }
    }
}