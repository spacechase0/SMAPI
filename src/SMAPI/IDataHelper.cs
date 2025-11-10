using System;
using Netcode;

namespace StardewModdingAPI;

/// <summary>Provides an API for reading and storing local mod data.</summary>
public interface IDataHelper
{
    /*********
    ** Public methods
    *********/
    /****
    ** JSON file
    ****/
    /// <summary>Read data from a JSON file in the mod's folder.</summary>
    /// <typeparam name="TModel">The model type. This should be a plain class that has public properties for the data you want. The properties can be complex types.</typeparam>
    /// <param name="path">The file path relative to the mod folder.</param>
    /// <returns>Returns the deserialized model, or <c>null</c> if the file doesn't exist or is empty.</returns>
    /// <exception cref="InvalidOperationException">The <paramref name="path"/> is not relative or contains directory climbing (../).</exception>
    TModel? ReadJsonFile<TModel>(string path)
        where TModel : class;

    /// <summary>Save data to a JSON file in the mod's folder.</summary>
    /// <typeparam name="TModel">The model type. This should be a plain class that has public properties for the data you want. The properties can be complex types.</typeparam>
    /// <param name="path">The file path relative to the mod folder.</param>
    /// <param name="data">The arbitrary data to save, or <c>null</c> to delete the file.</param>
    /// <exception cref="InvalidOperationException">The <paramref name="path"/> is not relative or contains directory climbing (../).</exception>
    void WriteJsonFile<TModel>(string path, TModel? data)
        where TModel : class;

    /****
    ** Save file
    ****/
    /// <summary>Read arbitrary data stored in the current save slot. This is only possible if a save has been loaded.</summary>
    /// <typeparam name="TModel">The model type. This should be a plain class that has public properties for the data you want. The properties can be complex types.</typeparam>
    /// <param name="key">The unique key identifying the data.</param>
    /// <returns>Returns the parsed data, or <c>null</c> if the entry doesn't exist or is empty.</returns>
    /// <exception cref="InvalidOperationException">The player hasn't loaded a save file yet or isn't the main player.</exception>
    TModel? ReadSaveData<TModel>(string key)
        where TModel : class;

    /// <summary>Save arbitrary data to the current save slot. This is only possible if a save has been loaded, and the data will be lost if the player exits without saving the current day.</summary>
    /// <typeparam name="TModel">The model type. This should be a plain class that has public properties for the data you want. The properties can be complex types.</typeparam>
    /// <param name="key">The unique key identifying the data.</param>
    /// <param name="data">The arbitrary data to save, or <c>null</c> to remove the entry.</param>
    /// <exception cref="InvalidOperationException">The player hasn't loaded a save file yet or isn't the main player.</exception>
    void WriteSaveData<TModel>(string key, TModel? data)
        where TModel : class;


    /****
    ** Global app data
    ****/
    /// <summary>Read arbitrary data stored on the local computer, synchronised by GOG/Steam if applicable.</summary>
    /// <typeparam name="TModel">The model type. This should be a plain class that has public properties for the data you want. The properties can be complex types.</typeparam>
    /// <param name="key">The unique key identifying the data.</param>
    /// <returns>Returns the parsed data, or <c>null</c> if the entry doesn't exist or is empty.</returns>
    TModel? ReadGlobalData<TModel>(string key)
        where TModel : class;

    /// <summary>Save arbitrary data to the local computer, synchronised by GOG/Steam if applicable.</summary>
    /// <typeparam name="TModel">The model type. This should be a plain class that has public properties for the data you want. The properties can be complex types.</typeparam>
    /// <param name="key">The unique key identifying the data.</param>
    /// <param name="data">The arbitrary data to save, or <c>null</c> to delete the file.</param>
    void WriteGlobalData<TModel>(string key, TModel? data)
        where TModel : class;

    /****
    ** Object data
    ****/
    /// <summary>Delegate for creating a field that will be added to a type.</summary>
    /// <returns>The created field with default values having been set (if any).</returns>
    /// <typeparam name="TParent">The type of the instance the field is being added to.</typeparam>
    /// <typeparam name="TValueType">The type of the field being returned.</typeparam>
    /// <param name="parent">The instance the field is being added to. The data for this instance (such as an <see cref="StardewValley.Item"/>'s item ID) will most likely not have been set yet.</param>
    delegate TValueType CreateFieldDelegate<TParent, TValueType>(TParent parent)
        where TParent : INetObject<NetFields>;

    /// <summary>Delegate for getting the field for a given instance of TParent.</summary>
    /// <typeparam name="TParent">The type of the instance to get the field from.</typeparam>
    /// <typeparam name="TValueType">The type of the field to get.</typeparam>
    /// <param name="parent">The instance to get the field from.</param>
    /// <returns>The field for the given instance.</returns>
    delegate ref TValueType GetFieldDelegate<TParent, TValueType>(TParent parent)
        where TParent : INetObject<NetFields>;

    /// <summary>Create a new field on TParent and all derived classes.</summary>
    /// <remarks>This must be called before the <see cref="StardewModdingAPI.Events.IGameLoopEvents.GameLaunched"/> event is raised.</remarks>
    /// <typeparam name="TParent">The type to register the field on.</typeparam>
    /// <typeparam name="TValueType">The type of the field to register.</typeparam>
    /// <param name="name">The name of field to create on TParent. This name is unique to your mod, but must not be shared by any other fields on that type (including among parent types of TParent).</param>
    /// <param name="createDelegate">A delegate to create the field instance, with any default values already set.</param>
    /// <param name="behavior">The additional behavior of the created field, if any.</param>
    GetFieldDelegate<TParent, TValueType> CreateObjectField<TParent, TValueType>(string name, CreateFieldDelegate<TParent, TValueType> createDelegate, CustomDataFieldBehavior behavior)
        where TParent : INetObject<NetFields>;

    /// <summary>Registers a serialized property on TParent and all derived classes.</summary>
    /// <typeparam name="TParent">The type to register the serialized property on.</typeparam>
    /// <typeparam name="TValueType">The type of the property to register.</typeparam>
    /// <param name="name">The name of field to create on TParent. This name is unique to your mod, but must not be shared by any other fields on that type (including among parent types of TParent).</param>
    /// <param name="getter">The function to act as a getter for the property.</param>
    /// <param name="setter">The function to act as a setter for the property.</param>
    void RegisterSerializedObjectProperty<TParent, TValueType>(string name, Func<TValueType> getter, Action<TValueType> setter)
        where TParent : INetObject<NetFields>;

    /// <summary>Get a delegate for obtaining the field instance corresponding to a specific TParent instance.</summary>
    /// <remarks>This must be called after the <see cref="StardewModdingAPI.Events.IGameLoopEvents.GameLaunched"/> event is raised.</remarks>
    /// <typeparam name="TParent">The type that the field was originally registered for.</typeparam>
    /// <typeparam name="TValueType">The type of the field to get.</typeparam>
    /// <param name="owningMod">The manifest for the mod that registered the requested field. This can be your own, or one obtained via <see cref="IModHelper.ModRegistry"/>.</param>
    /// <param name="name">The name of the field that was registered.</param>
    /// <returns>A delegate for obtaining the field instance corresponding to a specific TParent instance, or null if it doesn't exist.</returns>
    GetFieldDelegate<TParent, TValueType>? GetObjectFieldOnType<TParent, TValueType>(IManifest owningMod, string name)
        where TParent : INetObject<NetFields>;
}
