using Microsoft.Xna.Framework.Input;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6_16;

/// <summary>Maps Stardew Valley 1.6.15's <see cref="Response"/> methods to their newer form to avoid breaking older mods.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class ResponseFacade : Response, IRewriteFacade
{
    /*********
    ** Public methods
    *********/
    public Response SetHotKey(Keys key)
    {
        // note: the original code mutated the instance *and* returned it. Changing the current instance is no longer
        // possible since it's immutable, so this only really works for mods which used the return value correctly.

        return new Response(this.responseKey, this.responseText, key);
    }


    /*********
    ** Private methods
    *********/
    private ResponseFacade()
        : base(null, null)
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
