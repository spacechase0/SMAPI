using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Netcode;

namespace StardewModdingAPI;

/// <summary>Flags for determining additional behavior for mod-added data fields.</summary>
[Flags]
public enum CustomDataFieldBehavior
{
    /// <summary>No special behavior for the field will be applied. Useful for transient data, like something only used for local rendering.</summary>
    None = 0,

    /// <summary>The field will be serialized upon game save and deserialized upon game load.</summary>
    Serialized = 1 << 0,

    /// <summary>The field will be synced as a required field. The field's type must implement either <see cref="INetSerializable"/> or <see cref="INetObject{NetFields}"/>.</summary>
    Synced = 1 << 1,

    /// <summary>The field will be synced as an optional field. The field's type must implement either <see cref="INetSerializable"/> or <see cref="INetObject{NetFields}"/>.</summary>
    SyncedOptional = 1 << 2,
}
