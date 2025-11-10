using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Galaxy.Api;
using Netcode;
using Netcode.Validation;
using StardewModdingAPI.Events;
using StardewModdingAPI.Framework.Data;
using StardewModdingAPI.Framework.Networking;
using StardewValley;
using StardewValley.Network.Protocol;
using static StardewModdingAPI.IMultiplayerHelper;

namespace StardewModdingAPI.Framework.ModHelpers;

/// <summary>Provides multiplayer utilities.</summary>
internal class MultiplayerHelper : BaseHelper, IMultiplayerHelper
{
    /*********
    ** Fields
    *********/
    /// <summary>SMAPI's core multiplayer utility.</summary>
    private readonly SMultiplayer Multiplayer;


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="mod">The mod using this instance.</param>
    /// <param name="multiplayer">SMAPI's core multiplayer utility.</param>
    public MultiplayerHelper(IModMetadata mod, SMultiplayer multiplayer)
        : base(mod)
    {
        this.Multiplayer = multiplayer;
    }

    /// <inheritdoc />
    public long GetNewID()
    {
        return this.Multiplayer.getNewID();
    }

    /// <inheritdoc />
    public IEnumerable<GameLocation> GetActiveLocations()
    {
        return this.Multiplayer.activeLocations();
    }

    /// <inheritdoc />
    public IMultiplayerPeer? GetConnectedPlayer(long id)
    {
        return this.Multiplayer.Peers.TryGetValue(id, out MultiplayerPeer? peer)
            ? peer
            : null;
    }

    /// <inheritdoc />
    public IEnumerable<IMultiplayerPeer> GetConnectedPlayers()
    {
        return this.Multiplayer.Peers.Values;
    }

    /// <inheritdoc />
    public void SendMessage<TMessage>(TMessage message, string messageType, string[]? modIDs = null, long[]? playerIDs = null)
    {
        this.Multiplayer.BroadcastModMessage(
            message: message,
            messageType: messageType,
            fromModId: this.ModID,
            toModIds: modIDs,
            toPlayerIds: playerIDs
        );
    }

    public void RegisterNetSerializable<TNetType>() where TNetType : INetSerializable, new()
    {
        if (Context.IsGameLaunched)
            throw new InvalidOperationException($"Types cannot be registered after the {nameof(IGameLoopEvents.GameLaunched)} event has been raised");

        if (typeof(TNetType).IsGenericType && !typeof(TNetType).IsGenericTypeDefinition)
            throw new InvalidOperationException("Generic types must be registered without specified type parameters");

        for (var parent = typeof(TNetType).BaseType!; parent != typeof(object); parent = parent.BaseType!)
        {
            if (!ProtocolSummary.HasTypeData(parent))
                throw new InvalidOperationException($"Parent class {parent} of {typeof(TNetType)} has not been registered as a net type");
        }

        ProtocolTypeData typeData = new() { CorrespondingType = typeof(TNetType) };
        ProtocolSummary.TypeData.Add(typeData.CorrespondingType, typeData);
    }

    public void RegisterNetObject<TNetType>()
        where TNetType : INetObject<NetFields>, new()
    {
        if (Context.IsGameLaunched)
            throw new InvalidOperationException($"Types cannot be registered after the {nameof(IGameLoopEvents.GameLaunched)} event has been raised");

        if (typeof(TNetType).IsGenericType && !typeof(TNetType).IsGenericTypeDefinition)
            throw new InvalidOperationException("Generic types must be registered without specified type parameters");

        for (var parent = typeof(TNetType).BaseType!; parent != typeof(object); parent = parent.BaseType!)
        {
            if (!ProtocolSummary.HasTypeData(parent))
                throw new InvalidOperationException($"Parent class {parent} of {typeof(TNetType)} has not been registered as a net type");
        }

        List<ProtocolFieldData> fields = new();
        foreach (var field in typeof(TNetType).GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance))
        {
            var fieldType = field.FieldType;
            if (!fieldType.IsAssignableTo(typeof(INetSerializable)) && !fieldType.IsAssignableTo(typeof(INetObject<NetFields>)))
                continue;
            if (field.GetCustomAttribute<NotNetFieldAttribute>() != null)
                continue;

            fields.Add(new ProtocolFieldData(typeof(TNetType), field.Name) { OriginalField = field });
        }

