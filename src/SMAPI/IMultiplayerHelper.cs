using System;
using System.Collections.Generic;
using Galaxy.Api;
using Netcode;
using StardewModdingAPI.Framework;
using StardewValley;

namespace StardewModdingAPI;

/// <summary>Provides multiplayer utilities.</summary>
public interface IMultiplayerHelper : IModLinked
{
    /// <summary>Get a new multiplayer ID.</summary>
    long GetNewID();

    /// <summary>Get the locations which are being actively synced from the host.</summary>
    IEnumerable<GameLocation> GetActiveLocations();

    /// <summary>Get a connected player.</summary>
    /// <param name="id">The player's unique ID.</param>
    /// <returns>Returns the connected player, or <c>null</c> if no such player is connected.</returns>
    IMultiplayerPeer? GetConnectedPlayer(long id);

    /// <summary>Get all connected players.</summary>
    IEnumerable<IMultiplayerPeer> GetConnectedPlayers();

    /// <summary>Send a message to mods installed by connected players.</summary>
    /// <typeparam name="TMessage">The data type. This can be a class with a default constructor, or a value type.</typeparam>
    /// <param name="message">The data to send over the network.</param>
    /// <param name="messageType">A message type which receiving mods can use to decide whether it's the one they want to handle, like <c>SetPlayerLocation</c>. This doesn't need to be globally unique, since mods should check the originating mod ID.</param>
    /// <param name="modIDs">The mod IDs which should receive the message on the destination computers, or <c>null</c> for all mods. Specifying mod IDs is recommended to improve performance, unless it's a general-purpose broadcast.</param>
    /// <param name="playerIDs">The <see cref="Farmer.UniqueMultiplayerID" /> values for the players who should receive the message, or <c>null</c> for all players. If you don't need to broadcast to all players, specifying player IDs is recommended to reduce latency.</param>
    /// <exception cref="ArgumentNullException">The <paramref name="message"/> or <paramref name="messageType" /> is null.</exception>
    void SendMessage<TMessage>(TMessage message, string messageType, string[]? modIDs = null, long[]? playerIDs = null);

    /// <summary>Register a type for use with netfields.</summary>
    /// <remarks>This must be called before the <see cref="StardewModdingAPI.Events.IGameLoopEvents.GameLaunched"/> event is raised.</remarks>
    /// <remarks>
    ///     <para>
    ///         Requirements for a type to be registered:
    ///         <list type="bullet">
    ///             <item>All parents of a type must be registered, not just the derived type that actually ends up used.</item>
    ///             <item>It must have a constructor with no arguments.</item>
    ///         </list>
    ///     </para>
    ///     <para>When registering custom types that are generics, you must register them without any type parameters (ie. `MyCustomNetType&lt;,&gt;` instead of `MyCustomNetType&lt;NetString, int&gt;`).</para>
    ///     <para>
    ///         Additional registered net types are required by everyone connected.
    ///         <list type="bullet">
    ///             <item>For the host, this means farmhands must have all these types registered to be able to connect, and nothing else.</item>
    ///             <item>For farmhands, this means you can't connect if the host has types registered that you don't, or if you have types registered that the host doesn't.</item>
    ///         </list>
    ///     </para>
    /// </remarks>
    /// <typeparam name="TNetType">The type to register as usable.</typeparam>
    void RegisterNetSerializable<TNetType>()
        where TNetType : INetSerializable, new();

    /// <summary>Register a type for use with netfields.</summary>
    /// <remarks>This must be called before the <see cref="StardewModdingAPI.Events.IGameLoopEvents.GameLaunched"/> event is raised.</remarks>
    /// <remarks>
    ///     <para>
    ///         Requirements for a type to be registered:
    ///         <list type="bullet">
    ///             <item>All parents of a type must be registered, not just the derived type that actually ends up used.</item>
    ///             <item>It must have a constructor with no arguments.</item>
    ///         </list>
    ///     </para>
    ///     <para>
    ///         When registering a type that derives from <see cref="INetObject{NetFields}"/>, all publicly declared fields which derive from <see cref="INetSerializable"/> or <see cref="INetObject{NetFields}"/> are defined as required fields in the order that they are declared.
    ///         All fields must all be added to to that type's <see cref="INetObject{NetFields}.NetFields"/>, and in the same order they are declared.
    ///         If a field should be excluded from this, mark the field with <see cref="Netcode.Validation.NotNetFieldAttribute"/>. This also means you cannot add it as a synced field to <see cref="INetObject{NetFields}.NetFields"/>.
    ///         After registration, a temporary instance of TNetType will be created to validate this.
    ///     </para>
    ///     <para>When registering custom types that are generics, you must register them without any type parameters (ie. `MyCustomNetType&lt;,&gt;` instead of `MyCustomNetType&lt;NetString, int&gt;`).</para>
    ///     <para>
    ///         Additional registered net types are required by everyone connected.
    ///         <list type="bullet">
    ///             <item>For the host, this means farmhands must have all these types registered to be able to connect, and nothing else.</item>
    ///             <item>For farmhands, this means you can't connect if the host has types registered that you don't, or if you have types registered that the host doesn't.</item>
    ///         </list>
    ///     </para>
    /// </remarks>
    /// <typeparam name="TNetType">The type to register as usable.</typeparam>
    void RegisterNetObject<TNetType>()
        where TNetType : INetObject<NetFields>, new();
}
