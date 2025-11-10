using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic.FileIO;
using Netcode;
using StardewValley.Network.Protocol;

namespace StardewModdingAPI.Framework.Data;

internal class AdditionalFieldsData
{
    public const string DataKey = "Pathoschild.SMAPI/AdditionalFields";
    private const string CreateDelegateKey = "Pathoschild.SMAPI/CreateFieldDelegate";

    public static AdditionalFieldsData GetFor(ProtocolTypeData typeData)
    {
        if (!typeData.CustomData.TryGetValue(DataKey, out object? additionalFieldsDataObj) || additionalFieldsDataObj is not AdditionalFieldsData additionalFieldsData)
            typeData.CustomData[DataKey] = additionalFieldsData = new AdditionalFieldsData(typeData);
        return additionalFieldsData;
    }

    private readonly ProtocolTypeData CorrespondingType;
    private readonly Dictionary<string, FieldData> Fields = new();
    private readonly HashSet<string> SerializedFields = new();
    private readonly HashSet<string> SyncedRequiredFields = new();
    private readonly HashSet<string> SyncedOptionalFields = new();

    private List<string> AllSerializedFields = [];
    private List<string> AllSyncedRequiredFields = [];
    public List<string> AllSyncedOptionalFields = [];
    private byte[] AllSyncedOptionalFieldsListData = [];

    private AdditionalFieldsData(ProtocolTypeData typeData)
    {
        this.CorrespondingType = typeData;
    }

    internal void FinalizeData(ModuleBuilder builder)
    {
        this.CorrespondingType.AdditionalFields = this.SyncedRequiredFields.Select(req => new ProtocolFieldData(this.Fields[req].Type, req)).ToList();

        this.AllSyncedRequiredFields = [.. this.SyncedRequiredFields];
        this.AllSyncedOptionalFields = [.. this.SyncedOptionalFields];
        for (ProtocolTypeData? typeData = this.CorrespondingType; typeData != null; ProtocolSummary.TryGetTypeData(typeData.CorrespondingType.BaseType, out typeData))
        {
            AdditionalFieldsData other = GetFor(typeData);
            var reqFields = other.SyncedRequiredFields.ToList();
            reqFields.Sort(string.CompareOrdinal);

            this.AllSyncedRequiredFields.InsertRange(0, reqFields);
            this.AllSyncedOptionalFields.InsertRange(0, other.SyncedOptionalFields);
        }

        using MemoryStream memory = new();
        using BinaryWriter writer = new(memory);
        writer.Write7BitEncodedInt(this.AllSyncedOptionalFields.Count);
        foreach (string entry in this.AllSyncedOptionalFields)
        {
            writer.Write(entry);
            writer.Write(this.Fields[entry].Type.ToString());
        }
        this.AllSyncedOptionalFieldsListData = memory.ToArray();

        this.MakeHoldingType(builder);
    }

