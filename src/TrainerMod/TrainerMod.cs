using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Tools;
using TrainerMod.ItemData;
using Object = StardewValley.Object;

namespace TrainerMod
{
    /// <summary>The main entry point for the mod.</summary>
    public class TrainerMod : Mod
    {
        /*********
        ** Properties
        *********/
        /// <summary>The time of day at which to freeze time.</summary>
        private int FrozenTime;

        /// <summary>Whether to keep the player's health at its maximum.</summary>
        private bool InfiniteHealth;

        /// <summary>Whether to keep the player's stamina at its maximum.</summary>
        private bool InfiniteStamina;

        /// <summary>Whether to keep the player's money at a set value.</summary>
        private bool InfiniteMoney;

        /// <summary>Whether to freeze time.</summary>
        private bool FreezeTime;


        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            this.RegisterCommands(helper);
            GameEvents.UpdateTick += this.ReceiveUpdateTick;
        }


        /*********
        ** Private methods
        *********/
        /****
        ** Implementation
        ****/
        /// <summary>The method invoked when the game updates its state.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void ReceiveUpdateTick(object sender, EventArgs e)
        {
            if (Game1.player == null)
                return;

            if (this.InfiniteHealth)
                Game1.player.health = Game1.player.maxHealth;
            if (this.InfiniteStamina)
                Game1.player.stamina = Game1.player.MaxStamina;
            if (this.InfiniteMoney)
                Game1.player.money = 999999;
            if (this.FreezeTime)
                Game1.timeOfDay = this.FrozenTime;
        }

