using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using StardewModdingAPI.Framework;

namespace StardewModdingAPI
{
    internal class GameRewriter
    {
        internal static Assembly GameAssembly;
        internal static AssemblyDefinition GameAssemblyDefinition;

        internal static void RewriteAndLoad(ModRegistry modRegistry)
        {
            // Read game assembly
            var game = AssemblyDefinition.ReadAssembly( Path.Combine( Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), Program.GetExecutableAssemblyName() + ".exe" ) );

            // Do rewriting
            foreach ( var modMeta in modRegistry.GetAll() )
            {
                if ( !modMeta.IsCecilMod )
                    continue;
                ICecilMod mod = modMeta.Mod as ICecilMod;

                try
                {
                    mod.Entry( game );
                }
                catch ( Exception ex )
                {
                    modMeta.LogAsMod( $"Cecil crashed on entry; the game might not work correctly. Technical details:\n{ex.GetLogSummary()}", LogLevel.Error );
                }
            }

            // Save results and load the game
            GameAssemblyDefinition = game;
            using ( MemoryStream ms = new MemoryStream() )
            {
                game.Write( ms );
                GameAssembly = Assembly.Load( ms.ToArray() );
            }
        }
    }
}
