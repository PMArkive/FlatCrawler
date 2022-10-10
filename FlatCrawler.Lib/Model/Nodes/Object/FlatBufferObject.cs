using System;
using System.Diagnostics;

namespace FlatCrawler.Lib;

public sealed record FlatBufferObject : FlatBufferNodeField
{
    public FBClass ObjectClass => (FBClass)FieldInfo.Type;

    private FlatBufferObject(int offset, VTable vTable, int dataTableOffset, FlatBufferNode parent) :
        base(offset, vTable, dataTableOffset, parent)
    {
        FieldInfo = new FBFieldInfo { Type = new FBClass() };
        RegisterObjectClass();
    }

    ~FlatBufferObject()
    {
        UnRegisterObjectClass();
    }

    public override void TrackChildFieldNode(int fieldIndex, TypeCode code, bool asArray, FlatBufferNode node)
    {
        ObjectClass.SetMemberType(fieldIndex, code, asArray);

        Fields[fieldIndex] = node;
        node.TrackFieldInfo(ObjectClass.Members[fieldIndex]);
    }

    public override void TrackFieldInfo(FBFieldInfo sharedInfo)
    {
        UnRegisterObjectClass();
        FieldInfo = sharedInfo;
        RegisterObjectClass();
    }

    public override void TrackType(FBType classType)
    {
        UnRegisterObjectClass();
        FieldInfo = FieldInfo with { Type = classType };
        RegisterObjectClass();
    }

    /// <summary>
    /// Update all data based on the ObjectClass
    /// </summary>
    private void RegisterObjectClass()
    {
        ObjectClass.AssociateVTable(VTable);

        Fields = new FlatBufferNode[ObjectClass.Members.Count];

        ObjectClass.MemberTypeChanged += OnMemberTypeChanged;
        ObjectClass.MemberCountChanged += OnMemberCountChanged;
    }

    /// <summary>
    /// Remove event associations
    /// </summary>
    private void UnRegisterObjectClass()
    {
        ObjectClass.MemberTypeChanged -= OnMemberTypeChanged;
        ObjectClass.MemberCountChanged -= OnMemberCountChanged;
    }

    private void OnMemberCountChanged(object? sender, int e)
    {
        Fields = new FlatBufferNode[e];
    }

    private void OnMemberTypeChanged(object? sender, MemberTypeChangedArgs e)
    {
        Debug.WriteLine($"Changing Member Type: {e.MemberIndex} {e.OldType} -> {e.NewType}");
        if (HasField(e.MemberIndex))
        {
            var node = ReadNode(e.MemberIndex, CommandUtil.Data.ToArray(), e.NewType.Type, e.FieldInfo.IsArray);
            Fields[e.MemberIndex] = node;
            node.TrackFieldInfo(e.FieldInfo);
        }
    }

    public static FlatBufferObject Read(int offset, FlatBufferNode parent, byte[] data)
    {
        int tableOffset = offset;
        return Read(offset, parent, data, tableOffset);
    }

    public static FlatBufferObject Read(int offset, FlatBufferNode parent, byte[] data, int tableOffset)
    {
        // Read VTable
        var vTableOffset = GetVtableOffset(tableOffset, data, true);
        var vTable = ReadVTable(vTableOffset, data);

        // Ensure VTable is correct
        if (vTableOffset < tableOffset && (vTableOffset + vTable.VTableLength) > tableOffset)
            throw new IndexOutOfRangeException("VTable overflows into Data Table. Not a valid VTable.");
        return new FlatBufferObject(offset, vTable, tableOffset, parent);
    }

    public static FlatBufferObject Read(FlatBufferNodeField parent, int fieldIndex, byte[] data)
    {
        var offset = parent.GetReferenceOffset(fieldIndex, data);
        return Read(offset, parent, data);
    }
}