        /****
        ** Command definitions
        ****/
        /// <summary>Register all trainer commands.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        private void RegisterCommands(IModHelper helper)
        {
            helper.ConsoleCommands
                .Add("save", "Saves the game? Doesn't seem to work.", this.HandleCommand)
                .Add("load", "Shows the load screen.", this.HandleCommand)
                .Add("player_setname", "Sets the player's name.\n\nUsage: player_setname <target> <name>\n- target: what to rename (one of 'player' or 'farm').\n- name: the new name to set.", this.HandleCommand)
                .Add("player_setmoney", "Sets the player's money.\n\nUsage: player_setmoney <value>\n- value: an integer amount, or 'inf' for infinite money.", this.HandleCommand)
                .Add("player_setstamina", "Sets the player's stamina.\n\nUsage: player_setstamina [value]\n- value: an integer amount, or 'inf' for infinite stamina.", this.HandleCommand)
                .Add("player_setmaxstamina", "Sets the player's max stamina.\n\nUsage: player_setmaxstamina [value]\n- value: an integer amount.", this.HandleCommand)
                .Add("player_sethealth", "Sets the player's health.\n\nUsage: player_sethealth [value]\n- value: an integer amount, or 'inf' for infinite health.", this.HandleCommand)
                .Add("player_setmaxhealth", "Sets the player's max health.\n\nUsage: player_setmaxhealth [value]\n- value: an integer amount.", this.HandleCommand)
                .Add("player_setimmunity", "Sets the player's immunity.\n\nUsage: player_setimmunity [value]\n- value: an integer amount.", this.HandleCommand)

                .Add("player_setlevel", "Sets the player's specified skill to the specified value.\n\nUsage: player_setlevel <skill> <value>\n- skill: the skill to set (one of 'luck', 'mining', 'combat', 'farming', 'fishing', or 'foraging').\n- value: the target level (a number from 1 to 10).", this.HandleCommand)
                .Add("player_setspeed", "Sets the player's speed to the specified value?\n\nUsage: player_setspeed <value>\n- value: an integer amount (0 is normal).", this.HandleCommand)
                .Add("player_changecolor", "Sets the color of a player feature.\n\nUsage: player_changecolor <target> <color>\n- target: what to change (one of 'hair', 'eyes', or 'pants').\n- color: a color value in RGB format, like (255,255,255).", this.HandleCommand)
                .Add("player_changestyle", "Sets the style of a player feature.\n\nUsage: player_changecolor <target> <value>.\n- target: what to change (one of 'hair', 'shirt', 'skin', 'acc', 'shoe', 'swim', or 'gender').\n- value: the integer style ID.", this.HandleCommand)

                .Add("player_additem", $"Gives the player an item.\n\nUsage: player_additem <item> [count] [quality]\n- item: the item ID (use the 'list_items' command to see a list).\n- count (optional): how many of the item to give.\n- quality (optional): one of {Object.lowQuality} (normal), {Object.medQuality} (silver), {Object.highQuality} (gold), or {Object.bestQuality} (iridium).", this.HandleCommand)
                .Add("player_addweapon", "Gives the player a weapon.\n\nUsage: player_addweapon <item>\n- item: the weapon ID (use the 'list_items' command to see a list).", this.HandleCommand)
                .Add("player_addring", "Gives the player a ring.\n\nUsage: player_addring <item>\n- item: the ring ID (use the 'list_items' command to see a list).", this.HandleCommand)
                .Add("player_addwallpaper", "Gives the player a wallpaper.\n\nUsage: player_addwallpaper <wallpaper>\n- wallpaper: the wallpaper ID (ranges from 0 to 111).", this.HandleCommand)
                .Add("player_addflooring", "Gives the player a flooring.\n\nUsage: player_addflooring <flooring>\n- flooring: the flooring ID (ranges from 0 to 39).", this.HandleCommand)

                .Add("list_items", "Lists and searches items in the game data.\n\nUsage: list_items [search]\n- search (optional): an arbitrary search string to filter by.", this.HandleCommand)

                .Add("world_freezetime", "Freezes or resumes time.\n\nUsage: world_freezetime [value]\n- value: one of 0 (resume), 1 (freeze), or blank (toggle).", this.HandleCommand)
                .Add("world_settime", "Sets the time to the specified value.\n\nUsage: world_settime <value>\n- value: the target time in military time (like 0600 for 6am and 1800 for 6pm)", this.HandleCommand)
                .Add("world_setday", "Sets the day to the specified value.\n\nUsage: world_setday <value>.\n- value: the target day (a number from 1 to 28).", this.HandleCommand)
                .Add("world_setseason", "Sets the season to the specified value.\n\nUsage: world_setseason <season>\n- season: the target season (one of 'spring', 'summer', 'fall', 'winter').", this.HandleCommand)
                .Add("world_setyear", "Sets the year to the specified value.\n\nUsage: world_setyear <year>\n- year: the target year (a number starting from 1).", this.HandleCommand)
                .Add("world_downminelevel", "Goes down one mine level?", this.HandleCommand)
                .Add("world_setminelevel", "Sets the mine level?\n\nUsage: world_setminelevel <value>\n- value: The target level (a number between 1 and 120).", this.HandleCommand)

                .Add("show_game_files", "Opens the game folder.", this.HandleCommand)
                .Add("show_data_files", "Opens the folder containing the save and log files.", this.HandleCommand)

                .Add("debug", "Run one of the game's debug commands; for example, 'debug warp FarmHouse 1 1' warps the player to the farmhouse.", this.HandleCommand);
        }

