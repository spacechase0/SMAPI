using System;
using System.Reflection;
using StardewModdingAPI.Framework.ContentManagers;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;
using StardewValley.ContentManagement;
using StardewValley.GameData;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6_16;

/// <summary>Maps Stardew Valley 1.6.15's <see cref="LocalizedContentManager"/> methods to their newer form to avoid breaking older mods.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class LocalizedContentManagerFacade_1_6_16 : LocalizedContentManager, IRewriteFacade
{
    /*********
    ** Accessors
    *********/
    public static string CurrentLanguageString => Game1.content.LanguageString;
    public static bool CurrentLanguageLatin => Game1.content.LanguageUsesLatinFont;
    public static ModLanguage CurrentModLanguage => Game1.content.LanguageModData;

    public static LanguageCode CurrentLanguageCode
    {
        get => Game1.content.LanguageCode;
        set => Game1.content.LanguageCode = value;
    }

    public static event LanguageChangedHandler OnLanguageChange
    {
        add => Game1.content.OnGlobalLanguageChanged += value;
        remove => Game1.content.OnGlobalLanguageChanged -= value;
    }


    /*********
    ** Public methods
    *********/
    public LanguageCode GetCurrentLanguage()
    {
        return this.LanguageCode;
    }

    public new static LanguageCode GetDefaultLanguageCode()
    {
        return Game1.content.GetDefaultLanguageCode();
    }

    public new static void SetModLanguage(ModLanguage new_mod_language)
    {
        Game1.content.SetModLanguage(new_mod_language);
    }

    public new LocalizedContentManager CreateTemporary()
    {
        //
        // Note: since method calls are redirected to this implementation, the context doesn't work as you'd expect.
        // In particular:
        //   - `base.CreateTemporary()` calls the vanilla base class (*not* the subclass we're rewriting).
        //   - `this is GameContentManager` is false even if `this.GetType() == typeof(GameContentManager)`.
        //   - Private fields (e.g. to cache the reflection) are shared between multiple instances, resulting in access
        //     violation exceptions.
        //

        Type type = this.GetType();

        if (type == typeof(GameContentManager))
            return (LocalizedContentManager)Game1.content.CreateTemporary(); // skip reflection if we can avoid it

        MethodInfo method = this.GetType().GetMethod(nameof(base.CreateTemporary))!;
        return (LocalizedContentManager)method.Invoke(this, [])!;
    }


    /*********
    ** Private methods
    *********/
    private LocalizedContentManagerFacade_1_6_16()
        : base(null, null)
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
