using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;

if (args.Length != 2) throw new ArgumentException("Usage: MetadataIndex assembly.dll output.json");
using var stream = File.OpenRead(args[0]);
using var pe = new PEReader(stream);
var md = pe.GetMetadataReader();
var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => (ushort)o.Value);
string TypeName(EntityHandle h) => h.IsNil ? "" : h.Kind switch {
    HandleKind.TypeDefinition => DefinitionName((TypeDefinitionHandle)h),
    HandleKind.TypeReference => Name(md.GetTypeReference((TypeReferenceHandle)h).Namespace, md.GetTypeReference((TypeReferenceHandle)h).Name),
    HandleKind.TypeSpecification => "TypeSpec:" + MetadataTokens.GetToken(h).ToString("X8"),
    _ => h.IsNil ? "" : h.Kind.ToString()
};
string DefinitionName(TypeDefinitionHandle h) { var t=md.GetTypeDefinition(h); return t.GetDeclaringType().IsNil ? Name(t.Namespace,t.Name) : TypeName(t.GetDeclaringType())+"+"+md.GetString(t.Name); }
string Name(StringHandle ns, StringHandle name) => (md.GetString(ns) is string s && s.Length > 0 ? s + "." : "") + md.GetString(name);
string Symbol(EntityHandle h) => h.Kind switch {
    HandleKind.MethodDefinition => TypeName(md.GetMethodDefinition((MethodDefinitionHandle)h).GetDeclaringType()) + "::" + md.GetString(md.GetMethodDefinition((MethodDefinitionHandle)h).Name),
    HandleKind.MemberReference => TypeName(md.GetMemberReference((MemberReferenceHandle)h).Parent) + "::" + md.GetString(md.GetMemberReference((MemberReferenceHandle)h).Name),
    HandleKind.FieldDefinition => TypeName(md.GetFieldDefinition((FieldDefinitionHandle)h).GetDeclaringType()) + "::" + md.GetString(md.GetFieldDefinition((FieldDefinitionHandle)h).Name),
    HandleKind.MethodSpecification => Symbol(md.GetMethodSpecification((MethodSpecificationHandle)h).Method),
    _ => TypeName(h)
};
var types = new List<object>();
var failures = new List<object>();
foreach (var th in md.TypeDefinitions) {
    var t = md.GetTypeDefinition(th);
    var methods = new List<object>();
    foreach (var mh in t.GetMethods()) {
        var m = md.GetMethodDefinition(mh);
        var calls = new List<object>(); var fields = new HashSet<string>(); var literals = new List<object>();
        var ilLength = 0; var instructionCount = 0;
        if (m.RelativeVirtualAddress != 0) {
            try {
                var il = pe.GetMethodBody(m.RelativeVirtualAddress).GetILBytes()!; ilLength = il.Length;
                var pos = 0;
                while (pos < il.Length) {
                    var offset = pos; ushort code = il[pos++]; if (code == 0xfe) code = (ushort)(0xfe00 | il[pos++]);
                    var op = opcodes[code]; instructionCount++;
                    var size = op.OperandType switch {
                        OperandType.InlineNone => 0,
                        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                        OperandType.InlineVar => 2,
                        OperandType.InlineI8 or OperandType.InlineR => 8,
                        OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, pos),
                        _ => 4
                    };
                    if (op.OperandType == OperandType.InlineMethod) { var token = BitConverter.ToInt32(il, pos); calls.Add(new { offset, opcode = op.Name, token = token.ToString("X8"), target = Symbol(MetadataTokens.EntityHandle(token)) }); }
                    if (op.OperandType == OperandType.InlineField) fields.Add(Symbol(MetadataTokens.EntityHandle(BitConverter.ToInt32(il, pos))));
                    if (op.OperandType == OperandType.InlineString) literals.Add(new { offset, value = md.GetUserString(MetadataTokens.UserStringHandle(BitConverter.ToInt32(il, pos) & 0xffffff)) });
                    pos += size;
                    if (pos > il.Length) throw new InvalidDataException("IL operand exceeds method body");
                }
            } catch (Exception ex) { failures.Add(new { method = Symbol(mh), error = ex.Message }); }
        }
        methods.Add(new { name = md.GetString(m.Name), token = MetadataTokens.GetToken(mh).ToString("X8"), rva = m.RelativeVirtualAddress, attributes = m.Attributes.ToString(), signatureHex = Convert.ToHexString(md.GetBlobBytes(m.Signature)), parameters = m.GetParameters().Select(p => new { name = md.GetString(md.GetParameter(p).Name), sequence = md.GetParameter(p).SequenceNumber }).ToArray(), ilLength, instructionCount, calls, fields = fields.Order().ToArray(), literals });
    }
    types.Add(new { name = TypeName(th), token = MetadataTokens.GetToken(th).ToString("X8"), declaringType = t.GetDeclaringType().IsNil ? null : TypeName(t.GetDeclaringType()), baseType = TypeName(t.BaseType), attributes = t.Attributes.ToString(), fields = t.GetFields().Select(f => new { name = md.GetString(md.GetFieldDefinition(f).Name), token = MetadataTokens.GetToken(f).ToString("X8"), attributes = md.GetFieldDefinition(f).Attributes.ToString(), signatureHex = Convert.ToHexString(md.GetBlobBytes(md.GetFieldDefinition(f).Signature)) }).ToArray(), methods });
}
var result = new { assembly = md.GetString(md.GetAssemblyDefinition().Name), version = md.GetAssemblyDefinition().Version.ToString(), machine=pe.PEHeaders.CoffHeader.Machine.ToString(), corFlags=pe.PEHeaders.CorHeader?.Flags.ToString(), references = md.AssemblyReferences.Select(r => new { name = md.GetString(md.GetAssemblyReference(r).Name), version = md.GetAssemblyReference(r).Version.ToString() }).ToArray(), typeCount = md.TypeDefinitions.Count, methodCount = md.MethodDefinitions.Count, fieldCount = md.FieldDefinitions.Count, types, failures };
File.WriteAllText(args[1], JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Types={md.TypeDefinitions.Count} Methods={md.MethodDefinitions.Count} Fields={md.FieldDefinitions.Count} IL failures={failures.Count}");