        /// <summary>Handle a TrainerMod command.</summary>
        /// <param name="command">The command name.</param>
        /// <param name="args">The command arguments.</param>
        private void HandleCommand(string command, string[] args)
        {
            switch (command)
            {
                case "debug":
                    // submit command
                    string debugCommand = string.Join(" ", args);
                    string oldOutput = Game1.debugOutput;
                    Game1.game1.parseDebugInput(debugCommand);

                    // show result
                    this.Monitor.Log(Game1.debugOutput != oldOutput
                        ? $"> {Game1.debugOutput}"
                        : "Sent debug command to the game, but there was no output.", LogLevel.Info);
                    break;

                case "save":
                    this.Monitor.Log("Saving the game...", LogLevel.Info);
                    SaveGame.Save();
                    break;

                case "load":
                    this.Monitor.Log("Triggering load menu...", LogLevel.Info);
                    Game1.hasLoadedGame = false;
                    Game1.activeClickableMenu = new LoadGameMenu();
                    break;

                case "player_setname":
                    if (args.Length > 1)
                    {
                        string target = args[0];
                        string[] validTargets = { "player", "farm" };
                        if (validTargets.Contains(target))
                        {
                            switch (target)
                            {
                                case "player":
                                    Game1.player.Name = args[1];
                                    this.Monitor.Log($"OK, your player's name is now {Game1.player.Name}.", LogLevel.Info);
                                    break;
                                case "farm":
                                    Game1.player.farmName = args[1];
                                    this.Monitor.Log($"OK, your farm's name is now {Game1.player.Name}.", LogLevel.Info);
                                    break;
                            }
                        }
                        else
                            this.LogArgumentsInvalid(command);
                    }
                    else
                        this.Monitor.Log($"Your name is currently '{Game1.player.Name}'. Type 'help player_setname' for usage.", LogLevel.Info);
                    break;

                case "player_setmoney":
                    if (args.Any())
                    {
                        string amountStr = args[0];
                        if (amountStr == "inf")
                        {
                            this.InfiniteMoney = true;
                            this.Monitor.Log("OK, you now have infinite money.", LogLevel.Info);
                        }
                        else
                        {
                            this.InfiniteMoney = false;
                            int amount;
                            if (int.TryParse(amountStr, out amount))
                            {
                                Game1.player.Money = amount;
                                this.Monitor.Log($"OK, you now have {Game1.player.Money} gold.", LogLevel.Info);
                            }
                            else
                                this.LogArgumentNotInt(command);
                        }
                    }
                    else
                        this.Monitor.Log($"You currently have {(this.InfiniteMoney ? "infinite" : Game1.player.Money.ToString())} gold. Specify a value to change it.", LogLevel.Info);
                    break;

                case "player_setstamina":
                    if (args.Any())
                    {
                        string amountStr = args[0];
                        if (amountStr == "inf")
                        {
                            this.InfiniteStamina = true;
                            this.Monitor.Log("OK, you now have infinite stamina.", LogLevel.Info);
                        }
                        else
                        {
                            this.InfiniteStamina = false;
                            int amount;
                            if (int.TryParse(amountStr, out amount))
                            {
                                Game1.player.Stamina = amount;
                                this.Monitor.Log($"OK, you now have {Game1.player.Stamina} stamina.", LogLevel.Info);
                            }
                            else
                                this.LogArgumentNotInt(command);
                        }
                    }
                    else
                        this.Monitor.Log($"You currently have {(this.InfiniteStamina ? "infinite" : Game1.player.Stamina.ToString())} stamina. Specify a value to change it.", LogLevel.Info);
                    break;

                case "player_setmaxstamina":
                    if (args.Any())
                    {
                        int amount;
                        if (int.TryParse(args[0], out amount))
                        {
                            Game1.player.MaxStamina = amount;
                            this.Monitor.Log($"OK, you now have {Game1.player.MaxStamina} max stamina.", LogLevel.Info);
                        }
                        else
                            this.LogArgumentNotInt(command);
                    }
                    else
                        this.Monitor.Log($"You currently have {Game1.player.MaxStamina} max stamina. Specify a value to change it.", LogLevel.Info);
                    break;

                case "player_setlevel":
                    if (args.Length > 1)
                    {
                        string skill = args[0];
                        string[] skills = { "luck", "mining", "combat", "farming", "fishing", "foraging" };
                        if (skills.Contains(skill))
                        {
                            int level;
                            if (int.TryParse(args[1], out level))
                            {
                                switch (skill)
                                {
                                    case "luck":
                                        Game1.player.LuckLevel = level;
                                        this.Monitor.Log($"OK, your luck skill is now {Game1.player.LuckLevel}.", LogLevel.Info);
                                        break;
                                    case "mining":
                                        Game1.player.MiningLevel = level;
                                        this.Monitor.Log($"OK, your mining skill is now {Game1.player.MiningLevel}.", LogLevel.Info);
                                        break;
                                    case "combat":
                                        Game1.player.CombatLevel = level;
                                        this.Monitor.Log($"OK, your combat skill is now {Game1.player.CombatLevel}.", LogLevel.Info);
                                        break;
                                    case "farming":
                                        Game1.player.FarmingLevel = level;
                                        this.Monitor.Log($"OK, your farming skill is now {Game1.player.FarmingLevel}.", LogLevel.Info);
                                        break;
                                    case "fishing":
                                        Game1.player.FishingLevel = level;
                                        this.Monitor.Log($"OK, your fishing skill is now {Game1.player.FishingLevel}.", LogLevel.Info);
                                        break;
                                    case "foraging":
                                        Game1.player.ForagingLevel = level;
                                        this.Monitor.Log($"OK, your foraging skill is now {Game1.player.ForagingLevel}.", LogLevel.Info);
                                        break;
                                }
                            }
                            else
                                this.LogArgumentNotInt(command);
                        }
                        else
                            this.LogUsageError("That isn't a valid skill.", command);
                    }
                    else
                        this.LogArgumentsInvalid(command);
                    break;

                case "player_setspeed":
                    if (args.Any())
                    {
                        int addedSpeed;
                        if (int.TryParse(args[0], out addedSpeed))
                        {
                            Game1.player.addedSpeed = addedSpeed;
                            this.Monitor.Log($"OK, your added speed is now {Game1.player.addedSpeed}.", LogLevel.Info);
                        }
                        else
                            this.LogArgumentNotInt(command);
                    }
                    else
                        this.Monitor.Log($"You currently have {Game1.player.addedSpeed} added speed. Specify a value to change it.", LogLevel.Info);
                    break;

                case "player_changecolor":
                    if (args.Length > 1)
                    {
                        string target = args[0];
                        string[] validTargets = { "hair", "eyes", "pants" };
                        if (validTargets.Contains(target))
                        {
                            string[] colorHexes = args[1].Split(new[] { ',' }, 3);
                            int r, g, b;
                            if (int.TryParse(colorHexes[0], out r) && int.TryParse(colorHexes[1], out g) && int.TryParse(colorHexes[2], out b))
                            {
                                Color color = new Color(r, g, b);
                                switch (target)
                                {
                                    case "hair":
                                        Game1.player.hairstyleColor = color;
                                        this.Monitor.Log("OK, your hair color is updated.", LogLevel.Info);
                                        break;
                                    case "eyes":
                                        Game1.player.changeEyeColor(color);
                                        this.Monitor.Log("OK, your eye color is updated.", LogLevel.Info);
                                        break;
                                    case "pants":
                                        Game1.player.pantsColor = color;
                                        this.Monitor.Log("OK, your pants color is updated.", LogLevel.Info);
                                        break;
                                }
                            }
                            else
                                this.LogUsageError("The color should be an RBG value like '255,150,0'.", command);
                        }
                        else
                            this.LogArgumentsInvalid(command);
                    }
                    else
                        this.LogArgumentsInvalid(command);
                    break;

                case "player_changestyle":
                    if (args.Length > 1)
                    {
                        string target = args[0];
                        string[] validTargets = { "hair", "shirt", "skin", "acc", "shoe", "swim", "gender" };
                        if (validTargets.Contains(target))
                        {
                            int styleID;
                            if (int.TryParse(args[1], out styleID))
                            {
                                switch (target)
                                {
                                    case "hair":
                                        Game1.player.changeHairStyle(styleID);
                                        this.Monitor.Log("OK, your hair style is updated.", LogLevel.Info);
                                        break;
                                    case "shirt":
                                        Game1.player.changeShirt(styleID);
                                        this.Monitor.Log("OK, your shirt style is updated.", LogLevel.Info);
                                        break;
                                    case "acc":
                                        Game1.player.changeAccessory(styleID);
                                        this.Monitor.Log("OK, your accessory style is updated.", LogLevel.Info);
                                        break;
                                    case "skin":
                                        Game1.player.changeSkinColor(styleID);
                                        this.Monitor.Log("OK, your skin color is updated.", LogLevel.Info);
                                        break;
                                    case "shoe":
                                        Game1.player.changeShoeColor(styleID);
                                        this.Monitor.Log("OK, your shoe style is updated.", LogLevel.Info);
                                        break;
                                    case "swim":
                                        switch (styleID)
                                        {
                                            case 0:
                                                Game1.player.changeOutOfSwimSuit();
                                                this.Monitor.Log("OK, you're no longer in your swimming suit.", LogLevel.Info);
                                                break;
                                            case 1:
                                                Game1.player.changeIntoSwimsuit();
                                                this.Monitor.Log("OK, you're now in your swimming suit.", LogLevel.Info);
                                                break;
                                            default:
                                                this.LogUsageError("The swim value should be 0 (no swimming suit) or 1 (swimming suit).", command);
                                                break;
                                        }
                                        break;
                                    case "gender":
                                        switch (styleID)
                                        {
                                            case 0:
                                                Game1.player.changeGender(true);
                                                this.Monitor.Log("OK, you're now male.", LogLevel.Info);
                                                break;
                                            case 1:
                                                Game1.player.changeGender(false);
                                                this.Monitor.Log("OK, you're now female.", LogLevel.Info);
                                                break;
                                            default:
                                                this.LogUsageError("The gender value should be 0 (male) or 1 (female).", command);
                                                break;
                                        }
                                        break;
                                }
                            }
                            else
                                this.LogArgumentsInvalid(command);
                        }
                        else
                            this.LogArgumentsInvalid(command);
                    }
                    else
                        this.LogArgumentsInvalid(command);
                    break;

                case "world_freezetime":
                    if (args.Any())
                    {
                        int value;
                        if (int.TryParse(args[0], out value))
                        {
                            if (value == 0 || value == 1)
                            {
                                this.FreezeTime = value == 1;
                                this.FrozenTime = this.FreezeTime ? Game1.timeOfDay : 0;
                                this.Monitor.Log($"OK, time is now {(this.FreezeTime ? "frozen" : "resumed")}.", LogLevel.Info);
                            }
                            else
                                this.LogUsageError("The value should be 0 (not frozen), 1 (frozen), or empty (toggle).", command);
                        }
                        else
                            this.LogArgumentNotInt(command);
                    }
                    else
                    {
                        this.FreezeTime = !this.FreezeTime;
                        this.FrozenTime = this.FreezeTime ? Game1.timeOfDay : 0;
                        this.Monitor.Log($"OK, time is now {(this.FreezeTime ? "frozen" : "resumed")}.", LogLevel.Info);
                    }
                    break;

                case "world_settime":
                    if (args.Any())
                    {
                        int time;
                        if (int.TryParse(args[0], out time))
                        {
                            if (time <= 2600 && time >= 600)
                            {
                                Game1.timeOfDay = time;
                                this.FrozenTime = this.FreezeTime ? Game1.timeOfDay : 0;
                                this.Monitor.Log($"OK, the time is now {Game1.timeOfDay.ToString().PadLeft(4, '0')}.", LogLevel.Info);
                            }
                            else
                                this.LogUsageError("That isn't a valid time.", command);
                        }
                        else
                            this.LogArgumentNotInt(command);
                    }
                    else
                        this.Monitor.Log($"The current time is {Game1.timeOfDay}. Specify a value to change it.", LogLevel.Info);
                    break;

                case "world_setday":
                    if (args.Any())
                    {
                        int day;
                        if (int.TryParse(args[0], out day))
                        {
                            if (day <= 28 && day > 0)
                            {
                                Game1.dayOfMonth = day;
                                this.Monitor.Log($"OK, the date is now {Game1.currentSeason} {Game1.dayOfMonth}.", LogLevel.Info);
                            }
                            else
                                this.LogUsageError("That isn't a valid day.", command);
                        }
                        else
                            this.LogArgumentNotInt(command);
                    }
                    else
                        this.Monitor.Log($"The current date is {Game1.currentSeason} {Game1.dayOfMonth}. Specify a value to change the day.", LogLevel.Info);
                    break;

                case "world_setseason":
                    if (args.Any())
                    {
                        string season = args[0];
                        string[] validSeasons = { "winter", "spring", "summer", "fall" };
                        if (validSeasons.Contains(season))
                        {
                            Game1.currentSeason = season;
                            this.Monitor.Log($"OK, the date is now {Game1.currentSeason} {Game1.dayOfMonth}.", LogLevel.Info);
                        }
                        else
                            this.LogUsageError("That isn't a valid season name.", command);
                    }
                    else
                        this.Monitor.Log($"The current season is {Game1.currentSeason}. Specify a value to change it.", LogLevel.Info);
                    break;

                case "world_setyear":
                    if (args.Any())
                    {
                        int year;
                        if (int.TryParse(args[0], out year))
                        {
                            if (year >= 1)
                            {
                                Game1.year = year;
                                this.Monitor.Log($"OK, the year is now {Game1.year}.", LogLevel.Info);
                            }
                            else
                                this.LogUsageError("That isn't a valid year.", command);
                        }
                        else
                            this.LogArgumentNotInt(command);
                    }
                    else
                        this.Monitor.Log($"The current year is {Game1.year}. Specify a value to change the year.", LogLevel.Info);
                    break;

                case "player_sethealth":
                    if (args.Any())
                    {
                        string amountStr = args[0];

                        if (amountStr == "inf")
                        {
                            this.InfiniteHealth = true;
                            this.Monitor.Log("OK, you now have infinite health.", LogLevel.Info);
                        }
                        else
                        {
                            this.InfiniteHealth = false;
                            int amount;
                            if (int.TryParse(amountStr, out amount))
                            {
                                Game1.player.health = amount;
                                this.Monitor.Log($"OK, you now have {Game1.player.health} health.", LogLevel.Info);
                            }
                            else
                                this.LogArgumentNotInt(command);
                        }
                    }
                    else
                        this.Monitor.Log($"You currently have {(this.InfiniteHealth ? "infinite" : Game1.player.health.ToString())} health. Specify a value to change it.", LogLevel.Info);
                    break;

                case "player_setmaxhealth":
                    if (args.Any())
                    {
                        int maxHealth;
                        if (int.TryParse(args[0], out maxHealth))
                        {
                            Game1.player.maxHealth = maxHealth;
                            this.Monitor.Log($"OK, you now have {Game1.player.maxHealth} max health.", LogLevel.Info);
                        }
                        else
                            this.LogArgumentNotInt(command);
                    }
                    else
                        this.Monitor.Log($"You currently have {Game1.player.maxHealth} max health. Specify a value to change it.", LogLevel.Info);
                    break;

                case "player_setimmunity":
                    if (args.Any())
                    {
                        int amount;
                        if (int.TryParse(args[0], out amount))
                        {
                            Game1.player.immunity = amount;
                            this.Monitor.Log($"OK, you now have {Game1.player.immunity} immunity.", LogLevel.Info);
                        }
                        else
                            this.LogArgumentNotInt(command);
                    }
                    else
                        this.Monitor.Log($"You currently have {Game1.player.immunity} immunity. Specify a value to change it.", LogLevel.Info);
                    break;

                case "player_additem":
                    if (args.Any())
                    {
                        int itemID;
                        if (int.TryParse(args[0], out itemID))
                        {
                            int count = 1;
                            int quality = 0;
                            if (args.Length > 1)
                            {
                                if (!int.TryParse(args[1], out count))
                                {
                                    this.LogUsageError("The optional count is invalid.", command);
                                    return;
                                }

                                if (args.Length > 2 && !int.TryParse(args[2], out quality))
                                {
                                    this.LogUsageError("The optional quality is invalid.", command);
                                    return;
                                }
                            }

                            var item = new Object(itemID, count) { quality = quality };
                            if (item.Name == "Error Item")
                                this.Monitor.Log("There is no such item ID.", LogLevel.Error);
                            else
                            {
                                Game1.player.addItemByMenuIfNecessary(item);
                                this.Monitor.Log($"OK, added {item.Name} to your inventory.", LogLevel.Info);
                            }
                        }
                        else
                            this.LogUsageError("The item ID must be an integer.", command);
                    }
                    else
                        this.LogArgumentsInvalid(command);
                    break;

                case "player_addweapon":
                    if (args.Any())
                    {
                        int weaponID;
                        if (int.TryParse(args[0], out weaponID))
                        {
                            // get raw weapon data
                            string data;
                            if (!Game1.content.Load<Dictionary<int, string>>("Data\\weapons").TryGetValue(weaponID, out data))
                            {
                                this.Monitor.Log("There is no such weapon ID.", LogLevel.Error);
                                return;
                            }

                            // get raw weapon type
                            int type;
                            {
                                string[] fields = data.Split('/');
                                string typeStr = fields.Length > 8 ? fields[8] : null;
                                if (!int.TryParse(typeStr, out type))
                                {
                                    this.Monitor.Log("Could not parse the data for the weapon with that ID.", LogLevel.Error);
                                    return;
                                }
                            }

                            // get weapon
                            Tool weapon;
                            switch (type)
                            {
                                case MeleeWeapon.stabbingSword:
                                case MeleeWeapon.dagger:
                                case MeleeWeapon.club:
                                case MeleeWeapon.defenseSword:
                                    weapon = new MeleeWeapon(weaponID);
                                    break;

                                case 4:
                                    weapon = new Slingshot(weaponID);
                                    break;

                                default:
                                    this.Monitor.Log($"The specified weapon has unknown type '{type}' in the game data.", LogLevel.Error);
                                    return;
                            }

                            // validate
                            if (weapon.Name == null)
                            {
                                this.Monitor.Log("That weapon doesn't seem to be valid.", LogLevel.Error);
                                return;
                            }

                            // add weapon
                            Game1.player.addItemByMenuIfNecessary(weapon);
                            this.Monitor.Log($"OK, added {weapon.Name} to your inventory.", LogLevel.Info);
                        }
                        else
                            this.LogUsageError("The weapon ID must be an integer.", command);
                    }
                    else
                        this.LogArgumentsInvalid(command);
                    break;

                case "player_addring":
                    if (args.Any())
                    {
                        int ringID;
                        if (int.TryParse(args[0], out ringID))
                        {
                            if (ringID < Ring.ringLowerIndexRange || ringID > Ring.ringUpperIndexRange)
                                this.Monitor.Log($"There is no such ring ID (must be between {Ring.ringLowerIndexRange} and {Ring.ringUpperIndexRange}).", LogLevel.Error);
                            else
                            {
                                Ring ring = new Ring(ringID);
                                Game1.player.addItemByMenuIfNecessary(ring);
                                this.Monitor.Log($"OK, added {ring.Name} to your inventory.", LogLevel.Info);
                            }
                        }
                        else
                            this.Monitor.Log("<item> is invalid", LogLevel.Error);
                    }
                    else
                        this.LogArgumentsInvalid(command);
                    break;

                case "player_addwallpaper":
                case "player_addflooring":
                    if (args.Any())
                    {
                        string type = command.Substring(10);
                        int wallpaperID;
                        if (int.TryParse(args[0], out wallpaperID))
                        {
                            int upperID = type == "wallpaper" ? 111 : 39;
                            if (wallpaperID < 0 || wallpaperID > upperID)
                                this.Monitor.Log($"There is no such {type} ID (must be between 0 and {upperID}).", LogLevel.Error);
                            else
                            {
                                Wallpaper wallpaper = new Wallpaper(wallpaperID, type == "flooring" );
                                Game1.player.addItemByMenuIfNecessary(wallpaper);
                                this.Monitor.Log($"OK, added {type} {wallpaperID} to your inventory.", LogLevel.Info);
                            }
                        }
                        else
                            this.Monitor.Log($"<{type}> is invalid", LogLevel.Error);
                    }
                    else
                        this.LogArgumentsInvalid(command);
                    break;

                case "list_items":
                    {
                        var matches = this.GetItems(args).ToArray();

                        // show matches
                        string summary = "Searching...\n";
                        if (matches.Any())
                            this.Monitor.Log(summary + this.GetTableString(matches, new[] { "type", "id", "name" }, val => new[] { val.Type.ToString(), val.ID.ToString(), val.Name }), LogLevel.Info);
                        else
                            this.Monitor.Log(summary + "No items found", LogLevel.Info);
                    }
                    break;

                case "world_downminelevel":
                    {
                        int level = (Game1.currentLocation as MineShaft)?.mineLevel ?? 0;
                        this.Monitor.Log($"OK, warping you to mine level {level + 1}.", LogLevel.Info);
                        Game1.enterMine(false, level + 1, "");
                        break;
                    }

                case "world_setminelevel":
                    if (args.Any())
                    {
                        int level;
                        if (int.TryParse(args[0], out level))
                        {
                            level = Math.Max(1, level);
                            this.Monitor.Log($"OK, warping you to mine level {level}.", LogLevel.Info);
                            Game1.enterMine(true, level, "");
                        }
                        else
                            this.LogArgumentNotInt(command);
                    }
                    else
                        this.LogArgumentsInvalid(command);
                    break;

                case "show_game_files":
                    Process.Start(Constants.ExecutionPath);
                    this.Monitor.Log($"OK, opening {Constants.ExecutionPath}.", LogLevel.Info);
                    break;

                case "show_data_files":
                    Process.Start(Constants.DataPath);
                    this.Monitor.Log($"OK, opening {Constants.DataPath}.", LogLevel.Info);
                    break;

                default:
                    throw new NotImplementedException($"TrainerMod received unknown command '{command}'.");
            }
        }

