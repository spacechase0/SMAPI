using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;
using StardewValley.ContentManagement;
using StardewValley.GameData;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_7;

/// <summary>Maps Stardew Valley 1.6.15's <see cref="LocalizedContentManager"/> methods to their newer form to avoid breaking older mods.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class LocalizedContentManagerFacade_1_7 : LocalizedContentManager, IRewriteFacade
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

    public virtual string PreprocessString(string text)
    {
        return base.PreprocessString(text, null);
    }


    /*********
     ** Public methods
     *********/
    public LanguageCode GetCurrentLanguage()
    {
        return base.LanguageCode;
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
        return (LocalizedContentManager)base.CreateTemporary();
    }


    /*********
    ** Private methods
    *********/
    private LocalizedContentManagerFacade_1_7()
        : base(null, null)
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
