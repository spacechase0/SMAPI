using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Netcode;
using StardewValley.Network;
using StardewValley.Network.Protocol;

namespace StardewModdingAPI.Framework.Data;

internal abstract class AdditionalFields : IHaveAdditionalNetFields
{
    internal readonly INetObject<NetFields> Parent;
    internal readonly NetFields NetFields;
    internal readonly AdditionalFieldsData Data;
    internal readonly int[] SyncedOptionalIndexMapping;

    public AdditionalFields(INetObject<NetFields> parent)
    {
        this.Parent = parent;
        this.NetFields = parent.NetFields;

        if (!ProtocolSummary.TryGetTypeData(parent.GetType(), out ProtocolTypeData typeData) || typeData == null)
            throw new ArgumentException("Type was not in protocol summary", nameof(parent));
        this.Data = AdditionalFieldsData.GetFor(typeData);

        this.SyncedOptionalIndexMapping = Enumerable.Range(0, this.Data.AllSyncedOptionalFields.Count).ToArray();
    }

    public abstract void AddNetFields();
    public abstract void OptionalData_ActionForEachChild(Action<INetSerializable> childAction);
    public abstract bool OptionalData_WriteFull(BinaryWriter writer);
    public abstract bool OptionalData_WriteDelta(BinaryWriter writer);
    public abstract bool OptionalData_ReadFull(BinaryReader reader, NetVersion version);
    public abstract bool OptionalData_ReadDelta(BinaryReader reader, NetVersion version);

    protected abstract Delegate GetDelegateFor(string id);

    public IDataHelper.GetFieldDelegate<TParent, TNetType> GetObjectDelegateFor<TParent, TNetType>(string id)
        where TParent : INetObject<NetFields>
    {
        todo;
        if (!this.Data.TryGetObjectCreationDelegate(id, out IDataHelper.CreateFieldDelegate<TParent, TNetType>? createDelegate))
            throw new ArgumentException("No such field was registered", nameof(id));

        // TODO: This should be optimized somehow since the returned delegate will likely end up being called up often.
        //       Perhaps a type could be generated at runtime per type with additional netfields, and the delegate is just the appropriate getter?
        //       Similar to how vanilla's LocalMultiplayer manages static variables
        return (parentObj) =>
        {
            if (!this.NetFields.TryGetValue(id, out object? ret) || ret == null)
            {
                this.NetFields[id] = ret = createDelegate((TParent)this.Parent);
            }
            return (TNetType)ret;
        };
    }
}