        /****
        ** Helpers
        ****/
        /// <summary>Get all items which can be searched and added to the player's inventory through the console.</summary>
        /// <param name="searchWords">The search string to find.</param>
        private IEnumerable<ISearchItem> GetItems(string[] searchWords)
        {
            // normalise search term
            searchWords = searchWords?.Where(word => !string.IsNullOrWhiteSpace(word)).ToArray();
            if (searchWords?.Any() == false)
                searchWords = null;

            // find matches
            return (
                from item in this.GetItems()
                let term = $"{item.ID}|{item.Type}|{item.Name}"
                where searchWords == null || searchWords.All(word => term.IndexOf(word, StringComparison.CurrentCultureIgnoreCase) != -1)
                select item
            );
        }

        /// <summary>Get all items which can be searched and added to the player's inventory through the console.</summary>
        private IEnumerable<ISearchItem> GetItems()
        {
            // objects
            foreach (int id in Game1.objectInformation.Keys)
            {
                ISearchItem obj = id >= Ring.ringLowerIndexRange && id <= Ring.ringUpperIndexRange
                    ? new SearchableRing(id)
                    : (ISearchItem)new SearchableObject(id);
                if (obj.IsValid)
                    yield return obj;
            }

            // weapons
            foreach (int id in Game1.content.Load<Dictionary<int, string>>("Data\\weapons").Keys)
            {
                ISearchItem weapon = new SearchableWeapon(id);
                if (weapon.IsValid)
                    yield return weapon;
            }
        }

