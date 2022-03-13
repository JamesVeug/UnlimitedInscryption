using APIPlugin;
using DiskCardGame;

namespace UnlimitedInscryption.Scripts.Backgrounds
{
    public class BurnedBackground : CardAppearanceBehaviour
    {
        private const string TextureFile = "Artwork/Backgrounds/card_burned.png";

        public static Appearance CustomAppearance;

        public static void Initialize()
        {
            NewCardAppearanceBehaviour newBackgroundBehaviour = NewCardAppearanceBehaviour.AddNewBackground(typeof(BurnedBackground), "BurnedBackground");
            CustomAppearance = newBackgroundBehaviour.Appearance;
        }
        
        public override void ApplyAppearance()
        {
            base.Card.RenderInfo.baseTextureOverride = Utils.GetTextureFromPath(TextureFile);
            base.Card.RenderInfo.hidePortrait = true;
        }
    }
}