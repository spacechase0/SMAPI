using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley.TerrainFeatures;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6_16;

/// <summary>Maps Stardew Valley 1.6.15's <see cref="Bush"/> methods to their newer form to avoid breaking older mods.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class BushFacade_1_6_16 : Bush, IRewriteFacade
{
    /*********
    ** Public methods
    *********/
    public void draw(SpriteBatch spriteBatch, float yDrawOffset)
    {
        if (this.drawOffset.Y != yDrawOffset)
            this.drawOffset = new Point(this.drawOffset.X, this.drawOffset.Y);

        base.draw(spriteBatch);
    }


    /*********
    ** Private methods
    *********/
    private BushFacade_1_6_16()
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