        /// <summary>Get an ASCII table for a set of tabular data.</summary>
        /// <typeparam name="T">The data type.</typeparam>
        /// <param name="data">The data to display.</param>
        /// <param name="header">The table header.</param>
        /// <param name="getRow">Returns a set of fields for a data value.</param>
        private string GetTableString<T>(IEnumerable<T> data, string[] header, Func<T, string[]> getRow)
        {
            // get table data
            int[] widths = header.Select(p => p.Length).ToArray();
            string[][] rows = data
                .Select(item =>
                {
                    string[] fields = getRow(item);
                    if (fields.Length != widths.Length)
                        throw new InvalidOperationException($"Expected {widths.Length} columns, but found {fields.Length}: {string.Join(", ", fields)}");

                    for (int i = 0; i < fields.Length; i++)
                        widths[i] = Math.Max(widths[i], fields[i].Length);

                    return fields;
                })
                .ToArray();

            // render fields
            List<string[]> lines = new List<string[]>(rows.Length + 2)
            {
                header,
                header.Select((value, i) => "".PadRight(widths[i], '-')).ToArray()
            };
            lines.AddRange(rows);

            return string.Join(
                Environment.NewLine,
                lines.Select(line => string.Join(" | ",
                    line.Select((field, i) => field.PadRight(widths[i], ' ')).ToArray())
                )
            );
        }

        /****
        ** Logging
        ****/
        /// <summary>Log an error indicating incorrect usage.</summary>
        /// <param name="error">A sentence explaining the problem.</param>
        /// <param name="command">The name of the command.</param>
        private void LogUsageError(string error, string command)
        {
            this.Monitor.Log($"{error} Type 'help {command}' for usage.", LogLevel.Error);
        }

        /// <summary>Log an error indicating a value must be an integer.</summary>
        /// <param name="command">The name of the command.</param>
        private void LogArgumentNotInt(string command)
        {
            this.LogUsageError("The value must be a whole number.", command);
        }

        /// <summary>Log an error indicating a value is invalid.</summary>
        /// <param name="command">The name of the command.</param>
        private void LogArgumentsInvalid(string command)
        {
            this.LogUsageError("The arguments are invalid.", command);
        }
    }
}
