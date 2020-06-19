using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewModdingAPI.Enums;
using StardewModdingAPI.Framework;
using StardewValley;

namespace StardewModdingAPI
{
    internal class GameConstants
    {
        /// <summary>The game's current semantic version.</summary>
        internal static ISemanticVersion GameVersion { get; } = new GameVersion( Game1.version );

        /// <summary>The language code for non-translated mod assets.</summary>
        internal static LocalizedContentManager.LanguageCode DefaultLanguage { get; } = LocalizedContentManager.LanguageCode.en;
        
        /// <summary>Get the name of the save folder, if any.</summary>
        internal static string GetSaveFolderName()
        {
            // save not available
            if ( Context.LoadStage == LoadStage.None )
                return null;

            // get basic info
            string playerName;
            ulong saveID;
            if ( Context.LoadStage == LoadStage.SaveParsed )
            {
                playerName = SaveGame.loaded.player.Name;
                saveID = SaveGame.loaded.uniqueIDForThisGame;
            }
            else
            {
                playerName = Game1.player.Name;
                saveID = Game1.uniqueIDForThisGame;
            }

            // build folder name
            return $"{new string( playerName.Where( char.IsLetterOrDigit ).ToArray() )}_{saveID}";
        }

        /// <summary>Get the path to the current save folder, if any.</summary>
        internal static string GetSaveFolderPathIfExists()
        {
            string folderName = GameConstants.GetSaveFolderName();
            if ( folderName == null )
                return null;

            string path = Path.Combine(Constants.SavesPath, folderName);
            return Directory.Exists( path )
                ? path
                : null;
        }
    }
}
