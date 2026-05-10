using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;
using StardewValley.Menus;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6_16;

/// <summary>Maps Stardew Valley 1.6.15's <see cref="TextBox"/> methods to their newer form to avoid breaking older mods.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public abstract class TextBoxFacade : TextBox, IRewriteFacade
{
    /*********
    ** Public methods
    *********/
    public void RecieveCommandInput(char command)
    {
        base.RecieveCommandInput(command, KeyboardModifier.None);
    }

    public void RecieveSpecialInput(Keys key)
    {
        base.RecieveSpecialInput(key, KeyboardModifier.None);
    }


    /*********
    ** Private methods
    *********/
    private TextBoxFacade()
        : base(null, null, null, Color.Transparent)
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