        ProtocolTypeData typeData = new ProtocolTypeData() { CorrespondingType = typeof(TNetType), DeclaredFields = fields };
        ProtocolSummary.TypeData.Add(typeData.CorrespondingType, typeData);
    }

    public GetNetSerializableDelegate<TParent, TNetType> CreateRequiredNetFieldOnType<TParent, TNetType>(string name, IMultiplayerHelper.CreateNetSerializableDelegate<TParent, TNetType> createDelegate)
        where TParent : INetObject<NetFields>
        where TNetType : INetSerializable
    {
        if (Context.IsGameLaunched)
            throw new InvalidOperationException($"Types cannot be registered after the {nameof(IGameLoopEvents.GameLaunched)} event has been raised");

        if (!ProtocolSummary.TryGetTypeData(typeof(TParent), out var typeData))
            throw new InvalidOperationException($"The type {typeof(TParent)} has not registered yet.");
        if (!ProtocolSummary.HasTypeData(typeof(TNetType)))
            throw new InvalidOperationException($"The type {typeof(TNetType)} has not registered yet.");

        AdditionalFieldsData additionalFieldsData = AdditionalFieldsData.GetFor(typeData);
        additionalFieldsData.Add(new ProtocolFieldData(typeof(TNetType), $"{this.Mod.Manifest.UniqueID}/{name}"), createDelegate, optional: false);
        return this.GetNetSerializableOnType<TParent, TNetType>(this.Mod.Manifest, name)!;
    }

    public GetNetObjectDelegate<TParent, TNetType> CreateRequiredNetFieldOnType<TParent, TNetType>(string name, IMultiplayerHelper.CreateNetObjectDelegate<TParent, TNetType> createDelegate)
        where TParent : INetObject<NetFields>
        where TNetType : INetObject<NetFields>
    {
        if (Context.IsGameLaunched)
            throw new InvalidOperationException($"Types cannot be registered after the {nameof(IGameLoopEvents.GameLaunched)} event has been raised");

        if (!ProtocolSummary.TryGetTypeData(typeof(TParent), out var typeData))
            throw new InvalidOperationException($"The type {typeof(TParent)} has not registered yet.");
        if (!ProtocolSummary.HasTypeData(typeof(TNetType)))
            throw new InvalidOperationException($"The type {typeof(TNetType)} has not registered yet.");

        AdditionalFieldsData additionalFieldsData = AdditionalFieldsData.GetFor(typeData);
        additionalFieldsData.Add(new ProtocolFieldData(typeof(TNetType), $"{this.Mod.Manifest.UniqueID}/{name}"), createDelegate, optional: false);
        return this.GetNetObjectOnType<TParent, TNetType>(this.Mod.Manifest, name)!;
    }

    public GetNetSerializableDelegate<TParent, TNetType> CreateOptionalNetFieldOnType<TParent, TNetType>(string name, IMultiplayerHelper.CreateNetSerializableDelegate<TParent, TNetType> createDelegate)
        where TParent : INetObject<NetFields>
        where TNetType : INetSerializable
    {
        if (Context.IsGameLaunched)
            throw new InvalidOperationException($"Types cannot be registered after the {nameof(IGameLoopEvents.GameLaunched)} event has been raised");

        if (!ProtocolSummary.TryGetTypeData(typeof(TParent), out var typeData))
            throw new InvalidOperationException($"The type {typeof(TParent)} has not registered yet.");
        if (!ProtocolSummary.HasTypeData(typeof(TNetType)))
            throw new InvalidOperationException($"The type {typeof(TNetType)} has not registered yet.");

        AdditionalFieldsData additionalFieldsData = AdditionalFieldsData.GetFor(typeData);
        additionalFieldsData.Add(new ProtocolFieldData(typeof(TNetType), $"{this.Mod.Manifest.UniqueID}/{name}"), createDelegate, optional: true);
        return this.GetNetSerializableOnType<TParent, TNetType>(this.Mod.Manifest, name)!;
    }

    public GetNetObjectDelegate<TParent, TNetType> CreateOptionalNetFieldOnType<TParent, TNetType>(string name, IMultiplayerHelper.CreateNetObjectDelegate<TParent, TNetType> createDelegate)
        where TParent : INetObject<NetFields>
        where TNetType : INetObject<NetFields>
    {
        if (Context.IsGameLaunched)
            throw new InvalidOperationException($"Types cannot be registered after the {nameof(IGameLoopEvents.GameLaunched)} event has been raised");

        if (!ProtocolSummary.TryGetTypeData(typeof(TParent), out var typeData))
            throw new InvalidOperationException($"The type {typeof(TParent)} has not registered yet.");
        if (!ProtocolSummary.HasTypeData(typeof(TNetType)))
            throw new InvalidOperationException($"The type {typeof(TNetType)} has not registered yet.");

        AdditionalFieldsData additionalFieldsData = AdditionalFieldsData.GetFor(typeData);
        additionalFieldsData.Add(new ProtocolFieldData(typeof(TNetType), $"{this.Mod.Manifest.UniqueID}/{name}"), createDelegate, optional: true);
        return this.GetNetObjectOnType<TParent, TNetType>(this.Mod.Manifest, name)!;
    }

    public GetNetSerializableDelegate<TParent, TNetType>? GetNetSerializableOnType<TParent, TNetType>(IManifest owningMod, string name)
        where TParent : INetObject<NetFields>
        where TNetType : INetSerializable
    {
        if (!ProtocolSummary.TryGetTypeData(typeof(TParent), out var typeData))
            throw new InvalidOperationException($"The type {typeof(TParent)} has not registered yet.");

        var x = (TParent parent) => this.Multiplayer.GetAdditionalNetSerializableDelegateFor<TParent, TNetType>(parent, $"{this.Mod.Manifest.UniqueID}/{name}");
        GetNetSerializableDelegate < TParent, TNetType > ret = 
        return x;
    }

    public GetNetObjectDelegate<TParent, TNetType>? GetNetObjectOnType<TParent, TNetType>(IManifest owningMod, string name)
        where TParent : INetObject<NetFields>
        where TNetType : INetObject<NetFields>
    {
        if (!ProtocolSummary.TryGetTypeData(typeof(TParent), out var typeData))
            throw new InvalidOperationException($"The type {typeof(TParent)} has not registered yet.");

        return (TParent parent) => this.Multiplayer.GetAdditionalNetObjectDelegateFor<TParent, TNetType>(parent, $"{this.Mod.Manifest.UniqueID}/{name}");
    }
}