    private void MakeHoldingType(ModuleBuilder builder)
    {
        string[] methodNames =
        [
            nameof(AdditionalFields.AddNetFields),
            nameof(AdditionalFields.OptionalData_ActionForEachChild),
            nameof(AdditionalFields.OptionalData_WriteFull),
            nameof(AdditionalFields.OptionalData_WriteDelta),
            nameof(AdditionalFields.OptionalData_ReadFull),
            nameof(AdditionalFields.OptionalData_ReadDelta),
        ];
        MethodInfo[] originalMethods = new MethodInfo[methodNames.Length];
        for (int i = 0; i < methodNames.Length; ++i)
            originalMethods[i] = typeof(AdditionalFields).GetMethod(methodNames[i])!;

        FieldInfo parentField = typeof(AdditionalFields).GetField(nameof(AdditionalFields.Parent), BindingFlags.NonPublic | BindingFlags.Instance)!;
        FieldInfo fieldsField = typeof(AdditionalFields).GetField(nameof(AdditionalFields.NetFields), BindingFlags.NonPublic | BindingFlags.Instance)!;
        FieldInfo dataField = typeof(AdditionalFields).GetField(nameof(AdditionalFields.Data), BindingFlags.NonPublic | BindingFlags.Instance)!;
        FieldInfo optionalIndexMappingField = typeof(AdditionalFields).GetField(nameof(AdditionalFields.SyncedOptionalIndexMapping), BindingFlags.NonPublic | BindingFlags.Instance)!;

        FieldInfo dataFieldsField = typeof(AdditionalFieldsData).GetField(nameof(AdditionalFieldsData.Fields), BindingFlags.NonPublic | BindingFlags.Instance)!;
        MethodInfo dataFieldsGetItemMethod = dataFieldsField.FieldType.GetMethod("get_Item")!;
        PropertyInfo fieldDataCreationDelegateMethod = typeof(FieldData).GetProperty(nameof(FieldData.CreationDelegate))!;

        FieldInfo fieldsOptionalDataField = typeof(NetFields).GetField(nameof(NetFields.OptionalData))!;
        FieldInfo additionalFieldDataAllOptionalsFieldField = this.GetType().GetField(nameof(this.AllSyncedOptionalFields))!;
        PropertyInfo objectNetfieldsProperty = typeof(INetObject<NetFields>).GetProperty(nameof(INetObject<NetFields>.NetFields))!;
        MethodInfo fieldsAddFieldMethod = typeof(NetFields).GetMethod(nameof(NetFields.AddField))!;
        MethodInfo actionInvokeMethod = typeof(Action<INetSerializable>).GetMethod(nameof(Action<INetSerializable>.Invoke))!;
        MethodInfo delegateDynamicInvokeMethod = typeof(Delegate).GetMethod(nameof(Delegate.DynamicInvoke))!;

        TypeBuilder type = builder.DefineType("StardewModdingAPI.Framework.AdditionalNetFields.", TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed, typeof(AdditionalFields));
        ConstructorBuilder constructor = type.DefineConstructor(MethodAttributes.Public, CallingConventions.Any, [this.CorrespondingType.CorrespondingType, this.GetType()]);
        MethodBuilder[] methods = new MethodBuilder[originalMethods.Length];
        for (int i = 0; i < methodNames.Length; ++i)
        {
            var originalMethod = originalMethods[i];
            methods[i] = type.DefineMethod(originalMethod.Name, originalMethod.Attributes & ~MethodAttributes.Abstract, originalMethod.CallingConvention, originalMethod.ReturnType, originalMethod.GetParameters().Select(p => p.ParameterType).ToArray());
        }

        ILGenerator constructorIl = constructor.GetILGenerator();
        
        ILGenerator addIl = methods[0].GetILGenerator();

        ILGenerator actionForEachIl = methods[1].GetILGenerator();

        ILGenerator optionalWriteFull = methods[2].GetILGenerator();
        ILGenerator optionalWriteDelta = methods[3].GetILGenerator();
        ILGenerator optionalReadFull = methods[4].GetILGenerator();
        ILGenerator optionalReadDelta = methods[5].GetILGenerator();
        foreach (var optionalIo in (ILGenerator[])[optionalWriteFull, optionalWriteDelta])
        {
            optionalIo.Emit(OpCodes.Ldarg_0);
            optionalIo.Emit(OpCodes.Ldfld, optionalIndexMappingField);
            optionalIo.Emit(OpCodes.Call, typeof(byte[]).GetProperty(nameof(Array.Length))!.GetMethod!);
            optionalIo.Emit(OpCodes.Ldarg_0);
            optionalIo.Emit(OpCodes.Ldfld, fieldsField);
            optionalIo.Emit(OpCodes.Ldfld, fieldsOptionalDataField);
            optionalIo.Emit(OpCodes.Call, typeof(byte[][]).GetProperty(nameof(Array.Length))!.GetMethod!);
            Label afterResetLabel = optionalWriteFull.DefineLabel();
            optionalIo.Emit(OpCodes.Bne_Un, afterResetLabel);
            optionalIo.Emit(OpCodes.Ldarg_0);
            optionalIo.Emit(OpCodes.Ldfld, optionalIndexMappingField);
            optionalIo.Emit(OpCodes.Call, typeof(byte[]).GetProperty(nameof(Array.Length))!.GetMethod!);
            optionalIo.Emit(OpCodes.Ldc_I4_1);
            optionalIo.Emit(OpCodes.Add);
            optionalIo.Emit(OpCodes.Newarr, typeof(byte[][]));
            optionalIo.MarkLabel(afterResetLabel);
        }

        foreach (string fieldName in this.Fields.Keys)
        {
            FieldData fieldData = this.Fields[fieldName];
            if (fieldData.CreationDelegate == null)
                continue;

            FieldBuilder field = type.DefineField(fieldName, fieldData.Type, FieldAttributes.Private | FieldAttributes.InitOnly);

            constructorIl.Emit(OpCodes.Ldarg_2);
            constructorIl.Emit(OpCodes.Ldfld, dataFieldsField);
            constructorIl.Emit(OpCodes.Ldstr, fieldName);
            constructorIl.Emit(OpCodes.Callvirt, dataFieldsGetItemMethod);
            constructorIl.Emit(OpCodes.Callvirt, fieldDataCreationDelegateMethod.GetMethod!);
            constructorIl.Emit(OpCodes.Ldc_I4_1);
            constructorIl.Emit(OpCodes.Newarr, typeof(object));
            constructorIl.Emit(OpCodes.Dup);
            constructorIl.Emit(OpCodes.Ldc_I4_0);
            constructorIl.Emit(OpCodes.Ldarg_1);
            constructorIl.Emit(OpCodes.Stelem_Ref);
            constructorIl.Emit(OpCodes.Callvirt, delegateDynamicInvokeMethod);
            constructorIl.Emit(OpCodes.Stfld, field);

            if (fieldData.Behavior.HasFlag(CustomDataFieldBehavior.Synced))
            {
                actionForEachIl.Emit(OpCodes.Ldarg_1);
                actionForEachIl.Emit(OpCodes.Ldarg_0);
                actionForEachIl.Emit(OpCodes.Ldfld, field);
                if (!field.FieldType.IsAssignableTo(typeof(INetSerializable)))
                    actionForEachIl.EmitCall(OpCodes.Callvirt, objectNetfieldsProperty.GetMethod!, []);
                actionForEachIl.EmitCall(OpCodes.Call, actionInvokeMethod, []);

                if (!fieldData.Behavior.HasFlag(CustomDataFieldBehavior.SyncedOptional))
                {
                    addIl.Emit(OpCodes.Ldarg_0);
                    addIl.Emit(OpCodes.Ldfld, fieldsField);
                    addIl.Emit(OpCodes.Ldarg_0);
                    addIl.Emit(OpCodes.Ldfld, field);
                    addIl.Emit(OpCodes.Ldstr, field.Name);
                    addIl.EmitCall(OpCodes.Call, fieldsAddFieldMethod, null);
                }
                else
                {
                    todo Generate all the read write methods;
                }
            }
        }

        todo finish up each method;

        todo save the types;
    }

    public void Add<TParent, TNetType>(string name, FieldData field)
        where TParent : INetObject<NetFields>
    {
        if (field.Behavior.HasFlag(CustomDataFieldBehavior.Synced))
        {
            if (!field.Type.IsAssignableTo(typeof(INetSerializable)) || !field.Type.IsAssignableTo(typeof(INetObject<NetFields>)))
                throw new InvalidOperationException($"Synced field types must implement either {typeof(INetSerializable)} or {typeof(INetObject<NetFields>)}, which {field.Type} does not.");

            if (field.Behavior.HasFlag(CustomDataFieldBehavior.SyncedOptional))
                this.SyncedOptionalFields.Add(name);
            else
                this.SyncedRequiredFields.Add(name);
        }

        this.Fields.Add(name, field);
    }

    public bool TryGetFieldCreationDelegate<TParent, TValueType>(string id, [NotNullWhen(true)] out IDataHelper.CreateFieldDelegate<TParent, TValueType>? createDelegate)
        where TParent : INetObject<NetFields>
    {
        if (!this.Fields.TryGetValue(id, out FieldData? field) || field == null)
        {
            createDelegate = default;
            return false;
        }

        if (field.Type != typeof(TValueType))
            throw new ArgumentException($"Requested type {nameof(TValueType)} did not match type of field {field.Type}");
        if (field.CreationDelegate == null)
            throw new InvalidOperationException($"The requested field {id} cannot be created.");

        createDelegate = (IDataHelper.CreateFieldDelegate<TParent, TValueType>) field.CreationDelegate;
        return true;
    }
}
